using Content.Server._Stalker_EN.Anomaly.Effects.Components;
using Content.Shared._Stalker.Anomaly.Triggers.Events;
using Content.Shared._Stalker.Weight;
using Robust.Server.Audio;
using Robust.Shared.Physics;

namespace Content.Server._Stalker_EN.Anomaly.Effects.Systems;

/// <summary>
/// Plays different sounds based on damage type and target weight.
/// Weight bonus sound has highest priority and replaces other sounds.
/// Only plays when damage is actually being dealt (StateActiveBase or StateActiveDouble).
/// </summary>
public sealed class STAnomalyEffectDamageSoundConditionalSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAnomalyEffectDamageSoundConditionalComponent, STAnomalyTriggerEvent>(OnTriggered);
    }

    private void OnTriggered(
        Entity<STAnomalyEffectDamageSoundConditionalComponent> effect,
        ref STAnomalyTriggerEvent args)
    {
        var comp = effect.Comp;
        var coords = Transform(effect).Coordinates;

        // Always play passthrough sound when walking through
        if (comp.PassthroughSound != null)
            _audio.PlayPredicted(comp.PassthroughSound, coords, effect);

        // Check if this is a damage-dealing trigger (must have one of the damage groups)
        var isDouble = args.Groups.Contains(comp.DoubleDamageGroup);
        var isBase = args.Groups.Contains(comp.BaseDamageGroup);

        // No damage groups present - don't play any hit sound
        if (!isDouble && !isBase)
            return;

        // Find entities in range that might be damaged
        var entities = _entityLookup.GetEntitiesInRange<STWeightComponent>(coords, comp.Range, LookupFlags.Uncontained);

        // Check if any entity is heavy enough for weight bonus sound
        foreach (var entity in entities)
        {
            if (entity.Comp.Total >= comp.WeightThreshold && comp.WeightBonusSound != null)
            {
                _audio.PlayPredicted(comp.WeightBonusSound, coords, effect);
                return;
            }
        }

        // No heavy entity - play sound based on damage type
        var sound = isDouble ? comp.DoubleDamageSound : comp.BaseDamageSound;

        if (sound != null)
            _audio.PlayPredicted(sound, coords, effect);
    }
}
