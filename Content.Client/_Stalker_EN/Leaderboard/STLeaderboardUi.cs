using Content.Client.UserInterface.Fragments;
using Content.Shared._Stalker_EN.Leaderboard;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;

namespace Content.Client._Stalker_EN.Leaderboard;

public sealed partial class STLeaderboardUi : UIFragment
{
    private STLeaderboardUiFragment? _fragment;

    public override Control GetUIFragmentRoot() => _fragment!;

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new STLeaderboardUiFragment();
        _fragment.SetBoundUserInterface(userInterface);
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not STLeaderboardUiState dirState)
            return;

        _fragment?.UpdateState(dirState.Entries);
    }
}
