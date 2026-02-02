using Content.Server.Damage.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Content.Shared.Inventory;
using Content.Shared.Tag;

namespace Content.Server._Stalker.PersonalDamage;

public sealed class PersonalDamageSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<PersonalDamageComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.NextDamage > _timing.CurTime)
                continue;

            
            if (!IsArtifactAllowed(uid))
                continue;

            var parent = uid;
            while (!HasComp<MapComponent>(parent))
            {
                if (TerminatingOrDeleted(parent))
                    break;

                if (HasComp<PersonalDamageBlockComponent>(parent))
                    break;

                _damageableSystem.TryChangeDamage(parent, component.Damage, component.IgnoreResistances, component.InterruptsDoAfters);
                _stamina.TakeStaminaDamage(parent, component.StaminaDamage);
                parent = Transform(parent).ParentUid;
            }

            component.NextDamage = _timing.CurTime + TimeSpan.FromSeconds(component.Interval);
        }
    }

    private bool IsArtifactAllowed(EntityUid uid)
    {

        if (!TryComp<TagComponent>(uid, out var tagComp) || !_tag.HasTag(tagComp, "STArtifact"))
            return true;


        if (!TryComp<TransformComponent>(uid, out var xform) || !TryComp<MetaDataComponent>(uid, out var meta))
            return false;

        if (!_inventory.TryGetContainingSlot((uid, xform, meta), out var slotDef) || slotDef == null)
            return false;

        var name = slotDef.Name;
        return name == "artifact1" || name == "artifact2" || name == "artifact3" || name == "artifact4";
    }
}
