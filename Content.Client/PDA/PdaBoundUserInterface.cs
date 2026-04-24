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

            _menu.OnProgramDeactivated += DeactivateActiveCartridge;
            _menu.OnProgramActivate += ActivateCartridge;
            _menu.OnProgramInstall += InstallCartridge;
            _menu.OnProgramUninstall += UninstallCartridge;

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

            // Handle both CartridgeLoaderUiState (from Activate/Deactivate) and PdaUpdateState (from PdaSystem)
            var cartridgeState = state as CartridgeLoaderUiState;
            var serverActiveProgram = cartridgeState != null
                ? EntMan.GetEntity(cartridgeState.ActiveUI)
                : null;

            if (state is PdaUpdateState updateState)
            {
                if (_menu == null)
                {
                    _pdaSystem.Log.Error("PDA state received before menu was created.");
                    return;
                }

                _menu.SyncActiveProgram(serverActiveProgram);
                _menu.UpdateState(updateState);

                // stalker-en-changes-start: always show password button — if the user can see the PDA UI,
                // they already passed the server's OnOpenAttempt auth check (owner, unlocked, or no lock).
                // Server-side IsAuthorized in OnOpenSettings provides the actual security gate.
                _menu.SetPasswordButton.Visible = true;
                // stalker-en-changes-end
            }
            else if (_menu == null)
            {
                return;
            }

            // Server is the source of truth for which view to show.
            if (serverActiveProgram != null)
                _menu.ToProgramView();
            else if (serverActiveProgram.HasValue)
                _menu.OnServerProgramDeactivated();
        }

        protected override void AttachCartridgeUI(Control cartridgeUIFragment, string? title)
        {
            // Force the cartridge UI to expand to fill the entire ProgramView
            cartridgeUIFragment.VerticalExpand = true;
            cartridgeUIFragment.HorizontalExpand = true;

            _menu?.ProgramView.AddChild(cartridgeUIFragment);
            _menu?.ToProgramView();

        }

        protected override void DetachCartridgeUI(Control cartridgeUIFragment)
        {
            if (_menu is null)
                return;

            _menu.ProgramView.RemoveChild(cartridgeUIFragment);
        }

        protected override void UpdateAvailablePrograms(List<(EntityUid, CartridgeComponent)> programs)
        {
            _menu?.UpdateAvailablePrograms(programs);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _menu != null)
            {
                _menu.OnProgramDeactivated -= DeactivateActiveCartridge;
                _menu.OnProgramActivate -= ActivateCartridge;
                _menu.OnProgramInstall -= InstallCartridge;
                _menu.OnProgramUninstall -= UninstallCartridge;
            }

            base.Dispose(disposing);
        }

        private PdaBorderColorComponent? GetBorderColorComponent()
        {
            return EntMan.GetComponentOrNull<PdaBorderColorComponent>(Owner);
        }
    }
}
