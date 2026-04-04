using Content.Shared._Stalker.PersistentCrafting;
using Content.Client._Stalker.PersistentCrafting.UI;
using Robust.Shared.Prototypes;

namespace Content.Client._Stalker.PersistentCrafting;

public sealed class PersistentCraftingSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    private const float InventoryRefreshInterval = 0.5f;

    private PersistentCraftClientPrototypeCache _prototypeCache = default!;
    private PersistentCraftStationWindow? _craftWindow;
    private PersistentCraftingWindow? _skillsWindow;
    private PersistentCraftState? _latestState;
    private float _inventoryRefreshAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        _prototypeCache = PersistentCraftClientPrototypeCache.Create(_prototype);

        SubscribeNetworkEvent<OpenPersistentCraftMenuEvent>(OnOpenMenuEvent);
        SubscribeNetworkEvent<PersistentCraftStateEvent>(OnStateEvent);
        SubscribeNetworkEvent<PersistentCraftRecipeStartedEvent>(OnRecipeStartedEvent);
        SubscribeNetworkEvent<PersistentCraftRecipeFinishedEvent>(OnRecipeFinishedEvent);
    }

    public void RequestState()
    {
        RaiseNetworkEvent(new RequestPersistentCraftStateEvent());
    }

    public void RequestUnlock(string nodeId)
    {
        RaiseNetworkEvent(new RequestPersistentCraftUnlockEvent(nodeId));
    }

    public void RequestCraft(string recipeId)
    {
        RaiseNetworkEvent(new RequestPersistentCraftRecipeEvent(recipeId));
    }

    public void OpenSkillsWindow()
    {
        EnsureSkillsWindow();
        _skillsWindow!.ResetInitialTabSelection();
        _skillsWindow.ApplyFullscreenLayout();

        if (!_skillsWindow!.IsOpen)
            _skillsWindow.OpenCentered();
        else
            _skillsWindow.MoveToFront();

        RefreshSkillWindow();
    }

    private void ToggleSkillsWindowFromCraft()
    {
        EnsureSkillsWindow();

        if (_skillsWindow!.IsOpen)
        {
            _skillsWindow.Close();
            return;
        }

        OpenSkillsWindow();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_craftWindow == null ||
            _craftWindow.Disposed ||
            !_craftWindow.IsOpen ||
            _latestState == null)
        {
            _inventoryRefreshAccumulator = 0f;
            return;
        }

        _inventoryRefreshAccumulator += frameTime;
        if (_inventoryRefreshAccumulator < InventoryRefreshInterval)
            return;

        _inventoryRefreshAccumulator = 0f;
        _craftWindow.RefreshLocalInventory();
    }

    private void OnOpenMenuEvent(OpenPersistentCraftMenuEvent ev, EntitySessionEventArgs args)
    {
        EnsureCraftWindow();
        if (_craftWindow!.IsOpen)
        {
            _craftWindow.Close();

            if (_skillsWindow is { IsOpen: true })
                _skillsWindow.Close();

            return;
        }

        _craftWindow.ResetInitialTabSelection();
        _craftWindow.OpenCentered();

        RefreshCraftWindow();
        RequestState();
    }

    private void OnStateEvent(PersistentCraftStateEvent ev, EntitySessionEventArgs args)
    {
        _latestState = ev.State;
        RefreshCraftWindow();
        RefreshSkillWindow();
    }

    private void OnRecipeStartedEvent(PersistentCraftRecipeStartedEvent ev, EntitySessionEventArgs args)
    {
        _craftWindow?.NotifyCraftStarted(ev.RecipeId, ev.DurationSeconds);
    }

    private void OnRecipeFinishedEvent(PersistentCraftRecipeFinishedEvent ev, EntitySessionEventArgs args)
    {
        _craftWindow?.NotifyCraftFinished(ev.RecipeId, ev.Result);
    }

    private void EnsureCraftWindow()
    {
        _craftWindow ??= new PersistentCraftStationWindow(_prototypeCache);
        if (_craftWindow.Disposed)
            _craftWindow = new PersistentCraftStationWindow(_prototypeCache);

        _craftWindow.OnCraftPressed -= OnCraftRequestedFromWindow;
        _craftWindow.OnCraftPressed += OnCraftRequestedFromWindow;
        _craftWindow.OnOpenSkillsPressed -= ToggleSkillsWindowFromCraft;
        _craftWindow.OnOpenSkillsPressed += ToggleSkillsWindowFromCraft;
    }

    private void OnCraftRequestedFromWindow(string recipeId)
    {
        RequestCraft(recipeId);
    }

    private void EnsureSkillsWindow()
    {
        _skillsWindow ??= new PersistentCraftingWindow(_prototypeCache);
        if (_skillsWindow.Disposed)
            _skillsWindow = new PersistentCraftingWindow(_prototypeCache);
    }

    private void RefreshCraftWindow()
    {
        if (_craftWindow == null || _craftWindow.Disposed || !_craftWindow.IsOpen || _latestState == null)
            return;

        _craftWindow.UpdateState(_latestState, _prototypeCache);
    }

    private void RefreshSkillWindow()
    {
        if (_skillsWindow == null || _skillsWindow.Disposed || !_skillsWindow.IsOpen || _latestState == null)
            return;

        _skillsWindow.UpdateState(
            _latestState,
            _prototypeCache,
            RequestUnlock);
    }
}
