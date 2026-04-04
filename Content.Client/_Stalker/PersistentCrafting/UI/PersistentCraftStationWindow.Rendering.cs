using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client._Stalker.PersistentCrafting.UI.Indexes;
using Content.Client._Stalker.PersistentCrafting.UI.Controls;
using Content.Client.Message;
using Content.Shared._Stalker.PersistentCrafting;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Stalker.PersistentCrafting.UI;

public sealed partial class PersistentCraftStationWindow
{
    private BoxContainer CreateTierFilterRow(
        string branch,
        IReadOnlyList<PersistentCraftRecipePrototype> recipes,
        int selectedTier,
        Color accent)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        row.AddChild(CreateTierFilterButton(branch, 0, Loc.GetString("persistent-craft-filter-all"), selectedTier == 0, accent));

        var tiers = PersistentCraftRecipeLayoutBuilder.CollectSortedTiers(recipes);
        for (var i = 0; i < tiers.Count; i++)
        {
            var tier = tiers[i];
            row.AddChild(new Control { MinSize = new Vector2(10, 1) });
            row.AddChild(CreateTierFilterButton(
                branch,
                tier,
                PersistentCraftingHelper.GetTierDisplayLabel(tier),
                selectedTier == tier,
                accent));
        }

        return row;
    }

    private Button CreateTierFilterButton(
        string branch,
        int tier,
        string text,
        bool selected,
        Color accent)
    {
        var button = new Button
        {
            Text = text,
            MinSize = new Vector2(42, 28),
            ModulateSelfOverride = selected ? PersistentCraftUiTheme.TextPrimary : PersistentCraftUiTheme.TextSecondary,
            StyleBoxOverride = new StyleBoxFlat
            {
                BackgroundColor = selected ? accent.WithAlpha(0.12f) : PersistentCraftUiTheme.SurfacePanelSoft,
                BorderColor = selected ? accent.WithAlpha(0.72f) : CardMutedBorder,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 10,
                ContentMarginRightOverride = 10,
                ContentMarginTopOverride = 5,
                ContentMarginBottomOverride = 5,
            }
        };

        button.OnPressed += _ => SelectTierFilter(branch, tier);
        return button;
    }

    private Control CreateBranchHeader(
        string branch,
        PersistentCraftBranchState branchState,
        int unlockedRecipes,
        int totalRecipes)
    {
        var accent = GetAccent(branch);
        var unlockProgress = totalRecipes == 0
            ? 0f
            : unlockedRecipes / (float) totalRecipes;
        var control = new PersistentCraftBranchSummaryBlock();
        control.SetData(
            ResolveBranchTitle(branch),
            $"{Loc.GetString("persistent-craft-branch-points-label")}: {branchState.AvailablePoints} | {Loc.GetString("persistent-craft-spent-points-label")}: {branchState.SpentPoints} | " +
            $"{Loc.GetString("persistent-craft-recipes-short")}: {unlockedRecipes}/{totalRecipes}",
            Loc.GetString("persistent-craft-unlocked-summary", ("unlocked", unlockedRecipes)),
            unlockProgress,
            accent,
            null);
        return control;
    }

    private Control CreateFilterToolbar(string branch, string searchText, bool craftableOnly)
    {
        var accent = GetAccent(branch);
        var toolbar = new PersistentCraftRecipeToolbar();
        toolbar.SetData(
            Loc.GetString("persistent-craft-search-placeholder"),
            searchText,
            Loc.GetString("persistent-craft-filter-craftable"),
            craftableOnly,
            accent);
        _activeSearchInputsByBranch[branch] = toolbar.SearchInput;
        toolbar.SearchInput.OnTextChanged += args => UpdateSearch(branch, args);
        toolbar.CraftableToggleButton.OnPressed += _ => ToggleCraftableOnly(branch);
        return toolbar;
    }

    private Control CreateRecipeListSummary(int selectedTier, int visibleRecipes, int craftableCount, bool craftableOnly, Color accent)
    {
        var tierText = selectedTier > 0
            ? $"{Loc.GetString("persistent-craft-level-label")} {PersistentCraftingHelper.GetTierDisplayLabel(selectedTier)}"
            : Loc.GetString("persistent-craft-filter-all");
        var craftableText = $"{Loc.GetString("persistent-craft-craftable-short")}: {craftableCount}";
        var filterText = craftableOnly ? $" | {Loc.GetString("persistent-craft-filter-craftable")}" : string.Empty;

        var summary = new PersistentCraftRecipeListSummary();
        summary.SetData(
            $"{tierText} | {Loc.GetString("persistent-craft-recipes-short")}: {visibleRecipes} | {craftableText}{filterText}",
            accent);
        return summary;
    }

    private Control CreateEmptyRecipeListMessage(string searchText, bool craftableOnly)
    {
        var text = craftableOnly
            ? string.IsNullOrWhiteSpace(searchText)
                ? Loc.GetString("persistent-craft-empty-craftable")
                : Loc.GetString("persistent-craft-search-empty-craftable")
            : string.IsNullOrWhiteSpace(searchText)
                ? Loc.GetString("persistent-craft-station-no-recipes")
                : Loc.GetString("persistent-craft-search-empty");

        return new Label
        {
            Text = text,
            FontColorOverride = MutedText,
        };
    }

    private PanelContainer CreateEmptyDetailPanel(string searchText, bool craftableOnly)
    {
        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = WindowBackground,
                BorderColor = CardMutedBorder,
                BorderThickness = new Thickness(1),
            }
        };

        var text = craftableOnly
            ? string.IsNullOrWhiteSpace(searchText)
                ? Loc.GetString("persistent-craft-empty-craftable-detail")
                : Loc.GetString("persistent-craft-search-empty-craftable-detail")
            : string.IsNullOrWhiteSpace(searchText)
                ? Loc.GetString("persistent-craft-empty-detail")
                : Loc.GetString("persistent-craft-search-empty-detail");

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var iconBox = new PanelContainer
        {
            MinSize = new Vector2(72, 72),
            HorizontalAlignment = HAlignment.Center,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = PersistentCraftUiTheme.SurfacePanelAlt,
                BorderColor = CardMutedBorder,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 8,
                ContentMarginBottomOverride = 8,
            }
        };
        iconBox.AddChild(new Label
        {
            Text = "?",
            FontColorOverride = MutedText.WithAlpha(0.4f),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        });

        body.AddChild(iconBox);
        body.AddChild(new Control { MinSize = new Vector2(1, 14) });
        body.AddChild(new Label
        {
            Text = text,
            FontColorOverride = MutedText,
            HorizontalAlignment = HAlignment.Center,
        });

        panel.AddChild(body);
        return panel;
    }

    private BoxContainer CreateRecipeList(
        string branch,
        IReadOnlyList<PersistentCraftRecipePrototype> recipes,
        PersistentCraftRecipePrototype selected,
        Color accent)
    {
        var list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        var tierGroups = PersistentCraftRecipeLayoutBuilder.BuildLayout(
            recipes,
            GetRecipeCategoryId,
            GetCategoryOrder,
            GetRecipeSubCategoryId,
            GetSubCategoryOrder);

        for (var tierIndex = 0; tierIndex < tierGroups.Count; tierIndex++)
        {
            var tierGroup = tierGroups[tierIndex];
            list.AddChild(CreateTierHeader(tierGroup.Tier, accent));
            list.AddChild(new Control { MinSize = new Vector2(1, 6) });

            for (var categoryIndex = 0; categoryIndex < tierGroup.Categories.Count; categoryIndex++)
            {
                var categoryGroup = tierGroup.Categories[categoryIndex];
                var categoryKey = BuildCategoryGroupKey(branch, tierGroup.Tier, categoryGroup.CategoryId);
                EnsureCategoryCollapsedByDefault(categoryKey);
                var categoryCollapsed = _viewModel.CollapsedCategoryKeys.Contains(categoryKey);
                list.AddChild(CreateCategoryHeader(
                    GetCategoryName(categoryGroup.CategoryId),
                    accent,
                    CountCategoryRecipes(categoryGroup),
                    categoryCollapsed,
                    categoryKey));
                list.AddChild(new Control { MinSize = new Vector2(1, 4) });

                if (categoryCollapsed)
                    continue;

                for (var subCategoryIndex = 0; subCategoryIndex < categoryGroup.SubCategories.Count; subCategoryIndex++)
                {
                    var subCategoryGroup = categoryGroup.SubCategories[subCategoryIndex];
                    var subCategoryId = subCategoryGroup.SubCategoryId;
                    if (!string.IsNullOrWhiteSpace(subCategoryId))
                    {
                        var subCategoryKey = BuildSubCategoryGroupKey(branch, tierGroup.Tier, categoryGroup.CategoryId, subCategoryId);
                        EnsureSubCategoryCollapsedByDefault(subCategoryKey);
                        var subCategoryCollapsed = _viewModel.CollapsedSubCategoryKeys.Contains(subCategoryKey);
                        list.AddChild(CreateSubCategoryHeader(
                            GetSubCategoryName(subCategoryId),
                            subCategoryGroup.Recipes.Count,
                            subCategoryCollapsed,
                            subCategoryKey));
                        list.AddChild(new Control { MinSize = new Vector2(1, 4) });

                        if (subCategoryCollapsed)
                            continue;
                    }

                    for (var recipeIndex = 0; recipeIndex < subCategoryGroup.Recipes.Count; recipeIndex++)
                    {
                        var recipe = subCategoryGroup.Recipes[recipeIndex];
                        list.AddChild(CreateRecipeListEntry(branch, recipe, selected.ID == recipe.ID));
                    }
                }
            }
        }

        return list;
    }

    private static int CountCategoryRecipes(PersistentCraftRecipeCategoryGroup categoryGroup)
    {
        var count = 0;
        for (var i = 0; i < categoryGroup.SubCategories.Count; i++)
        {
            count += categoryGroup.SubCategories[i].Recipes.Count;
        }

        return count;
    }

    private Control CreateTierHeader(int tier, Color accent)
    {
        var header = new PersistentCraftTierHeader();
        header.SetData(
            $"{Loc.GetString("persistent-craft-level-label")} {PersistentCraftingHelper.GetTierDisplayLabel(tier)}",
            accent);
        return header;
    }

    private Control CreateCategoryHeader(string text, Color accent, int count, bool collapsed, string categoryKey)
    {
        var header = new PersistentCraftCollapsibleHeader();
        header.SetData(
            text,
            count,
            collapsed,
            accent,
            PersistentCraftUiTheme.SurfacePanelAlt,
            accent.WithAlpha(0.28f),
            new Thickness(0, 0, 0, 6),
            5);
        header.Button.OnPressed += _ => ToggleCategoryCollapse(categoryKey);
        return header;
    }

    private Control CreateSubCategoryHeader(string text, int count, bool collapsed, string subCategoryKey)
    {
        var header = new PersistentCraftCollapsibleHeader();
        header.SetData(
            text,
            count,
            collapsed,
            MutedText,
            PersistentCraftUiTheme.SurfacePanelSoft,
            CardMutedBorder,
            new Thickness(0),
            4);
        header.Button.OnPressed += _ => ToggleSubCategoryCollapse(subCategoryKey);
        return header;
    }

    private Control CreateRecipeListEntry(string branch, PersistentCraftRecipePrototype recipe, bool selected)
    {
        var accent = GetAccent(recipe.Branch);
        var card = new PersistentCraftRecipeCard();
        card.Margin = new Thickness(0, 0, 0, 6);
        card.SetData(
            ResolveRecipeName(recipe),
            BuildRecipeSecondaryLine(recipe),
            CanViewRecipe(recipe) ? Color.White : MutedText,
            MutedText);
        card.Button.OnPressed += _ => SelectRecipe(recipe.Branch, recipe.ID);
        card.StatusContainer.AddChild(CreateRecipeStatusBadge(recipe));
        card.IconHost.AddChild(CreateRecipeIconContent(recipe, new Vector2(48, 48)));

        ApplyRecipeEntryVisuals(card.Button, card.IconHost, recipe, selected);
        _recipeEntryControlsByBranch[branch][recipe.ID] = new RecipeEntryControls(card.Button, card.IconHost);

        return card;
    }

    private PanelContainer CreateRecipeDetailsPanel(
        PersistentCraftRecipePrototype recipe,
        PersistentCraftBranchState branchState)
    {
        var loaded = _state?.Loaded == true;
        var requirementMet = _state != null && HasRequirement(_state, recipe);
        var hasMaterials = GetHasLocalMaterials(recipe);
        var canCraft = loaded && requirementMet && hasMaterials;
        var accent = GetAccent(recipe.Branch);

        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = PersistentCraftUiTheme.SurfacePanel,
                BorderColor = accent.WithAlpha(0.5f),
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 18,
                ContentMarginRightOverride = 18,
                ContentMarginTopOverride = 18,
                ContentMarginBottomOverride = 18,
            }
        };

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        body.AddChild(CreateRecipeDetailHeader(recipe, branchState, canCraft, accent));
        body.AddChild(new Control { MinSize = new Vector2(1, 12) });

        body.AddChild(CreateSection(
            Loc.GetString("persistent-craft-description-label"),
            $"[color={DescriptionText.ToHex()}]{FormattedMessage.EscapeText(ResolveRecipeDescription(recipe))}[/color]",
            10));
        body.AddChild(new Control { MinSize = new Vector2(1, 10) });

        body.AddChild(CreateSection(
            Loc.GetString("persistent-craft-recipe-ingredients"),
            BuildIngredientMarkup(recipe),
            10));
        body.AddChild(new Control { VerticalExpand = true });
        panel.AddChild(body);

        return panel;
    }

    private Control CreateSection(string title, string markupText, int padding)
    {
        var section = new PersistentCraftTextSection();
        section.SetData(title, markupText, CardMutedBorder, padding);
        return section;
    }

    private Control CreateRecipeDetailHeader(
        PersistentCraftRecipePrototype recipe,
        PersistentCraftBranchState branchState,
        bool canCraft,
        Color accent)
    {
        var header = new PersistentCraftRecipeDetailHeader();
        header.SetData(
            ResolveRecipeName(recipe),
            Color.White,
            Loc.GetString("persistent-craft-recipe-action"),
            !canCraft);
        header.ActionButton.OnPressed += _ => OnCraftPressed?.Invoke(recipe.ID);

        header.ActionButton.StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = canCraft ? accent.WithAlpha(0.18f) : PersistentCraftUiTheme.SurfacePanelSoft,
            BorderColor = canCraft ? accent.WithAlpha(0.80f) : PersistentCraftUiTheme.BorderSoft,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 12,
            ContentMarginRightOverride = 12,
            ContentMarginTopOverride = 8,
            ContentMarginBottomOverride = 8,
        };
        header.ActionButton.ModulateSelfOverride = canCraft ? accent : PersistentCraftUiTheme.TextMuted;

        header.InfoText.SetMessage(FormattedMessage.FromMarkupPermissive(BuildHeaderInfoMarkup(recipe)));
        header.IconHost.RectClipContent = true;
        header.IconHost.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = IconBackground,
            BorderColor = accent.WithAlpha(0.60f),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 6,
            ContentMarginRightOverride = 6,
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 6,
        };
        header.IconHost.AddChild(CreateRecipeIconContent(recipe, new Vector2(116, 116)));
        var progressOverlay = new PersistentCraftRecipeIconProgressOverlay
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        header.IconHost.AddChild(progressOverlay);
        BindActiveRecipeIconOverlay(recipe.ID, accent, progressOverlay);

        header.MetaContainer.AddChild(CreateMetaBadge(
            $"{Loc.GetString("persistent-craft-branch-points-label")}: {branchState.AvailablePoints} | {Loc.GetString("persistent-craft-spent-points-label")}: {branchState.SpentPoints}",
            PersistentCraftUiTheme.SurfacePanelAlt,
            PersistentCraftUiTheme.TextPrimary,
            PersistentCraftUiTheme.BorderSoft));
        header.MetaContainer.AddChild(new Control { MinSize = new Vector2(1, 4) });
        header.MetaContainer.AddChild(CreateMetaBadge(
            $"{Loc.GetString("persistent-craft-level-label")} {PersistentCraftingHelper.GetTierDisplayLabel(recipe.Tier)}",
            PersistentCraftUiTheme.SurfacePanelAlt,
            accent,
            accent.WithAlpha(0.35f)));
        header.MetaContainer.AddChild(new Control { MinSize = new Vector2(1, 4) });
        header.MetaContainer.AddChild(CreateMetaBadge(
            GetRecipeCategoryPath(recipe),
            PersistentCraftUiTheme.SurfacePanelAlt,
            PersistentCraftUiTheme.TextMuted,
            PersistentCraftUiTheme.BorderSoft));
        header.MetaContainer.AddChild(new Control { MinSize = new Vector2(1, 4) });
        header.MetaContainer.AddChild(CreateMetaBadge(
            $"{Loc.GetString("persistent-craft-recipe-points")}: +{PersistentCraftingHelper.GetPointReward(recipe)}",
            PersistentCraftUiTheme.SurfacePanelAlt,
            accent,
            accent.WithAlpha(0.35f)));

        return header;
    }

    private PanelContainer CreateRecipeIcon(PersistentCraftRecipePrototype recipe, Color accent, Vector2 size)
    {
        var panel = new PanelContainer
        {
            MinSize = size,
            RectClipContent = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = IconBackground,
                BorderColor = accent,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 6,
            },
        };

        panel.AddChild(CreateRecipeIconContent(recipe, size));

        return panel;
    }

    private Control CreateRecipeIconContent(PersistentCraftRecipePrototype recipe, Vector2 size)
    {
        if (TryGetRecipeTexture(recipe, out var texture))
        {
            return new TextureRect
            {
                Texture = texture,
                TextureScale = size.X >= RecipeIconLargeThreshold
                    ? new Vector2(RecipeIconScaleLarge, RecipeIconScaleLarge)
                    : new Vector2(RecipeIconScaleSmall, RecipeIconScaleSmall),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
            };
        }

        return new Label
        {
            Text = PersistentCraftingHelper.GetTierDisplayLabel(recipe.Tier),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };
    }

    private PanelContainer CreateMetaBadge(string text, Color background, Color foreground, Color border)
    {
        var badge = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = background,
                BorderColor = border,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 10,
                ContentMarginRightOverride = 10,
                ContentMarginTopOverride = 5,
                ContentMarginBottomOverride = 5,
            }
        };

        badge.AddChild(new Label
        {
            Text = text,
            FontColorOverride = foreground,
            ClipText = true,
        });

        return badge;
    }
}
