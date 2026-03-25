using Content.Client.CartridgeLoader;
using Content.Shared._Stalker_EN.PDA;
using Content.Shared._Stalker_EN.PDA.Ringer;
using Content.Shared.CartridgeLoader;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.PDA;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Player;

namespace Content.Client.PDA
{
    [UsedImplicitly]
    public sealed class PdaBoundUserInterface : CartridgeLoaderBoundUserInterface
    {
        private readonly PdaSystem _pdaSystem;
        private readonly ISharedPlayerManager _playerMgr; // stalker-en-changes

        [ViewVariables]
        private PdaMenu? _menu;

        public PdaBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
            _pdaSystem = EntMan.System<PdaSystem>();
            _playerMgr = IoCManager.Resolve<ISharedPlayerManager>(); // stalker-en-changes
        }

        protected override void Open()
        {
            base.Open();

            if (_menu == null)
                CreateMenu();
        }

        private void CreateMenu()
        {
            _menu = this.CreateWindowCenteredLeft<PdaMenu>();

            _menu.FlashLightToggleButton.OnToggled += _ =>
            {
                SendMessage(new PdaToggleFlashlightMessage());
            };

            _menu.EjectIdButton.OnPressed += _ =>
            {
                SendPredictedMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaIdSlotId));
            };

            _menu.EjectPenButton.OnPressed += _ =>
            {
                SendPredictedMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaPenSlotId));
            };

            _menu.EjectPaiButton.OnPressed += _ =>
            {
                SendPredictedMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaPaiSlotId));
            };

            _menu.ActivateMusicButton.OnPressed += _ =>
            {
                SendMessage(new PdaShowMusicMessage());
            };

            _menu.AccessRingtoneButton.OnPressed += _ =>
            {
                SendMessage(new PdaShowRingtoneMessage());
            };

            _menu.SilentModeButton.OnPressed += _ =>
            {
                SendMessage(new STPdaToggleSilentModeMessage());
            };

            _menu.ShowUplinkButton.OnPressed += _ =>
            {
                SendMessage(new PdaShowUplinkMessage());
            };

            _menu.LockUplinkButton.OnPressed += _ =>
            {
                SendMessage(new PdaLockUplinkMessage());
            };

            // stalker-en-changes: PDA password settings
            _menu.SetPasswordButton.OnPressed += _ =>
            {
                SendMessage(new STPdaPasswordOpenSettingsMessage());
            };

            _menu.OnProgramItemPressed += (uid) =>
            {
                // Close current program before opening a new one
                _menu?.CloseProgram();
                ActivateCartridge(uid);
            };
            _menu.OnInstallButtonPressed += InstallCartridge;
            _menu.OnUninstallButtonPressed += UninstallCartridge;

            var borderColorComponent = GetBorderColorComponent();
            if (borderColorComponent == null)
                return;

            _menu.BorderColor = borderColorComponent.BorderColor;
            _menu.AccentHColor = borderColorComponent.AccentHColor;
            _menu.AccentVColor = borderColorComponent.AccentVColor;
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not PdaUpdateState updateState)
                return;

            if (_menu == null)
            {
                _pdaSystem.Log.Error("PDA state received before menu was created.");
                return;
            }

            _menu.UpdateState(updateState);

            // stalker-en-changes-start: hide password button for non-owners (use entity comparison)
            var isOwner = _playerMgr.LocalEntity.HasValue
                && updateState.PdaOwnerInfo.PdaOwnerEntity.HasValue
                && EntMan.GetEntity(updateState.PdaOwnerInfo.PdaOwnerEntity.Value) == _playerMgr.LocalEntity.Value;
            _menu.SetPasswordButton.Visible = isOwner;
            // stalker-en-changes-end
        }

        protected override void AttachCartridgeUI(Control cartridgeUIFragment, string? title)
        {
            _menu?.ProgramView.AddChild(cartridgeUIFragment);
            _menu?.ToProgramView(title ?? Loc.GetString("comp-pda-io-program-fallback-title"));
        }

        protected override void DetachCartridgeUI(Control cartridgeUIFragment)
        {
            if (_menu is null)
                return;

            if (cartridgeUIFragment.Parent != null)
                cartridgeUIFragment.Orphan();
            _menu.CloseProgram();
        }

        protected override void UpdateAvailablePrograms(List<(EntityUid, CartridgeComponent)> programs)
        {
            _menu?.UpdateAvailablePrograms(programs);
        }

        private PdaBorderColorComponent? GetBorderColorComponent()
        {
            return EntMan.GetComponentOrNull<PdaBorderColorComponent>(Owner);
        }
    }
}
