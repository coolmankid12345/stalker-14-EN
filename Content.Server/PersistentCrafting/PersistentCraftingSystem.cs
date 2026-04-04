using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Stalker.PersistentCrafting;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.PersistentCrafting;

public sealed class PersistentCraftingSystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const double CraftRateLimitSeconds = 0.5;
    private const double UnlockRateLimitSeconds = 0.3;
    private const double RateLimitCleanupIntervalSeconds = 60.0;
    private const int MaxNetworkStringLength = 128;
    private readonly Dictionary<NetUserId, TimeSpan> _lastCraftRequestTime = new();
    private readonly Dictionary<NetUserId, TimeSpan> _lastUnlockRequestTime = new();
    private TimeSpan _lastRateLimitCleanup;

    private readonly ConcurrentQueue<PendingProfileLoad> _completedLoads = new();
    private readonly ConcurrentQueue<PendingSaveFailure> _saveFailures = new();
    private readonly CancellationTokenSource _shutdownCts = new();

    private PersistentCraftBranchRegistry _branchRegistry = default!;
    private PersistentCraftProfileService _profileService = default!;
    private PersistentCraftProfileRepository _profileRepository = default!;
    private PersistentCraftUnlockService _unlockService = default!;
    private PersistentCraftCraftExecutionService _craftExecutionService = default!;
    private List<PersistentCraftNodePrototype> _nodeCache = new();

    public override void Initialize()
    {
        base.Initialize();

        _branchRegistry = PersistentCraftBranchRegistry.Create(_proto);
        _nodeCache = _proto.EnumeratePrototypes<PersistentCraftNodePrototype>().ToList();
        _profileService = new PersistentCraftProfileService(_proto, _branchRegistry, _nodeCache);
        _profileRepository = new PersistentCraftProfileRepository(_db, _branchRegistry);
        _unlockService = new PersistentCraftUnlockService(_profileService);
        _craftExecutionService = new PersistentCraftCraftExecutionService(
            EntityManager,
            _tag,
            _stacks,
            _hands,
            _profileService);
        ValidatePrototypeConfiguration();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<PersistentCraftAccessComponent, ComponentStartup>(OnAccessStartup);
        SubscribeLocalEvent<PersistentCraftAccessComponent, ComponentShutdown>(OnAccessShutdown);
        SubscribeLocalEvent<PersistentCraftAccessComponent, OpenPersistentCraftMenuActionEvent>(OnOpenCraftMenu);
        SubscribeLocalEvent<PersistentCraftAccessComponent, PersistentCraftDoAfterEvent>(OnCraftDoAfter);
        SubscribeNetworkEvent<RequestPersistentCraftStateEvent>(OnRequestState);
        SubscribeNetworkEvent<RequestPersistentCraftRecipeEvent>(OnRequestCraftRecipe);
        SubscribeNetworkEvent<RequestPersistentCraftUnlockEvent>(OnRequestUnlock);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        ProcessCompletedLoads();
        ProcessSaveFailures();
        CleanupStaleRateLimitEntries();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        // Отменяем retry-циклы сохранений — иначе сервер ждёт до 15 секунд при выключении
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();
    }

    /// <summary>
    /// Возвращает true если игрок отправляет запросы слишком часто.
    /// Обновляет время последнего запроса при разрешении.
    /// </summary>
    private bool IsRateLimited(
        NetUserId userId,
        Dictionary<NetUserId, TimeSpan> lastRequestTime,
        double limitSeconds)
    {
        var now = _timing.CurTime;
        if (lastRequestTime.TryGetValue(userId, out var last) &&
            (now - last).TotalSeconds < limitSeconds)
        {
            return true;
        }

        lastRequestTime[userId] = now;
        return false;
    }

    private void CleanupStaleRateLimitEntries()
    {
        var now = _timing.CurTime;
        if ((now - _lastRateLimitCleanup).TotalSeconds < RateLimitCleanupIntervalSeconds)
            return;

        _lastRateLimitCleanup = now;
        CleanupRateLimitDictionary(_lastCraftRequestTime, now, CraftRateLimitSeconds);
        CleanupRateLimitDictionary(_lastUnlockRequestTime, now, UnlockRateLimitSeconds);
    }

    private static void CleanupRateLimitDictionary(
        Dictionary<NetUserId, TimeSpan> dictionary,
        TimeSpan now,
        double limitSeconds)
    {
        List<NetUserId>? stale = null;
        foreach (var (userId, lastTime) in dictionary)
        {
            if ((now - lastTime).TotalSeconds >= limitSeconds)
                (stale ??= new List<NetUserId>()).Add(userId);
        }

        if (stale == null)
            return;

        for (var i = 0; i < stale.Count; i++)
            dictionary.Remove(stale[i]);
    }

    private void ValidatePrototypeConfiguration()
    {
        ValidateNodeAndRecipeDefinitions();
        ValidateRecipeIngredientDefinitions();
    }

    private void ValidateNodeAndRecipeDefinitions()
    {
        var branchIds = new HashSet<string>(_proto.EnumeratePrototypes<PersistentCraftBranchPrototype>().Select(branch => branch.ID));
        var categoryIds = new HashSet<string>(_proto.EnumeratePrototypes<PersistentCraftCategoryPrototype>().Select(category => category.ID));
        var subCategories = _proto.EnumeratePrototypes<PersistentCraftSubCategoryPrototype>().ToDictionary(subCategory => subCategory.ID);
        var nodesById = _nodeCache.ToDictionary(node => node.ID);
        var occupiedTreeSlots = new HashSet<string>();

        foreach (var branch in _proto.EnumeratePrototypes<PersistentCraftBranchPrototype>())
        {
            if (!string.IsNullOrWhiteSpace(branch.DefaultCategory) && !categoryIds.Contains(branch.DefaultCategory))
            {
                Log.Warning($"[PersistentCraft] Branch '{branch.ID}' references missing defaultCategory '{branch.DefaultCategory}'.");
            }
        }

        foreach (var node in _nodeCache)
        {
            if (!branchIds.Contains(node.Branch))
                Log.Warning($"[PersistentCraft] Node '{node.ID}' references missing branch '{node.Branch}'.");

            if (node.Cost < 0)
                Log.Warning($"[PersistentCraft] Node '{node.ID}' has negative cost '{node.Cost}'.");

            if (!string.IsNullOrWhiteSpace(node.DisplayProto) && !_proto.TryIndex<EntityPrototype>(node.DisplayProto, out _))
            {
                Log.Warning($"[PersistentCraft] Node '{node.ID}' references missing displayProto '{node.DisplayProto}'.");
            }

            if (node.TreeColumn >= 0 && node.TreeRow >= 0)
            {
                var slotKey = $"{node.Branch}|{node.TreeColumn}|{node.TreeRow}";
                if (!occupiedTreeSlots.Add(slotKey))
                {
                    Log.Warning($"[PersistentCraft] Duplicate tree position for branch '{node.Branch}' at column={node.TreeColumn}, row={node.TreeRow}. Node='{node.ID}'.");
                }
            }

            for (var i = 0; i < node.Prerequisites.Count; i++)
            {
                var prerequisiteId = node.Prerequisites[i];
                if (!nodesById.TryGetValue(prerequisiteId, out var prerequisite))
                {
                    Log.Warning($"[PersistentCraft] Node '{node.ID}' references missing prerequisite '{prerequisiteId}'.");
                    continue;
                }

                if (!string.Equals(prerequisite.Branch, node.Branch, StringComparison.Ordinal))
                {
                    Log.Warning($"[PersistentCraft] Node '{node.ID}' has cross-branch prerequisite '{prerequisiteId}' ('{prerequisite.Branch}' -> '{node.Branch}').");
                }
            }
        }

        ValidateNodeCycles(nodesById);

        foreach (var recipe in _proto.EnumeratePrototypes<PersistentCraftRecipePrototype>())
        {
            if (!branchIds.Contains(recipe.Branch))
                Log.Warning($"[PersistentCraft] Recipe '{recipe.ID}' references missing branch '{recipe.Branch}'.");

            if (recipe.CraftTime < 0f)
                Log.Warning($"[PersistentCraft] Recipe '{recipe.ID}' has negative craftTime '{recipe.CraftTime}'.");

            if (recipe.PointReward < 0)
                Log.Warning($"[PersistentCraft] Recipe '{recipe.ID}' has negative pointReward '{recipe.PointReward}'.");

            if (!nodesById.TryGetValue(recipe.RequiredNode, out var requiredNode))
            {
                Log.Warning($"[PersistentCraft] Recipe '{recipe.ID}' references missing requiredNode '{recipe.RequiredNode}'.");
            }
            else if (!string.Equals(requiredNode.Branch, recipe.Branch, StringComparison.Ordinal))
            {
                Log.Warning($"[PersistentCraft] Recipe '{recipe.ID}' branch '{recipe.Branch}' does not match requiredNode '{recipe.RequiredNode}' branch '{requiredNode.Branch}'.");
            }

            if (!string.IsNullOrWhiteSpace(recipe.DisplayProto) && !_proto.TryIndex<EntityPrototype>(recipe.DisplayProto, out _))
            {
                Log.Warning($"[PersistentCraft] Recipe '{recipe.ID}' references missing displayProto '{recipe.DisplayProto}'.");
            }

            if (!string.IsNullOrWhiteSpace(recipe.Category) && !categoryIds.Contains(recipe.Category))
            {
                Log.Warning($"[PersistentCraft] Recipe '{recipe.ID}' references missing category '{recipe.Category}'.");
            }

            if (!string.IsNullOrWhiteSpace(recipe.SubCategory))
            {
                if (!subCategories.TryGetValue(recipe.SubCategory, out var subCategory))
                {
                    Log.Warning($"[PersistentCraft] Recipe '{recipe.ID}' references missing subCategory '{recipe.SubCategory}'.");
                }
                else if (!string.IsNullOrWhiteSpace(recipe.Category) &&
                         !string.IsNullOrWhiteSpace(subCategory.Category) &&
                         !string.Equals(subCategory.Category, recipe.Category, StringComparison.Ordinal))
                {
                    Log.Warning($"[PersistentCraft] Recipe '{recipe.ID}' uses category '{recipe.Category}' but subCategory '{recipe.SubCategory}' belongs to '{subCategory.Category ?? string.Empty}'.");
                }
            }

            if (recipe.Results.Count == 0)
                Log.Warning($"[PersistentCraft] Recipe '{recipe.ID}' has no results.");

            for (var i = 0; i < recipe.Results.Count; i++)
            {
                var result = recipe.Results[i];
                if (string.IsNullOrWhiteSpace(result.Proto) || !_proto.TryIndex<EntityPrototype>(result.Proto, out _))
                {
                    Log.Warning($"[PersistentCraft] Recipe '{recipe.ID}' result #{i} references missing proto '{result.Proto}'.");
                }

                if (result.Amount <= 0)
                    Log.Warning($"[PersistentCraft] Recipe '{recipe.ID}' result #{i} has non-positive amount '{result.Amount}'.");
            }
        }
    }

    private void ValidateNodeCycles(IReadOnlyDictionary<string, PersistentCraftNodePrototype> nodesById)
    {
        var visitState = new Dictionary<string, byte>(nodesById.Count);
        var path = new Stack<string>();

        foreach (var nodeId in nodesById.Keys)
        {
            ValidateNodeCyclesDfs(nodeId, nodesById, visitState, path);
        }
    }

    private void ValidateNodeCyclesDfs(
        string nodeId,
        IReadOnlyDictionary<string, PersistentCraftNodePrototype> nodesById,
        Dictionary<string, byte> visitState,
        Stack<string> path)
    {
        if (visitState.TryGetValue(nodeId, out var state))
        {
            if (state == 1)
            {
                var cycle = string.Join(" -> ", path.Reverse().Append(nodeId));
                Log.Warning($"[PersistentCraft] Detected prerequisite cycle: {cycle}");
            }

            return;
        }

        visitState[nodeId] = 1;
        path.Push(nodeId);

        if (nodesById.TryGetValue(nodeId, out var node))
        {
            for (var i = 0; i < node.Prerequisites.Count; i++)
            {
                var prerequisiteId = node.Prerequisites[i];
                if (!nodesById.ContainsKey(prerequisiteId))
                    continue;

                ValidateNodeCyclesDfs(prerequisiteId, nodesById, visitState, path);
            }
        }

        path.Pop();
        visitState[nodeId] = 2;
    }

    private void ValidateRecipeIngredientDefinitions()
    {
        foreach (var recipe in _proto.EnumeratePrototypes<PersistentCraftRecipePrototype>())
        {
            for (var index = 0; index < recipe.Ingredients.Count; index++)
            {
                var ingredient = recipe.Ingredients[index];
                var selectorKind = ingredient.GetSelectorKind();
                var selectorValue = ingredient.GetSelectorValue();

                if (selectorKind == PersistentCraftIngredientSelectorKind.None)
                {
                    Log.Warning($"[PersistentCraft] Recipe '{recipe.ID}' ingredient #{index} has no selector (proto/stackType/tag).");
                }
                else if (selectorKind == PersistentCraftIngredientSelectorKind.InvalidMultiple)
                {
                    Log.Warning($"[PersistentCraft] Recipe '{recipe.ID}' ingredient #{index} has multiple selectors set. proto='{ingredient.Proto ?? string.Empty}', stackType='{ingredient.StackType ?? string.Empty}', tag='{ingredient.Tag ?? string.Empty}'.");
                }
                else if (selectorKind == PersistentCraftIngredientSelectorKind.Proto &&
                         !string.IsNullOrWhiteSpace(ingredient.Proto) &&
                         !_proto.TryIndex<EntityPrototype>(ingredient.Proto, out _))
                {
                    Log.Warning($"[PersistentCraft] Recipe '{recipe.ID}' ingredient #{index} references missing proto '{ingredient.Proto}'.");
                }

                if (ingredient.Amount <= 0)
                {
                    Log.Warning($"[PersistentCraft] Recipe '{recipe.ID}' ingredient #{index} has non-positive amount '{ingredient.Amount}'. Selector={selectorKind} Value='{selectorValue}'.");
                }
            }
        }
    }

    public PersistentCraftState GetState(EntityUid uid)
    {
        if (!TryComp(uid, out PersistentCraftProfileComponent? profile))
        {
            var defaultProfile = new PersistentCraftProfileComponent
            {
                BranchProgress = _profileService.CreateDefaultBranchProfiles(),
            };

            return new PersistentCraftState(
                false,
                _profileService.BuildBranchStates(defaultProfile),
                new List<string>());
        }

        return new PersistentCraftState(
            profile.Loaded,
            _profileService.BuildBranchStates(profile),
            profile.UnlockedNodes.OrderBy(id => id).ToList());
    }

    public bool IsLoaded(EntityUid uid)
    {
        return TryComp(uid, out PersistentCraftProfileComponent? profile) && profile.Loaded;
    }

    public Task<bool> ResetProfileAsync(EntityUid uid)
    {
        if (!TryComp(uid, out PersistentCraftProfileComponent? profile))
            return Task.FromResult(false);

        if (profile.UserId == Guid.Empty || string.IsNullOrWhiteSpace(profile.CharacterName))
            return Task.FromResult(false);

        profile.BranchProgress = _profileService.CreateDefaultBranchProfiles();
        profile.UnlockedNodes.Clear();
        profile.Loaded = true;
        profile.PersistenceDisabled = false;
        PrepareProfileForPersistence(profile);
        QueueSaveProfile(uid, profile);
        SendStateToAttachedActor(uid);
        return Task.FromResult(true);
    }

    public string SerializeEmptyProfile()
    {
        var emptySnapshot = new PersistentCraftProfileSnapshot
        {
            UserId = Guid.Empty,
            CharacterName = string.Empty,
            BranchEarnedPoints = new Dictionary<string, int>(),
            UnlockedNodes = new List<string>(),
        };
        return _profileRepository.SerializeSaveData(emptySnapshot);
    }

    public Task WriteProfileJsonAsync(Guid userId, string characterName, string json)
    {
        return _db.SetStalkerPersistentCraftProfileAsync(userId, characterName, json);
    }

    private void OnAccessStartup(EntityUid uid, PersistentCraftAccessComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, ref component.ActionEntity, component.Action, uid);
    }

    private void OnAccessShutdown(EntityUid uid, PersistentCraftAccessComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.ActionEntity);
        component.ActionEntity = null;
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        var profile = EnsureComp<PersistentCraftProfileComponent>(args.Mob);
        profile.LoadGeneration++;
        profile.UserId = args.Player.UserId.UserId;
        profile.CharacterName = args.Profile.Name;
        profile.BranchProgress = _profileService.CreateDefaultBranchProfiles();
        profile.UnlockedNodes.Clear();
        _profileService.EnsureAutoTierNodesUnlocked(profile);
        profile.Loaded = false;
        profile.PersistenceDisabled = false;

        StartLoadProfile(args.Mob, profile.UserId, profile.CharacterName, profile.LoadGeneration);
    }

    private void OnOpenCraftMenu(EntityUid uid, PersistentCraftAccessComponent component, OpenPersistentCraftMenuActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryComp(args.Performer, out ActorComponent? actor))
            return;

        RaiseNetworkEvent(new OpenPersistentCraftMenuEvent(), actor.PlayerSession);
        SendState(actor.PlayerSession, args.Performer);
    }

    private void OnRequestState(RequestPersistentCraftStateEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { Valid: true } user)
            return;

        SendState(args.SenderSession, user);
    }

    private void OnRequestCraftRecipe(RequestPersistentCraftRecipeEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { Valid: true } user)
            return;

        if (ev.RecipeId.Length > MaxNetworkStringLength)
            return;

        if (IsRateLimited(args.SenderSession.UserId, _lastCraftRequestTime, CraftRateLimitSeconds))
            return;

        if (!HasComp<PersistentCraftAccessComponent>(user))
            return;

        if (!_proto.TryIndex<PersistentCraftRecipePrototype>(ev.RecipeId, out var recipe))
            return;

        if (!IsLoaded(user))
        {
            PopupUser(user, "persistent-craft-popup-loading");
            SendState(args.SenderSession, user);
            return;
        }

        if (!_craftExecutionService.MeetsRecipeRequirement(user, recipe))
        {
            PopupUser(user, "persistent-craft-station-popup-skill-locked");
            SendState(args.SenderSession, user);
            return;
        }

        if (!_craftExecutionService.TryPlanIngredientConsumption(user, recipe, out _))
        {
            PopupUser(user, "persistent-craft-station-popup-missing-items");
            SendState(args.SenderSession, user);
            return;
        }

        if (!TryStartCraftDoAfter(user, recipe))
            return;

        RaiseNetworkEvent(
            new PersistentCraftRecipeStartedEvent(
                recipe.ID,
                _craftExecutionService.GetEffectiveCraftTime(recipe)),
            args.SenderSession);

        _popup.PopupEntity(
            Loc.GetString("persistent-craft-station-popup-started", ("recipe", ResolveRecipeName(recipe))),
            user,
            user);
    }

    private void OnRequestUnlock(RequestPersistentCraftUnlockEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { Valid: true } user)
            return;

        if (ev.NodeId.Length > MaxNetworkStringLength)
            return;

        if (IsRateLimited(args.SenderSession.UserId, _lastUnlockRequestTime, UnlockRateLimitSeconds))
            return;

        if (!HasComp<PersistentCraftAccessComponent>(user))
            return;

        if (!TryComp(user, out PersistentCraftProfileComponent? profile))
            return;

        if (!profile.Loaded)
        {
            _popup.PopupEntity(Loc.GetString("persistent-craft-popup-loading"), user, user);
            SendState(args.SenderSession, user);
            return;
        }

        if (!_proto.TryIndex<PersistentCraftNodePrototype>(ev.NodeId, out var node))
            return;

        if (!_unlockService.TryUnlockNode(profile, node, out var failure))
        {
            var failureLoc = failure switch
            {
                PersistentCraftUnlockFailure.AutoUnlockedNode => "persistent-craft-popup-tier-auto",
                PersistentCraftUnlockFailure.AlreadyUnlocked => "persistent-craft-popup-already-unlocked",
                PersistentCraftUnlockFailure.MissingPrerequisites => "persistent-craft-popup-prerequisite",
                PersistentCraftUnlockFailure.NotEnoughPoints => "persistent-craft-popup-not-enough-points",
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(failureLoc))
                _popup.PopupEntity(Loc.GetString(failureLoc), user, user);

            return;
        }

        QueueSaveProfile(user, profile);

        _popup.PopupEntity(
            Loc.GetString("persistent-craft-popup-unlocked", ("skill", ResolveNodeName(node))),
            user,
            user);

        SendState(args.SenderSession, user);
    }

    private void OnCraftDoAfter(EntityUid uid, PersistentCraftAccessComponent component, PersistentCraftDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (!_proto.TryIndex<PersistentCraftRecipePrototype>(args.RecipeId, out var recipe))
            return;

        if (!Exists(args.User) || args.User != uid)
            return;

        if (args.Cancelled)
        {
            args.Handled = true;
            SendCraftRecipeExecutionToAttachedActor(args.User, recipe.ID, PersistentCraftRecipeExecutionResult.Cancelled);
            SendStateToAttachedActor(args.User);
            return;
        }

        args.Handled = true;

        if (!IsLoaded(args.User))
        {
            PopupUser(args.User, "persistent-craft-popup-loading");
            SendCraftRecipeExecutionToAttachedActor(args.User, recipe.ID, PersistentCraftRecipeExecutionResult.Cancelled);
            SendStateToAttachedActor(args.User);
            return;
        }

        if (!_craftExecutionService.MeetsRecipeRequirement(args.User, recipe))
        {
            PopupUser(args.User, "persistent-craft-station-popup-skill-locked");
            SendCraftRecipeExecutionToAttachedActor(args.User, recipe.ID, PersistentCraftRecipeExecutionResult.Cancelled);
            SendStateToAttachedActor(args.User);
            return;
        }

        if (!_craftExecutionService.TryPlanIngredientConsumption(args.User, recipe, out var plan))
        {
            PopupUser(args.User, "persistent-craft-station-popup-missing-items");
            SendCraftRecipeExecutionToAttachedActor(args.User, recipe.ID, PersistentCraftRecipeExecutionResult.Cancelled);
            SendStateToAttachedActor(args.User);
            return;
        }

        _craftExecutionService.ConsumeIngredientPlan(plan);
        _craftExecutionService.SpawnResults(args.User, recipe);
        _craftExecutionService.GrantCraftPoints(args.User, recipe);

        _popup.PopupEntity(
            Loc.GetString("persistent-craft-station-popup-crafted", ("recipe", ResolveRecipeName(recipe))),
            args.User,
            args.User);

        var pointsReward = PersistentCraftingHelper.GetPointReward(recipe);
        if (pointsReward > 0)
        {
            _popup.PopupEntity(
                Loc.GetString("persistent-craft-popup-points-gained", ("points", pointsReward)),
                args.User,
                args.User);
        }

        if (TryComp(args.User, out PersistentCraftProfileComponent? craftProfile))
            QueueSaveProfile(args.User, craftProfile);

        SendCraftRecipeExecutionToAttachedActor(args.User, recipe.ID, PersistentCraftRecipeExecutionResult.Completed);
        SendStateToAttachedActor(args.User);
    }

    private bool TryStartCraftDoAfter(EntityUid user, PersistentCraftRecipePrototype recipe)
    {
        var craftTime = _craftExecutionService.GetEffectiveCraftTime(recipe);
        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            craftTime,
            new PersistentCraftDoAfterEvent(recipe.ID),
            user,
            target: user,
            used: user)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            RequireCanInteract = true,
            BlockDuplicate = true,
        };

        return _doAfter.TryStartDoAfter(doAfter);
    }

    private void SendState(ICommonSession session, EntityUid uid)
    {
        RaiseNetworkEvent(new PersistentCraftStateEvent(GetState(uid)), session);
    }

    private void SendStateToAttachedActor(EntityUid uid)
    {
        if (!TryComp(uid, out ActorComponent? actor))
            return;

        SendState(actor.PlayerSession, uid);
    }

    private void SendCraftRecipeExecutionToAttachedActor(
        EntityUid uid,
        string recipeId,
        PersistentCraftRecipeExecutionResult result)
    {
        if (!TryComp(uid, out ActorComponent? actor))
            return;

        RaiseNetworkEvent(new PersistentCraftRecipeFinishedEvent(recipeId, result), actor.PlayerSession);
    }

    private void StartLoadProfile(EntityUid uid, Guid userId, string characterName, int loadGeneration)
    {
        _ = LoadProfileInBackground(uid, userId, characterName, loadGeneration);
    }

    private async Task LoadProfileInBackground(EntityUid uid, Guid userId, string characterName, int loadGeneration)
    {
        PersistentCraftProfileLoadResult loadResult;

        try
        {
            loadResult = await _profileRepository.LoadProfileAsync(userId, characterName);
        }
        catch (Exception ex)
        {
            loadResult = PersistentCraftProfileLoadResult.Failed($"[PersistentCraft] Failed to load profile for '{characterName}': {ex}");
        }

        _completedLoads.Enqueue(new PendingProfileLoad(uid, userId, characterName, loadGeneration, loadResult));
    }

    private void ProcessCompletedLoads()
    {
        while (_completedLoads.TryDequeue(out var pending))
        {
            ApplyLoadedProfile(pending);
        }
    }

    private void ApplyLoadedProfile(PendingProfileLoad pending)
    {
        if (Deleted(pending.Uid) ||
            !TryComp(pending.Uid, out PersistentCraftProfileComponent? profile) ||
            profile.LoadGeneration != pending.LoadGeneration)
            return;

        profile.BranchProgress = _profileService.CreateDefaultBranchProfiles();
        profile.UnlockedNodes.Clear();
        profile.PersistenceDisabled = false;
        var shouldResaveProfile = false;

        if (!pending.LoadResult.Success)
        {
            profile.PersistenceDisabled = true;
            Log.Error(pending.LoadResult.ErrorMessage ?? $"[PersistentCraft] Persistent craft profile for '{pending.CharacterName}' ({pending.UserId}) is unreadable. Using a temporary in-memory profile without persistence.");

            if (TryComp(pending.Uid, out ActorComponent? popupActor))
            {
                _popup.PopupEntity(
                    Loc.GetString("persistent-craft-load-failed"),
                    pending.Uid,
                    popupActor.PlayerSession,
                    PopupType.MediumCaution);
            }
        }
        else if (pending.LoadResult.HasSavedProfile && pending.LoadResult.SaveData != null)
        {
            var saveData = pending.LoadResult.SaveData;
            profile.BranchProgress = _profileService.BuildBranchProfiles(saveData.Branches);
            profile.UnlockedNodes = _profileService.SanitizeUnlockedNodes(saveData.UnlockedNodes, pending.CharacterName);
            shouldResaveProfile = pending.LoadResult.DataChanged || profile.UnlockedNodes.Count != saveData.UnlockedNodes.Count;
        }

        PrepareProfileForPersistence(profile);
        profile.Loaded = true;

        if (shouldResaveProfile)
            QueueSaveProfile(pending.Uid, profile);

        if (TryComp(pending.Uid, out ActorComponent? actor))
            SendState(actor.PlayerSession, pending.Uid);
    }

    private void QueueSaveProfile(EntityUid uid, PersistentCraftProfileComponent profile)
    {
        if (profile.PersistenceDisabled)
            return;

        PrepareProfileForPersistence(profile);
        var snapshot = CreateSnapshot(profile);
        _ = SaveProfileInBackground(uid, snapshot);
    }

    private async Task SaveProfileInBackground(EntityUid uid, PersistentCraftProfileSnapshot snapshot)
    {
        const int maxAttempts = 3;
        const int retryDelayMs = 5000;

        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await _profileRepository.SaveProfileAsync(snapshot);
                return;
            }
            catch (OperationCanceledException)
            {
                // Сервер выключается — прерываем retry без ошибки
                Log.Info($"[PersistentCraft] Save for '{snapshot.CharacterName}' cancelled on shutdown (attempt {attempt}/{maxAttempts}).");
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt < maxAttempts)
                {
                    Log.Warning($"[PersistentCraft] Save attempt {attempt}/{maxAttempts} failed for '{snapshot.CharacterName}', retrying in {retryDelayMs}ms: {ex.Message}");
                    try
                    {
                        await Task.Delay(retryDelayMs, _shutdownCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Info($"[PersistentCraft] Save retry for '{snapshot.CharacterName}' cancelled on shutdown.");
                        return;
                    }
                }
            }
        }

        _saveFailures.Enqueue(new PendingSaveFailure(uid, snapshot.CharacterName, lastException!));
    }

    private void ProcessSaveFailures()
    {
        while (_saveFailures.TryDequeue(out var failure))
        {
            Log.Error($"Failed to save persistent craft profile for {failure.CharacterName}: {failure.Exception}");

            if (!Deleted(failure.Uid) && TryComp(failure.Uid, out ActorComponent? actor))
            {
                _popup.PopupEntity(
                    Loc.GetString("persistent-craft-save-failed"),
                    failure.Uid,
                    actor.PlayerSession,
                    PopupType.MediumCaution);
            }
        }
    }

    private void PrepareProfileForPersistence(PersistentCraftProfileComponent profile)
    {
        profile.UnlockedNodes = _profileService.SanitizeUnlockedNodes(profile.UnlockedNodes, profile.CharacterName);
        _profileService.EnsureAutoTierNodesUnlocked(profile);
    }

    private PersistentCraftProfileSnapshot CreateSnapshot(PersistentCraftProfileComponent profile)
    {
        var branchEarnedPoints = new Dictionary<string, int>(profile.BranchProgress.Count);
        foreach (var entry in profile.BranchProgress)
        {
            branchEarnedPoints[entry.Key] = entry.Value.TotalEarnedPoints;
        }

        return new PersistentCraftProfileSnapshot
        {
            UserId = profile.UserId,
            CharacterName = profile.CharacterName,
            BranchEarnedPoints = branchEarnedPoints,
            UnlockedNodes = profile.UnlockedNodes
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .OrderBy(id => id)
                .ToList(),
        };
    }

    private void PopupUser(EntityUid user, string locKey)
    {
        _popup.PopupEntity(Loc.GetString(locKey), user, user);
    }

    private string ResolveRecipeName(PersistentCraftRecipePrototype recipe)
    {
        var displayProto = PersistentCraftingHelper.GetDisplayPrototypeId(recipe);
        if (!string.IsNullOrWhiteSpace(displayProto) &&
            _proto.TryIndex<EntityPrototype>(displayProto, out var prototype))
        {
            return prototype.Name;
        }

        return Loc.GetString(recipe.Name);
    }

    private string ResolveNodeName(PersistentCraftNodePrototype node)
    {
        if (!string.IsNullOrWhiteSpace(node.Name))
        {
            try
            {
                return Loc.GetString(node.Name);
            }
            catch (Exception ex)
            {
                Log.Warning($"[PersistentCraft] Missing loc key '{node.Name}' for node '{node.ID}': {ex.Message}");
                return node.Name;
            }
        }

        if (!string.IsNullOrWhiteSpace(node.DisplayProto) &&
            _proto.TryIndex<EntityPrototype>(node.DisplayProto, out var prototype))
        {
            return prototype.Name;
        }

        return node.ID;
    }

    private sealed class PendingProfileLoad
    {
        public readonly EntityUid Uid;
        public readonly Guid UserId;
        public readonly string CharacterName;
        public readonly int LoadGeneration;
        public readonly PersistentCraftProfileLoadResult LoadResult;

        public PendingProfileLoad(EntityUid uid, Guid userId, string characterName, int loadGeneration, PersistentCraftProfileLoadResult loadResult)
        {
            Uid = uid;
            UserId = userId;
            CharacterName = characterName;
            LoadGeneration = loadGeneration;
            LoadResult = loadResult;
        }
    }

    private sealed class PendingSaveFailure
    {
        public readonly EntityUid Uid;
        public readonly string CharacterName;
        public readonly Exception Exception;

        public PendingSaveFailure(EntityUid uid, string characterName, Exception exception)
        {
            Uid = uid;
            CharacterName = characterName;
            Exception = exception;
        }
    }
}
