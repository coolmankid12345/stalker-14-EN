using Content.Shared.ActionBlocker;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker_EN.QuickEquipBolt;

/// <summary>
/// Handles the quick equip bolt keybind. Searches the player's inventory (including nested storage)
/// for an entity with the STBolt tag and picks it up into an empty hand.
/// </summary>
public sealed class STQuickEquipBoltSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private static readonly ProtoId<TagPrototype> BoltTag = "STBolt";

    /// <summary>
    /// Defensive guard against pathological nesting; circular refs are impossible in the engine
    /// but this bounds worst-case iteration.
    /// </summary>
    private const int MaxSearchDepth = 4;

    /// <summary>
    /// Belt is first because bolt bags are typically stored there.
    /// </summary>
    private static readonly string[] SlotPriority = { "belt", "pocket1", "pocket2", "back", "suitstorage", "id" };

    private static readonly HashSet<string> PrioritySlotSet = new(SlotPriority);

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.STQuickEquipBolt,
                InputCmdHandler.FromDelegate(HandleQuickEquipBolt, handle: false, outsidePrediction: false))
            .Register<STQuickEquipBoltSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<STQuickEquipBoltSystem>();
    }

    private void HandleQuickEquipBolt(ICommonSession? session)
    {
        if (session?.AttachedEntity is not { Valid: true } uid || !Exists(uid))
            return;

        if (!TryComp<HandsComponent>(uid, out var hands) || hands.ActiveHandId == null)
            return;

        if (!_actionBlocker.CanInteract(uid, null))
            return;

        if (!_hands.TryGetEmptyHand((uid, hands), out _))
        {
            _popup.PopupClient(Loc.GetString("st-quick-equip-bolt-hands-full"), uid, uid);
            return;
        }

        if (!TryFindBolt(uid, out var boltUid))
        {
            _popup.PopupClient(Loc.GetString("st-quick-equip-bolt-none-found"), uid, uid);
            return;
        }

        _hands.TryPickupAnyHand(uid, boltUid.Value, checkActionBlocker: false, handsComp: hands);
    }

    /// <summary>
    /// Searches the player's inventory slots and their nested containers for an entity with the STBolt tag.
    /// Priority slots are checked first, then remaining slots.
    /// </summary>
    private bool TryFindBolt(EntityUid uid, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out EntityUid? found)
    {
        found = null;

        if (!_inventory.TryGetSlots(uid, out var slotDefs))
            return false;

        foreach (var slotName in SlotPriority)
        {
            if (!_inventory.TryGetSlotEntity(uid, slotName, out var slotEntity))
                continue;

            if (TryFindBoltInEntity(slotEntity.Value, out found, 0))
                return true;
        }

        foreach (var slotDef in slotDefs)
        {
            if (PrioritySlotSet.Contains(slotDef.Name))
                continue;

            if (!_inventory.TryGetSlotEntity(uid, slotDef.Name, out var slotEntity))
                continue;

            if (TryFindBoltInEntity(slotEntity.Value, out found, 0))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the entity itself has the STBolt tag, or recursively searches its containers.
    /// </summary>
    private bool TryFindBoltInEntity(EntityUid entity, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out EntityUid? found, int depth)
    {
        found = null;

        if (_tag.HasTag(entity, BoltTag))
        {
            found = entity;
            return true;
        }

        if (depth >= MaxSearchDepth)
            return false;

        if (!TryComp<ContainerManagerComponent>(entity, out var containerManager))
            return false;

        foreach (var container in _container.GetAllContainers(entity, containerManager))
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (TryFindBoltInEntity(contained, out found, depth + 1))
                    return true;
            }
        }

        return false;
    }
}
