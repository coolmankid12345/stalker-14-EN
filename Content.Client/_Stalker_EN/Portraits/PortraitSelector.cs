using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared._Stalker_EN.Portraits;
using Robust.Client.Graphics;
using Robust.Client.Localization;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Log;
using Robust.Shared.Utility;

namespace Content.Client._Stalker_EN.Portraits;

/// <summary>
/// Grid-based portrait selector for the character profile editor.
/// Displays available portrait textures and allows player selection.
/// </summary>
public sealed class PortraitSelector : BoxContainer
{
    private readonly IResourceCache _resCache;
    private readonly IRobustRandom _random;
    private readonly GridContainer _grid;
    private readonly TextureRect _previewRect;
    private readonly Label _previewName;
    private readonly Label _previewDesc;
    private readonly Dictionary<string, Button> _buttons = new();
    private readonly ISawmill _sawmill;

    /// <summary>
    /// Event raised when a portrait texture is selected.
    /// Passes the texture path of the selected portrait.
    /// </summary>
    public event Action<string>? OnPortraitSelected;

    /// <summary>
    /// Initializes a new instance of the PortraitSelector.
    /// </summary>
    public PortraitSelector()
    {
        _resCache = IoCManager.Resolve<IResourceCache>();
        _random = IoCManager.Resolve<IRobustRandom>();
        _sawmill = Logger.GetSawmill("st.portrait.selector");

        Orientation = LayoutOrientation.Horizontal;
        HorizontalExpand = true;
        VerticalExpand = true;

        var scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true };
        _grid = new GridContainer { Columns = 4, HorizontalExpand = true };
        scroll.AddChild(_grid);
        AddChild(scroll);

        var preview = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            MinSize = new Vector2(150, 0),
        };
        preview.AddChild(new Label { Text = Loc.GetString("st-portrait-preview-label"), StyleClasses = { "labelHeading" }, HorizontalAlignment = HAlignment.Center });
        preview.AddChild(new Control { MinSize = new Vector2(0, 5) });

        var texPanel = new PanelContainer { HorizontalAlignment = HAlignment.Center };
        texPanel.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#1B1B1E") };
        _previewRect = new TextureRect { Stretch = TextureRect.StretchMode.KeepCentered, MinSize = new Vector2(128, 128) };
        texPanel.AddChild(_previewRect);
        preview.AddChild(texPanel);

        preview.AddChild(new Control { MinSize = new Vector2(0, 5) });
        _previewName = new Label { HorizontalAlignment = HAlignment.Center };
        preview.AddChild(_previewName);
        _previewDesc = new Label { HorizontalAlignment = HAlignment.Center, FontColorOverride = Color.FromHex("#888888") };
        preview.AddChild(_previewDesc);

        AddChild(preview);
    }

    /// <summary>
    /// Sets up the portrait selector with the given portraits and selected texture path.
    /// </summary>
    /// <param name="portraits">List of portrait prototypes to display.</param>
    /// <param name="selectedId">Texture path of the currently selected portrait.</param>
    /// <param name="protoMan">Prototype manager for localization.</param>
    public void Setup(List<CharacterPortraitPrototype> portraits, string? selectedId, IPrototypeManager protoMan)
    {
        _grid.RemoveAllChildren();
        _buttons.Clear();

        // Validate input
        if (portraits == null || portraits.Count == 0)
        {
            _previewName.Text = Loc.GetString("st-portrait-no-portraits-available");
            _previewDesc.Text = string.Empty;
            _previewRect.Texture = null;
            return;
        }

        // Flatten portraits: each texture becomes a button
        var textureEntries = new List<(CharacterPortraitPrototype Proto, SpriteSpecifier TextureSpecifier, int Index)>();
        foreach (var proto in portraits)
        {
            if (proto?.Textures == null || proto.Textures.Count == 0)
            {
                _sawmill.Warning($"Portrait prototype {proto?.ID ?? "unknown"} has no textures");
                continue;
            }

            for (var i = 0; i < proto.Textures.Count; i++)
                textureEntries.Add((proto, proto.Textures[i], i));
        }

        if (textureEntries.Count == 0)
        {
            _previewName.Text = Loc.GetString("st-portrait-no-textures-available");
            _previewDesc.Text = string.Empty;
            _previewRect.Texture = null;
            return;
        }

        // Fallback: auto-select random if none selected
        // Note: We don't invoke OnPortraitSelected here to avoid overwriting user's profile choice
        var selectedTexturePath = textureEntries.FirstOrDefault(t => t.TextureSpecifier is SpriteSpecifier.Texture tex && tex.TexturePath.ToString() == selectedId);
        if ((string.IsNullOrEmpty(selectedId) || selectedTexturePath.TextureSpecifier == null)
            && textureEntries.Count > 0)
        {
            var randomEntry = textureEntries[_random.Next(textureEntries.Count)];
            selectedId = randomEntry.TextureSpecifier switch
            {
                SpriteSpecifier.Texture tex => tex.TexturePath.ToString(),
                SpriteSpecifier.Rsi rsi => rsi.RsiPath.ToString(),
                _ => string.Empty
            };
        }

        foreach (var entry in textureEntries)
        {
            var btn = CreateButton(entry.Proto, entry.TextureSpecifier, entry.Index);
            var texturePath = entry.TextureSpecifier switch
            {
                SpriteSpecifier.Texture tex => tex.TexturePath.ToString(),
                SpriteSpecifier.Rsi rsi => rsi.RsiPath.ToString(),
                _ => string.Empty
            };
            btn.Pressed = texturePath == selectedId;
            if (texturePath == selectedId)
                UpdatePreview(entry.Proto, entry.TextureSpecifier);

            _buttons[texturePath] = btn;
            _grid.AddChild(btn);
        }

        if (string.IsNullOrEmpty(selectedId))
        {
            _previewName.Text = Loc.GetString("st-portrait-no-selection-name");
            _previewDesc.Text = Loc.GetString("st-portrait-no-selection-desc");
            _previewRect.Texture = null;
        }
    }

    /// <summary>
    /// Creates a button for a portrait texture.
    /// </summary>
    /// <param name="proto">The portrait prototype.</param>
    /// <param name="textureSpecifier">The texture SpriteSpecifier.</param>
    /// <param name="index">The texture index in the prototype.</param>
    /// <returns>A button configured for the portrait.</returns>
    private Button CreateButton(CharacterPortraitPrototype proto, SpriteSpecifier textureSpecifier, int index)
    {
        var btn = new Button { MinSize = new Vector2(64, 64), ToggleMode = true };

        var texRect = new TextureRect { Stretch = TextureRect.StretchMode.KeepCentered, HorizontalExpand = true, VerticalExpand = true };
        btn.AddChild(texRect);

        var texturePath = textureSpecifier switch
        {
            SpriteSpecifier.Texture tex => tex.TexturePath.ToString(),
            SpriteSpecifier.Rsi rsi => rsi.RsiPath.ToString(),
            _ => string.Empty
        };
        if (!string.IsNullOrEmpty(texturePath) && _resCache.TryGetResource<TextureResource>(texturePath, out var texture))
            texRect.Texture = texture;
        else
            _sawmill.Warning($"Portrait texture not found: {texturePath}");

        btn.OnToggled += _ =>
        {
            if (btn.Pressed)
            {
                foreach (var kvp in _buttons)
                    kvp.Value.Pressed = false;
                btn.Pressed = true;
                UpdatePreview(proto, textureSpecifier);
                OnPortraitSelected?.Invoke(texturePath);
            }
        };

        return btn;
    }

    /// <summary>
    /// Updates the preview panel with the selected portrait.
    /// </summary>
    /// <param name="proto">The portrait prototype.</param>
    /// <param name="textureSpecifier">The texture SpriteSpecifier to display.</param>
    private void UpdatePreview(CharacterPortraitPrototype proto, SpriteSpecifier textureSpecifier)
    {
        var textureIndex = proto.Textures.IndexOf(textureSpecifier);
        _previewName.Text = proto.Textures.Count > 1
            ? Loc.GetString("st-portrait-preview-multi", ("name", Loc.GetString(proto.Name)), ("current", textureIndex + 1), ("total", proto.Textures.Count))
            : Loc.GetString(proto.Name);
        _previewDesc.Text = proto.Description != null ? Loc.GetString(proto.Description) : string.Empty;

        var texturePath = textureSpecifier switch
        {
            SpriteSpecifier.Texture tex => tex.TexturePath.ToString(),
            SpriteSpecifier.Rsi rsi => rsi.RsiPath.ToString(),
            _ => string.Empty
        };
        if (!string.IsNullOrEmpty(texturePath) && _resCache.TryGetResource<TextureResource>(texturePath, out var texture))
            _previewRect.Texture = texture;
        else
        {
            _sawmill.Warning($"Portrait texture not found: {texturePath}");
            _previewRect.Texture = null;
        }
    }

    /// <summary>
    /// Disposes of the portrait selector and clears button references.
    /// </summary>
    /// <param name="disposing">Whether managed resources should be disposed.</param>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _buttons.Clear();
    }
}
