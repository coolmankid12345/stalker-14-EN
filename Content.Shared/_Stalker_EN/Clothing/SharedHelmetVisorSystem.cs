using Content.Shared._Stalker_EN.Clothing.Components;
using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Stalker_EN.Clothing;

public abstract class SharedHelmetVisorSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly SharedActionsSystem Actions = default!;
    [Dependency] protected readonly InventorySystem InventorySystem = default!;
    [Dependency] protected readonly ClothingSystem Clothing = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;
    [Dependency] protected readonly SharedAppearanceSystem Appearance = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HelmetVisorComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<HelmetVisorComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<HelmetVisorComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<HelmetVisorComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<HelmetVisorComponent, ToggleHelmetVisorEvent>(OnToggle);
    }

    protected virtual void OnInit(EntityUid uid, HelmetVisorComponent comp, ComponentInit args)
    {
        UpdateBlockers(uid, comp);

        // Don't modify component data - DefaultReflectProb should be set in prototype or left as default
        // The value is used at runtime from ReflectComponent directly
    }
    private void OnToggle(EntityUid uid, HelmetVisorComponent comp, ToggleHelmetVisorEvent args)
    {
        if (args.Handled || !comp.IsToggleable)
            return;

        if (!CanLowerVisor(uid, comp))
            return;

        if (Timing.CurTime.TotalSeconds - comp.LastToggleTime < comp.ToggleDelay)
            return;

        comp.LastToggleTime = (float)Timing.CurTime.TotalSeconds;
        Audio.PlayPredicted(comp.IsUp ? comp.SoundVisorDown : comp.SoundVisorUp, uid, args.Performer);
        SetUp(uid, comp, !comp.IsUp);
        args.Handled = true;
    }
    protected virtual void OnGetActions(EntityUid uid, HelmetVisorComponent comp, GetItemActionsEvent args)
    {
        if (!comp.IsToggleable)
            return;

        if (args.SlotFlags == null || (args.SlotFlags.Value & SlotFlags.HEAD) == 0)
            return;

        args.AddAction(ref comp.ToggleActionEntity, comp.ToggleAction);
        Dirty(uid, comp);
    }

    protected virtual void OnExamine(EntityUid uid, HelmetVisorComponent comp, ExaminedEvent args)
    {
        if (!comp.IsToggleable || !args.IsInDetailsRange)
            return;

        var key = comp.IsUp ? "helmet-visor-up" : "helmet-visor-down";
        args.PushMarkup(Loc.GetString(key));
    }

    protected virtual void OnGetVerbs(EntityUid uid, HelmetVisorComponent comp, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !comp.IsToggleable)
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString(comp.IsUp ? "helmet-visor-lower" : "helmet-visor-raise"),
            Act = () => ToggleVisor(uid, comp, args.User)
        });
    }

    protected virtual void ToggleVisor(EntityUid uid, HelmetVisorComponent comp, EntityUid user)
    {
        if (!comp.IsToggleable)
            return;

        if (!CanLowerVisor(uid, comp))
            return;

        if (Timing.CurTime.TotalSeconds - comp.LastToggleTime < comp.ToggleDelay)
            return;

        comp.LastToggleTime = (float)Timing.CurTime.TotalSeconds;
        Audio.PlayPredicted(comp.IsUp ? comp.SoundVisorDown : comp.SoundVisorUp, uid, user);
        SetUp(uid, comp, !comp.IsUp);
    }

    public void SetUp(EntityUid uid, HelmetVisorComponent comp, bool up, bool force = false)
    {
        if (Timing.ApplyingState)
            return;

        if (!force && !comp.IsToggleable)
            return;

        if (comp.IsUp == up)
            return;

        comp.IsUp = up;

        if (TryComp<SlotBlockOverrideComponent>(uid, out var over))
        {
            over.Overridden = comp.IsUp;
            Dirty(uid, over);
        }

        if (comp.ToggleActionEntity is { } action)
            Actions.SetToggled(action, comp.IsUp);

        UpdateVisuals(uid, comp);
        RaiseLocalEvent(uid, new VisorToggledEvent(uid, comp.IsUp));
        Dirty(uid, comp);
    }

    protected virtual void UpdateVisuals(EntityUid uid, HelmetVisorComponent comp)
    {
        if (comp.EquippedPrefixUp != null)
        {
            var prefix = comp.IsUp ? comp.EquippedPrefixUp : null;
            Clothing.SetEquippedPrefix(uid, prefix);
        }

        Appearance.SetData(uid, HelmetVisorVisuals.IsUp, comp.IsUp);
        RaiseLocalEvent(uid, new HelmetVisorVisualsChangedEvent());
    }

    protected virtual void UpdateBlockers(EntityUid uid, HelmetVisorComponent comp)
    {
        var block = !comp.IsUp;
        RaiseLocalEvent(uid, new VisorBlockersChangedEvent(block, block));
    }

    protected bool CanLowerVisor(EntityUid uid, HelmetVisorComponent comp)
    {
        if (!comp.IsUp)
            return true;

        if (InventorySystem.TryGetSlotEntity(Transform(uid).ParentUid, "mask", out var maskItem))
            if (_tagSystem.HasTag(maskItem.Value, "BlockMask"))
                return false;
        return true;
    }
}

public enum HelmetVisorVisuals : byte
{
    IsUp
}
public readonly record struct VisorToggledEvent(EntityUid Visor, bool IsUp);
public readonly record struct VisorBlockersChangedEvent(bool BlockIngestion, bool BlockIdentity);
public readonly record struct HelmetVisorVisualsChangedEvent;
