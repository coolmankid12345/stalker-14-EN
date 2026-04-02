using Content.Shared._ES.Viewcone;
using Content.Shared._Stalker_EN.CharacterRank;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Stalker_EN.CharacterRank;

/// <summary>
/// Displays the character's rank icon as a status icon below the band icon.
/// </summary>
public sealed class STCharacterRankSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STCharacterRankComponent, GetStatusIconsEvent>(OnGetStatusIcon);
    }

    private void OnGetStatusIcon(EntityUid uid, STCharacterRankComponent comp, ref GetStatusIconsEvent args)
    {
        if (_appearance.TryGetData<TypingIndicatorState>(uid, TypingIndicatorVisuals.State, out var typingState)
            && typingState != TypingIndicatorState.None)
            return;

        if (TryComp<ESViewconeOccludableComponent>(uid, out var occ) && occ.IsHidden)
            return;

        if (!comp.Enabled)
            return;

        if (_proto.TryIndex(comp.RankIconId, out var icon))
            args.StatusIcons.Add(icon);
    }
}
