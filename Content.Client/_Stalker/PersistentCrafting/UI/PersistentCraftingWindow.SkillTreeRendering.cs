using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client._Stalker.PersistentCrafting;
using Content.Client._Stalker.PersistentCrafting.UI.Indexes;
using Content.Client.Message;
using Content.Shared._Stalker.PersistentCrafting;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Stalker.PersistentCrafting.UI;

public sealed partial class PersistentCraftingWindow
{
    private BoxContainer CreateSubNodeTree(
        string branch,
        IReadOnlyList<PersistentCraftNodePrototype> subNodes,
        string? selectedNodeId)
    {
        var accent = GetBranchAccent(branch);
        var nodeWidth = BaseTierTreeNodeWidth * _treeZoom;
        var nodeHeight = BaseTierTreeNodeHeight * _treeZoom;
        var horizontalGap = BaseTierTreeHorizontalGap * _treeZoom;
        var verticalGap = BaseTierTreeVerticalGap * _treeZoom;
        var padding = BaseTierTreePadding * _treeZoom;
        var lineThickness = MathF.Max(2f, BaseTierTreeLineThickness * MathF.Sqrt(_treeZoom));
        var layoutData = PersistentCraftNodeTreeLayoutBuilder.Build(subNodes);
        var positions = layoutData.Positions;
        var nodeBounds = new Dictionary<string, UIBox2>(subNodes.Count);

        var layout = new LayoutContainer
        {
            MinSize = new Vector2(
                padding * 2 + (layoutData.MaxColumn + 1) * nodeWidth + layoutData.MaxColumn * horizontalGap,
                padding * 2 + (layoutData.MaxRow + 1) * nodeHeight + layoutData.MaxRow * verticalGap + 20f * _treeZoom),
        };

        foreach (var node in subNodes)
        {
            var childPosition = GetNodeCanvasPosition(positions[node.ID], nodeWidth, nodeHeight, horizontalGap, verticalGap, padding);
            var childCenter = GetNodeCenter(childPosition, nodeWidth, nodeHeight);

            foreach (var prerequisiteId in node.Prerequisites)
            {
                if (!layoutData.ContainsNode(prerequisiteId))
                    continue;

                if (!positions.TryGetValue(prerequisiteId, out var parentGridPosition))
                    continue;

                var parentPosition = GetNodeCanvasPosition(parentGridPosition, nodeWidth, nodeHeight, horizontalGap, verticalGap, padding);
                var parentCenter = GetNodeCenter(parentPosition, nodeWidth, nodeHeight);
                var parentUnlocked = HasNodeUnlockedOrAutoAvailable(prerequisiteId);
                AddConnector(layout, parentCenter, childCenter, nodeHeight, lineThickness, accent, parentUnlocked);
            }
        }

        foreach (var node in subNodes)
        {
            var position = GetNodeCanvasPosition(positions[node.ID], nodeWidth, nodeHeight, horizontalGap, verticalGap, padding);
            var control = CreateSubNodeEntry(branch, node, selectedNodeId == node.ID, nodeWidth, nodeHeight);
            control.MinSize = new Vector2(nodeWidth, nodeHeight);
            control.MaxSize = new Vector2(nodeWidth, nodeHeight);
            LayoutContainer.SetPosition(control, position);
            layout.AddChild(control);
            nodeBounds[node.ID] = UIBox2.FromDimensions(position, new Vector2(nodeWidth, nodeHeight));
        }

        var wrapper = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Left,
            HorizontalExpand = false,
            VerticalExpand = false,
        };
        wrapper.AddChild(layout);
        _nodeBoundsByBranch[branch] = nodeBounds;
        return wrapper;
    }

    private static Vector2 GetNodeCanvasPosition(
        Vector2i gridPosition,
        float nodeWidth,
        float nodeHeight,
        float horizontalGap,
        float verticalGap,
        float padding)
    {
        return new Vector2(
            padding + gridPosition.X * (nodeWidth + horizontalGap),
            padding + gridPosition.Y * (nodeHeight + verticalGap));
    }

    private static Vector2 GetNodeCenter(Vector2 canvasPosition, float nodeWidth, float nodeHeight)
    {
        return new Vector2(
            canvasPosition.X + nodeWidth / 2f,
            canvasPosition.Y + nodeHeight / 2f);
    }

    private static void AddConnector(
        LayoutContainer layout,
        Vector2 parentCenter,
        Vector2 childCenter,
        float nodeHeight,
        float lineThickness,
        Color accent,
        bool parentUnlocked = false)
    {
        var connectorColor = parentUnlocked ? accent.WithAlpha(0.85f) : accent.WithAlpha(0.25f);
        var parentBottom = parentCenter.Y + nodeHeight / 2f;
        var childTop = childCenter.Y - nodeHeight / 2f;
        var midY = parentBottom + (childTop - parentBottom) / 2f;

        AddLine(layout, parentCenter.X, parentBottom, parentCenter.X, midY, lineThickness, connectorColor);
        AddLine(layout, Math.Min(parentCenter.X, childCenter.X), midY, Math.Max(parentCenter.X, childCenter.X), midY, lineThickness, connectorColor);
        AddLine(layout, childCenter.X, midY, childCenter.X, childTop, lineThickness, connectorColor);
    }

    private static void AddLine(
        LayoutContainer layout,
        float startX,
        float startY,
        float endX,
        float endY,
        float lineThickness,
        Color color)
    {
        var isVertical = Math.Abs(startX - endX) < 0.01f;
        var minX = isVertical
            ? startX - lineThickness / 2f
            : Math.Min(startX, endX);
        var minY = isVertical
            ? Math.Min(startY, endY)
            : startY - lineThickness / 2f;
        var width = isVertical
            ? lineThickness
            : Math.Max(Math.Abs(endX - startX), lineThickness);
        var height = isVertical
            ? Math.Max(Math.Abs(endY - startY), lineThickness)
            : lineThickness;

        var line = new PanelContainer
        {
            MinSize = new Vector2(width, height),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = color,
            }
        };

        LayoutContainer.SetPosition(line, new Vector2(minX, minY));
        layout.AddChild(line);
    }

    private ContainerButton CreateSubNodeEntry(
        string branch,
        PersistentCraftNodePrototype node,
        bool selected,
        float nodeWidth,
        float nodeHeight)
    {
        var state = _state ?? throw new InvalidOperationException("Persistent craft state is not initialized.");
        var branchState = _branchCoordinator.GetBranchState(branch);
        var unlocked = HasNodeUnlockedOrAutoAvailable(node.ID);
        var prerequisitesMet = ArePrerequisitesMet(node);
        var canUnlock = state.Loaded && !unlocked && prerequisitesMet && branchState.AvailablePoints >= node.Cost;
        var accent = GetBranchAccent(branch);
        var iconSize = Math.Clamp(64f * _treeZoom, 40f, 80f);
        var textPlateHeight = Math.Clamp(26f * _treeZoom, 20f, 34f);
        var bodyMargin = Math.Clamp(12f * _treeZoom, 8f, 14f);

        var button = new ContainerButton
        {
            MinSize = new Vector2(nodeWidth, nodeHeight),
            MaxSize = new Vector2(nodeWidth, nodeHeight),
            HorizontalExpand = false,
            VerticalExpand = false,
            StyleBoxOverride = new StyleBoxFlat
            {
                BackgroundColor = unlocked ? CardUnlockedBackground : canUnlock ? CardAvailableBackground : CardLockedBackground,
                BorderColor = selected ? SelectedBorder : unlocked ? UnlockedBorder : canUnlock ? accent.WithAlpha(0.6f) : CardBorder,
                BorderThickness = new Thickness(selected || unlocked ? 2 : 1),
                ContentMarginLeftOverride = bodyMargin,
                ContentMarginRightOverride = bodyMargin,
                ContentMarginTopOverride = bodyMargin,
                ContentMarginBottomOverride = bodyMargin,
            }
        };
        button.OnPressed += _ => SelectNode(branch, node.ID);

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        body.AddChild(CreateNodeIcon(node, selected ? SelectedBorder : accent, new Vector2(iconSize, iconSize)));
        body.AddChild(new Control { MinSize = new Vector2(1, 8) });

        var namePlate = new PanelContainer
        {
            HorizontalExpand = true,
            MinSize = new Vector2(0, textPlateHeight),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = IconBackground.WithAlpha(0.45f),
                BorderColor = CardBorder.WithAlpha(0.55f),
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 4,
                ContentMarginRightOverride = 4,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2,
            }
        };

        var nameLabel = new RichTextLabel
        {
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        };
        nameLabel.SetMarkup($"[color={HeaderTextColor.ToHex()}]{FormattedMessage.EscapeText(ResolveNodeCardCaption(node))}[/color]");
        namePlate.AddChild(nameLabel);
        body.AddChild(namePlate);
        body.AddChild(new Control { VerticalExpand = true });

        button.AddChild(body);
        return button;
    }

    private bool ArePrerequisitesMet(PersistentCraftNodePrototype node)
    {
        var state = _state ?? throw new InvalidOperationException("Persistent craft state is not initialized.");
        return PersistentCraftNodeAvailabilityResolver.ArePrerequisitesMet(state, node, ResolveNodePrototypeOrNull, _reusablePath);
    }
}
