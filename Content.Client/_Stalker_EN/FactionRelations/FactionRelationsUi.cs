using Content.Client.UserInterface.Fragments;
using Content.Shared._Stalker_EN.FactionRelations;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;

namespace Content.Client._Stalker_EN.FactionRelations;

/// <summary>
/// UIFragment for the faction relations PDA cartridge program.
/// </summary>
public sealed partial class FactionRelationsUi : UIFragment
{
    private FactionRelationsUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new FactionRelationsUiFragment();
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not STFactionRelationsUiState relationsState)
            return;

        _fragment?.UpdateState(relationsState);
    }
}
