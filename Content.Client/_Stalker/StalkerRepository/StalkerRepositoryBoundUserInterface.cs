using System.Collections.Generic;
using Content.Client._Stalker.Shop.Ui;
using Content.Client._Stalker_EN.Loadout;
using Content.Shared._Stalker.Shop;
using Content.Shared._Stalker.StalkerRepository;
using Content.Shared._Stalker_EN.Loadout;
using JetBrains.Annotations;

namespace Content.Client._Stalker.StalkerRepository;

/// <summary>
/// Stalker shops BUI to handle events raising and send data to server.
/// </summary>
[UsedImplicitly]
public sealed class StalkerRepositoryBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private StalkerRepositoryMenu? _menu;

    [ViewVariables]
    private LoadoutMenu? _loadoutMenu;

    // Cache loadouts so they're available when the loadout menu opens
    private List<PlayerLoadout> _cachedLoadouts = new();

    public StalkerRepositoryBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        _menu = new StalkerRepositoryMenu();
        _menu.OpenCentered();

        _menu.OnClose += Close;

        _menu.RepositoryButtonPutPressed += (item, count) =>
        {
            SendMessage(new RepositoryInjectFromUserMessage(item, count));
        };
        _menu.RepositoryButtonGetPressed += (item, count) =>
        {
            SendMessage(new RepositoryEjectMessage(item, count));
        };

        // Loadout button handler - opens separate window
        _menu.OpenLoadoutsPressed += OpenLoadoutMenu;
    }

    private void OpenLoadoutMenu()
    {
        // If menu exists, is not disposed, and is open - just bring to front
        if (_loadoutMenu is { Disposed: false, IsOpen: true })
        {
            _loadoutMenu.MoveToFront();
            return;
        }

        // Request fresh loadouts from server
        SendMessage(new LoadoutRequestMessage());

        _loadoutMenu = new LoadoutMenu();

        // Populate with cached loadouts immediately (may be empty on first open)
        _loadoutMenu.UpdateLoadouts(_cachedLoadouts);

        _loadoutMenu.OpenCentered();

        // Wire up loadout events
        _loadoutMenu.OnQuickSave += () =>
        {
            SendMessage(new LoadoutSaveMessage("Quick Save", true));
        };

        _loadoutMenu.OnQuickLoad += () =>
        {
            SendMessage(new LoadoutLoadMessage(0));
        };

        _loadoutMenu.OnSave += (name, isQuickSave) =>
        {
            SendMessage(new LoadoutSaveMessage(name, isQuickSave));
        };

        _loadoutMenu.OnLoad += loadoutId =>
        {
            SendMessage(new LoadoutLoadMessage(loadoutId));
        };

        _loadoutMenu.OnDelete += loadoutId =>
        {
            SendMessage(new LoadoutDeleteMessage(loadoutId));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        // Ensure menu is open and visible (BUI instances are reused between opens)
        if (_menu == null || _menu.Disposed)
        {
            _menu = new StalkerRepositoryMenu();
            _menu.OnClose += Close;
            _menu.RepositoryButtonPutPressed += (item, count) =>
            {
                SendMessage(new RepositoryInjectFromUserMessage(item, count));
            };
            _menu.RepositoryButtonGetPressed += (item, count) =>
            {
                SendMessage(new RepositoryEjectMessage(item, count));
            };
            _menu.OpenLoadoutsPressed += OpenLoadoutMenu;
        }

        if (!_menu.IsOpen)
        {
            _menu.OpenCentered();
        }

        // Basic place for handling states
        switch (state)
        {
            case RepositoryUpdateState msg:
                _menu.UpdateAll(msg.Items, msg.UserItems, msg.MaxWeight);
                break;
            case LoadoutUpdateState loadoutState:
                // Cache loadouts for when the menu opens
                _cachedLoadouts = loadoutState.Loadouts;
                // Update menu if it exists and is not disposed
                if (_loadoutMenu is { Disposed: false })
                    _loadoutMenu.UpdateLoadouts(loadoutState.Loadouts);
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _menu?.Close();
        _menu?.Dispose();
        _loadoutMenu?.Close();
        _loadoutMenu?.Dispose();
    }
}

