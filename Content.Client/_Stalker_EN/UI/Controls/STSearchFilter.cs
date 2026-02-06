using System;

namespace Content.Client._Stalker_EN.UI.Controls;

/// <summary>
/// Reusable search filter logic for item-listing UIs (shops, stash, etc.).
/// </summary>
public static class STSearchFilter
{
    /// <summary>
    /// Returns true when <paramref name="name"/> contains the search term (case-insensitive).
    /// An empty or null filter matches everything.
    /// </summary>
    public static bool Matches(string? filter, string name)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        return name.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true when <paramref name="name"/> or <paramref name="description"/> contains
    /// the search term (case-insensitive). An empty or null filter matches everything.
    /// </summary>
    public static bool Matches(string? filter, string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        var trimmed = filter.Trim();
        return name.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
               || (!string.IsNullOrEmpty(description)
                   && description.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
