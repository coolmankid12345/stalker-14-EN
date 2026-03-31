using Content.Shared._Stalker.Weight;
using Content.Shared._Stalker_EN.Weight;
using Content.Shared._Stalker_EN.Weight.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker_EN.EntityEffects.Effects;

/// <summary>
/// Entity effect that applies temporary weight reduction via status effects.
/// Used by STHercules reagent to reduce entity weight by 20kg while metabolizing.
/// Works with STWeightModStatusSystem to create/extend status effects.
/// </summary>
public sealed partial class STWeightModifierEntityEffectSystem : EntityEffectSystem<STWeightComponent, STWeightModifier>
{
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly STWeightModStatusSystem _weightModStatus = default!;

    // stalker-en-changes - applies weight modifier via status effect system
    protected override void Effect(Entity<STWeightComponent> entity, ref EntityEffectEvent<STWeightModifier> args)
    {
        var proto = args.Effect.EffectProto;
        var weightMod = args.Effect.WeightModifier;
        var time = args.Effect.Time ?? TimeSpan.FromSeconds(2);

        // Use status effect system to apply modifier once and hold it while reagent present
        _weightModStatus.TryUpdateWeightModDuration(
            entity,
            proto,
            time * args.Scale,
            weightMod);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class STWeightModifier : BaseStatusEntityEffect<STWeightModifier>
{
    /// <summary>
    /// Weight modifier value applied to entity Self weight.
    /// Negative values reduce weight (e.g., -20 for Hercules reagent).
    /// </summary>
    [DataField(required: true)]
    public float WeightModifier = 0f;

    /// <summary>
    /// Status effect prototype used for weight modification.
    /// Default "ReagentWeight" is defined in Resources/Prototypes/_Stalker/status_effects.yml
    /// </summary>
    [DataField]
    public EntProtoId EffectProto = "ReagentWeight";

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
    Time == null
        ? null
        : Loc.GetString("entity-effect-guidebook-st-weight-modifier",
            ("chance", Probability),
            ("modifier", WeightModifier),
            ("time", Time.Value.TotalSeconds));
}
