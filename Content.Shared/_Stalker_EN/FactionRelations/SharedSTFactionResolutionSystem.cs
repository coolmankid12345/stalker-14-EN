using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker_EN.FactionRelations;

/// <summary>
/// Shared system providing O(1) faction resolution lookups cached from the
/// <see cref="STFactionRelationDefaultsPrototype"/>.
/// Both client and server use this to resolve band names to faction names
/// and faction aliases to their primaries without repeated prototype indexing.
/// </summary>
public sealed class SharedSTFactionResolutionSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    /// <summary>
    /// Single source of truth for the defaults prototype ID.
    /// </summary>
    public static readonly ProtoId<STFactionRelationDefaultsPrototype> DefaultsProtoId = "Default";

    /// <summary>
    /// Maps alias factions to their primary. E.g. "Rookies" → "Loners".
    /// </summary>
    private Dictionary<string, string> _aliasToPrimary = new();

    /// <summary>
    /// Maps band prototype names to faction relation names. E.g. "Dolg" → "Duty".
    /// </summary>
    private Dictionary<string, string> _bandToFaction = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        RebuildCache();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<STFactionRelationDefaultsPrototype>())
            RebuildCache();
    }

    private void RebuildCache()
    {
        _aliasToPrimary = new Dictionary<string, string>();
        _bandToFaction = new Dictionary<string, string>();

        if (!_protoManager.TryIndex(DefaultsProtoId, out var proto))
            return;

        foreach (var (primary, aliases) in proto.FactionGroups)
        {
            foreach (var alias in aliases)
            {
                _aliasToPrimary[alias] = primary;
            }
        }

        foreach (var (bandName, factionName) in proto.BandMapping)
        {
            _bandToFaction[bandName] = factionName;
        }
    }

    /// <summary>
    /// Resolves a faction alias to its primary. Returns the input if not an alias.
    /// O(1) dictionary lookup.
    /// </summary>
    public string ResolvePrimary(string faction)
    {
        return _aliasToPrimary.GetValueOrDefault(faction, faction);
    }

    /// <summary>
    /// Resolves a band prototype name (e.g. "Dolg") to a faction relation name (e.g. "Duty").
    /// O(1) dictionary lookup. Returns null if no mapping exists.
    /// </summary>
    public string? GetBandFactionName(string bandName)
    {
        return _bandToFaction.GetValueOrDefault(bandName);
    }
}
