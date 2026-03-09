using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface;

/// <summary>
/// Convention-based theme resolution for SpriteSpecifier icons.
/// Looks for themed variants of alert/action icons under the current theme's texture path.
/// </summary>
public static class ThemedSpriteResolver
{
    /// <summary>
    /// Known path segments that identify themeable icon directories.
    /// When one of these segments is found in a sprite path, the portion from that segment
    /// onward is appended to the theme path to look for a themed variant.
    /// </summary>
    private static readonly string[] ThemeableSegments = { "/Alerts/", "/Actions/" };

    /// <summary>
    /// Attempts to resolve a themed variant of the given SpriteSpecifier.
    /// If the current theme has a matching icon, returns a new SpriteSpecifier pointing to it.
    /// Otherwise, returns the original unchanged.
    /// </summary>
    public static SpriteSpecifier Resolve(
        SpriteSpecifier original,
        ResPath themePath,
        IResourceManager resourceManager)
    {
        switch (original)
        {
            case SpriteSpecifier.Rsi rsi:
            {
                var themedPath = TryResolveThemedPath(rsi.RsiPath, themePath, resourceManager, true);
                if (themedPath != null)
                    return new SpriteSpecifier.Rsi(themedPath.Value, rsi.RsiState);
                return original;
            }
            case SpriteSpecifier.Texture texture:
            {
                var themedPath = TryResolveThemedPath(texture.TexturePath, themePath, resourceManager, false);
                if (themedPath != null)
                    return new SpriteSpecifier.Texture(themedPath.Value);
                return original;
            }
            default:
                return original;
        }
    }

    private static ResPath? TryResolveThemedPath(
        ResPath originalPath,
        ResPath themePath,
        IResourceManager resourceManager,
        bool isRsi)
    {
        var pathStr = originalPath.ToString();

        foreach (var segment in ThemeableSegments)
        {
            var segmentIndex = pathStr.IndexOf(segment);
            if (segmentIndex < 0)
                continue;

            // Extract from the segment onward, e.g. "Alerts/hunger.rsi"
            var relative = pathStr[(segmentIndex + 1)..];
            var themedPath = themePath / new ResPath(relative);

            if (isRsi)
            {
                // RSI directories contain meta.json
                if (resourceManager.ContentFileExists(themedPath / "meta.json"))
                    return themedPath;
            }
            else
            {
                if (resourceManager.ContentFileExists(themedPath))
                    return themedPath;
            }
        }

        return null;
    }
}
