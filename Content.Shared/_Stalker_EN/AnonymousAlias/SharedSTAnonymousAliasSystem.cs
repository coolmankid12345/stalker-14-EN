using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Content.Shared.Dataset;
using Content.Shared.Decals;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker_EN.AnonymousAlias;

/// <summary>
/// Shared base for the anonymous alias system. Provides access control for
/// <see cref="STAnonymousAliasComponent"/> and shared utilities.
/// </summary>
public abstract class SharedSTAnonymousAliasSystem : EntitySystem
{
    public static readonly ProtoId<LocalizedDatasetPrototype> AdjDatasetId = "STAliasAdjectives";
    public static readonly ProtoId<LocalizedDatasetPrototype> NounDatasetId = "STAliasNouns";
    public static readonly ProtoId<ColorPalettePrototype> ColorPaletteId = "STAliasColors";

    private EntityQuery<STAnonymousAliasComponent> _aliasQuery;

    public override void Initialize()
    {
        base.Initialize();
        _aliasQuery = GetEntityQuery<STAnonymousAliasComponent>();
    }

    /// <summary>
    ///     Tries to get the full alias of the entity, with <see cref="STAnonymousAliasComponent.TransformLoc"/>
    ///         used if necessary.
    /// </summary>
    /// <returns>Whether the entity had a <see cref="STAnonymousAliasComponent"/> and the full alias wasn't empty.</returns>
    public bool TryGetFullAlias(Entity<STAnonymousAliasComponent?> entity, [NotNullWhen(true)] out string? alias)
    {
        if (!_aliasQuery.Resolve(ref entity, logMissing: false))
        {
            alias = null;
            return false;
        }

        alias = GetFullAlias(entity.Comp!);
        if (alias == string.Empty)
            return false;

        return true;
    }

    /// <returns>The full alias of the entity, with prefix prepended.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetFullAlias(STAnonymousAliasComponent component) =>
    // if no transformloc just return alias otherwise use it
        component.TransformLoc == null ?
            component.Alias :
            Loc.GetString(component.TransformLoc, ("alias", component.Alias));
}
