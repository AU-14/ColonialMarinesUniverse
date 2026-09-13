using System.IO;
using System.Linq;
using Content.Server.Afk;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Server.Stack;
using Content.Shared.ActionBlocker;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.PersistentEconomy;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.PersistentEconomy;

public sealed partial class CMUPersistentEconomySystem : EntitySystem
{
    private static readonly EntProtoId CashPrototype = "RMCSpaceCash";
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IAfkManager _afk = default!;
    [Dependency] private IServerPreferencesManager _preferences = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private StackSystem _stacks = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private EuiManager _euis = default!;

    private CMUEconomyStore? _store;
    private readonly Dictionary<NetUserId, EntityUid> _deployed = new();
    private readonly Dictionary<NetUserId, float> _activeSeconds = new();
    private readonly HashSet<NetUserId> _pendingAttachments = new();
    private readonly Dictionary<NetUserId, CMUEconomyEui> _open = new();
    public bool Enabled => _config.GetCVar(CMUEconomyCVars.Enabled);
    public int RoundId => _ticker.RoundId;
    public CMUEconomyStore Store => _store ??= new CMUEconomyStore(Path.Combine(
        _resources.UserData.RootDir ?? throw new InvalidOperationException("Economy requires persistent user data"), "cmu-economy.sqlite"));

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawn);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
    }

    public override void Shutdown()
    {
        _store?.Dispose();
        _store = null;
        base.Shutdown();
    }

    private void OnCleanup(RoundRestartCleanupEvent args)
    {
        _deployed.Clear();
        _activeSeconds.Clear();
        _pendingAttachments.Clear();
        foreach (var eui in _open.Values.ToArray())
            eui.Close();
    }

    private void OnSpawn(PlayerSpawnCompleteEvent args)
    {
        if (!Enabled || RoundId <= 0 || args.JobId == null || HasComp<XenoComponent>(args.Mob) ||
            !_prototypes.TryIndex<JobPrototype>(args.JobId, out var job) || !job.CmuEconomyEnabled)
            return;
        var player = args.Player.UserId;
        _deployed[player] = args.Mob;
        if (Store.ReadRound(player.UserId, RoundId).DeploymentIssued)
            return;

        var account = Store.ReadAccount(player.UserId);
        var profile = _preferences.GetPreferences(player).IndexOfCharacter(args.Profile);
        var selections = account.Loadouts.GetValueOrDefault(profile) ?? new List<string>();
        var items = ValidateItems(selections, account, args.JobId);
        var cost = items.Sum(i => i.PurchaseMode == CMUPurchaseMode.Permanent ? 0 : i.Price);
        // Standard issue has already been equipped. An unaffordable optional kit falls back to it.
        if (cost > account.Balance)
        {
            items.Clear();
            cost = 0;
        }

        var spawned = new List<EntityUid>();
        try
        {
            var success = Store.Mutate(player.UserId, RoundId, $"deploy:{RoundId}:{player}", op =>
            {
                if (!op.Deploy(cost, StakePercent, StakeCap, MultiplierPercent, args.JobId))
                    return false;
                foreach (var item in items)
                    spawned.Add(Spawn(item.Entity, Transform(args.Mob).Coordinates));
                if (op.Round.Stake > 0)
                    SpawnCash(args.Mob, (int) op.Round.Stake, spawned);
                return true;
            });
            if (!success)
            {
                foreach (var entity in spawned)
                    Del(entity);
                return;
            }
            foreach (var entity in spawned)
                GiveItem(args.Mob, entity);
        }
        catch (Exception exception)
        {
            foreach (var entity in spawned)
            {
                if (Exists(entity))
                    Del(entity);
            }
            Log.Error($"Economy deployment failed for {player}: {exception}");
        }
        Refresh(player);
    }

    private void OnAttached(PlayerAttachedEvent args) => _pendingAttachments.Add(args.Player.UserId);

    private void ProcessAttachments()
    {
        // Ghost roles, threat forces and third parties assign their job AFTER transferring
        // the mind. Resolve next tick, once their spawn/equipment handlers have completed.
        foreach (var player in _pendingAttachments.ToArray())
        {
            _pendingAttachments.Remove(player);
            if (!_players.TryGetSessionById(player, out var session) || !_preferences.HavePreferencesLoaded(session) ||
                !_mind.TryGetMind(player, out var mindId, out var mind) || mind.OwnedEntity is not { } body ||
                session.AttachedEntity != body || !Alive(body) || !_jobs.MindTryGetJob(mindId, out var job) || !job.CmuEconomyEnabled)
                continue;
            OnSpawn(new PlayerSpawnCompleteEvent(body, session, job.ID, true, true, 0, EntityUid.Invalid,
                _preferences.GetPreferences(player).SelectedCharacter));
        }
    }

    private void GiveItem(EntityUid body, EntityUid item)
    {
        try
        {
            if (_inventory.TryGetSlotEntity(body, "back", out var back) && TryComp<StorageComponent>(back, out var storage) &&
                _storage.Insert(back.Value, item, out _, out _, storageComp: storage, playSound: false))
                return;
            _hands.TryPickupAnyHand(body, item, false);
        }
        catch (Exception e)
        {
            // Payment is already committed: retain the item in the world if inventory insertion fails.
            Log.Error($"Economy item {item} could not be placed in {body}'s inventory: {e}");
        }
    }

    private void SpawnCash(EntityUid body, int amount, List<EntityUid> spawned)
    {
        var maxCount = _stacks.GetMaxCount(CashPrototype);
        if (maxCount <= 0)
            throw new InvalidOperationException("Dollar stack has no capacity");
        while (amount > 0)
        {
            var item = Spawn(CashPrototype, Transform(body).Coordinates);
            // Record each spawned entity before invoking any stack events, for rollback cleanup.
            spawned.Add(item);
            var count = Math.Min(amount, maxCount);
            _stacks.SetCount(item, count);
            amount -= count;
        }
    }

    private int StakePercent => Math.Clamp(_config.GetCVar(CMUEconomyCVars.StakePercent), 0, 100);
    private int StakeCap => Math.Clamp(_config.GetCVar(CMUEconomyCVars.StakeCap), 0, 100_000);
    private int MultiplierPercent => Math.Clamp(_config.GetCVar(CMUEconomyCVars.MultiplierPercent), 0, 1000);

    private List<CMULoadoutItemPrototype> ValidateItems(IEnumerable<string> selected, CMUEconomyStore.Account account, string? job)
    {
        var result = new List<CMULoadoutItemPrototype>();
        foreach (var id in selected.Distinct().Take(20))
        {
            if (!_prototypes.TryIndex<CMULoadoutItemPrototype>(id, out var item) || item.Price < 0 ||
                item.Price > CMUEconomyStore.MaximumBalance || item.CategoryLimit is < 1 or > 3 ||
                !_prototypes.HasIndex(item.Entity) || (job != null && !item.Jobs.Contains(job)) ||
                item.PurchaseMode == CMUPurchaseMode.Permanent && !account.Purchases.ContainsKey(id))
                continue;
            var category = result.Where(i => i.Category == item.Category).ToList();
            if (category.Count >= item.CategoryLimit || category.Any(i => category.Count >= i.CategoryLimit))
                continue;
            result.Add(item);
        }
        return result;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!Enabled || _ticker.RunLevel != GameRunLevel.InRound)
            return;
        ProcessAttachments();
        foreach (var (player, _) in _deployed)
        {
            if (!_players.TryGetSessionById(player, out var session) || session.Status != SessionStatus.InGame ||
                _afk.IsAfk(session) || Body(player) is not { } body || session.AttachedEntity != body || !Alive(body))
                continue;
            var seconds = _activeSeconds.GetValueOrDefault(player) + frameTime;
            _activeSeconds[player] = seconds;
            if (seconds < 60)
                continue;
            var state = Store.ReadRound(player.UserId, RoundId);
            if (!_prototypes.TryIndex<JobPrototype>(state.Job, out var job) || !job.CmuEconomyEnabled)
                continue;
            var minute = state.ActiveMinutes + 1;
            var payUnits = Math.Clamp(_config.GetCVar(CMUEconomyCVars.Salary), 0, 1000) *
                           Math.Clamp(job.CmuSalaryPercent, 0, 200) + state.PayrollRemainder;
            var salary = payUnits / 100;
            try
            {
                if (Store.Mutate(player.UserId, RoundId, $"salary:{RoundId}:{player}:{minute}", op =>
                    {
                        if (op.Round.Settled || !op.Round.DeploymentIssued || op.Round.ActiveMinutes != minute - 1)
                            return false;
                        if (salary > 0 && !op.Change(salary, "Payroll", "Active service minute"))
                            return false;
                        op.Round.ActiveMinutes = minute;
                        op.Round.PayrollRemainder = payUnits % 100;
                        return true;
                    }))
                {
                    _activeSeconds[player] -= 60;
                    Refresh(player);
                }
            }
            catch (Exception e)
            {
                _activeSeconds[player] = 0;
                Log.Error($"Economy payroll failed for {player}: {e}");
            }
        }
    }

    private EntityUid? Body(NetUserId player)
    {
        return _mind.TryGetMind(player, out var mindId, out var mind) && mind.OwnedEntity is { } body && Exists(body) &&
               !HasComp<XenoComponent>(body) && _jobs.MindTryGetJob(mindId, out var job) && job.CmuEconomyEnabled
            ? body : null;
    }

    private bool Alive(EntityUid body) => TryComp<MobStateComponent>(body, out var state) && state.CurrentState != MobState.Dead;

    private List<(EntityUid Uid, StackComponent Stack)> Cash(EntityUid body)
    {
        var result = new List<(EntityUid, StackComponent)>();
        var visited = new HashSet<EntityUid>();
        void Visit(EntityUid entity)
        {
            if (!visited.Add(entity))
                return;
            if (TryComp<StackComponent>(entity, out var stack) && stack.StackTypeId == "Dollar" && !stack.Unlimited && stack.Count > 0)
                result.Add((entity, stack));
            if (!HasComp<ContainerManagerComponent>(entity))
                return;
            foreach (var container in _containers.GetAllContainers(entity))
            foreach (var child in container.ContainedEntities)
            {
                // A carried person is not part of the owner's cash inventory.
                if (!HasComp<MobStateComponent>(child))
                    Visit(child);
            }
        }
        Visit(body);
        return result;
    }

    private bool Deposit(Guid player, EntityUid body, string key, bool settlement)
    {
        var cash = Cash(body);
        var round = Store.ReadRound(player, RoundId);
        var amount = Math.Min(cash.Sum(c => (long) c.Stack.Count), round.SettlementCap - round.CashCredited);
        if (amount <= 0 && !settlement)
            return false;
        // No awaits: inventory cannot change between validation, DB commit and consumption.
        var success = Store.Mutate(player, RoundId, key, op =>
        {
            if (op.Round.Settled || !op.Round.DeploymentIssued)
                return false;
            if (amount > 0 && !op.Deposit(amount, settlement))
                return false;
            if (settlement)
                op.Round.Settled = true;
            return true;
        });
        if (!success)
            return false;
        foreach (var (uid, stack) in cash)
        {
            var take = (int) Math.Min(amount, stack.Count);
            if (take == 0)
                break;
            _stacks.SetCount(uid, stack.Count - take, stack);
            amount -= take;
        }
        return true;
    }

    private void OnRoundEnd(RoundEndMessageEvent args)
    {
        if (!Enabled)
            return;
        foreach (var player in _deployed.Keys)
        {
            try
            {
                var key = $"settlement:{RoundId}:{player}";
                if (Body(player) is { } body && Alive(body))
                    Deposit(player.UserId, body, key, true);
                else
                    Store.Mutate(player.UserId, RoundId, key, op =>
                    {
                        if (op.Round.Settled)
                            return false;
                        op.Round.Settled = true;
                        return true;
                    });
                Refresh(player);
                if (_players.TryGetSessionById(player, out var session))
                    Open(session);
            }
            catch (Exception e)
            {
                Log.Error($"Economy settlement failed for {player}: {e}");
            }
        }
        // Physical currency that still exists after settlements is lost when this world ends.
        long lostCash = 0;
        var cashQuery = EntityQueryEnumerator<StackComponent>();
        while (cashQuery.MoveNext(out _, out var cash))
            if (cash.StackTypeId == "Dollar" && !cash.Unlimited && cash.Count > 0)
                lostCash += cash.Count;
        var vendorQuery = EntityQueryEnumerator<AU14CashVendorComponent>();
        while (vendorQuery.MoveNext(out _, out var vendor))
            lostCash += vendor.InsertedCash;
        var shopQuery = EntityQueryEnumerator<AU14ShopkeeperVendorComponent>();
        while (shopQuery.MoveNext(out _, out var shop))
            lostCash += shop.InsertedCash;
        Store.RecordMetric($"lost:{RoundId}", RoundId, "LostCashAtRoundEnd", lostCash);
    }

    public void RecordStoreSink(long amount)
    {
        if (!Enabled || amount <= 0)
            return;
        try
        {
            Store.RecordMetric($"shop:{Guid.NewGuid()}", RoundId, "CashStoreSink", amount);
        }
        catch (Exception e)
        {
            Log.Error($"Economy store metric failed: {e}");
        }
    }

    public void Open(ICommonSession player, EntityUid? atm = null)
    {
        if (!Enabled || !_preferences.HavePreferencesLoaded(player))
            return;
        if (_open.Remove(player.UserId, out var old))
            old.Close();
        var eui = new CMUEconomyEui(this, atm);
        _open[player.UserId] = eui;
        _euis.OpenEui(eui, player);
    }

    public void Closed(NetUserId player, CMUEconomyEui eui)
    {
        if (_open.GetValueOrDefault(player) == eui)
            _open.Remove(player);
    }

    private void Refresh(NetUserId player)
    {
        if (_open.TryGetValue(player, out var eui))
            eui.StateDirty();
    }

    public bool CanUseAtm(ICommonSession player, EntityUid? atm)
    {
        return Enabled && _ticker.RunLevel == GameRunLevel.InRound && atm is { } machine && Exists(machine) &&
               HasComp<ColonyAtmComponent>(machine) && player.AttachedEntity is { } body && Body(player.UserId) == body &&
               Alive(body) && _blocker.CanInteract(body, machine) && _interaction.InRangeUnobstructed(body, machine);
    }

    public CMUEconomyState GetState(ICommonSession player, EntityUid? atm, Guid token, string status)
    {
        var account = Store.ReadAccount(player.UserId.UserId);
        var round = Store.ReadRound(player.UserId.UserId, RoundId);
        var preferences = _preferences.GetPreferences(player.UserId);
        var selected = account.Loadouts.GetValueOrDefault(preferences.SelectedCharacterIndex) ?? new List<string>();
        var items = ValidateItems(selected, account, null);
        var cost = items.Sum(i => i.PurchaseMode == CMUPurchaseMode.Permanent ? 0 : i.Price);
        var stake = account.StakeEnabled ? Math.Max(0, account.Balance - cost) * StakePercent / 100 : 0;
        if (StakeCap > 0)
            stake = Math.Min(stake, StakeCap);
        return new CMUEconomyState
        {
            Token = token, ProfileId = preferences.SelectedCharacterIndex,
            Character = preferences.SelectedCharacter.Name, PlayerId = player.UserId.ToString(),
            Balance = account.Balance, Stake = round.Stake, Cap = round.SettlementCap, Credited = round.CashCredited,
            StakeEnabled = account.StakeEnabled, Cost = cost, ProjectedStake = stake, ProjectedCap = stake * MultiplierPercent / 100,
            Atm = CanUseAtm(player, atm), Status = status,
            History = Loc.GetString("cmu-economy-statement", ("start", round.StartingBalance), ("final", account.Balance),
                ("profit", round.CashCredited - round.Stake)) + "\n" + string.Join("\n", Store.History(player.UserId.UserId).Select(e =>
                $"{e.Timestamp[..16]}  {Loc.GetString("cmu-economy-type-" + e.Type.ToLowerInvariant())}  {e.Amount:+#;-#;0}  → ${e.After}")),
            Items = _prototypes.EnumeratePrototypes<CMULoadoutItemPrototype>().OrderBy(i => i.Category).ThenBy(i => i.ID)
                .Select(i => new CMUEconomyItem
                {
                    Id = i.ID, Name = _prototypes.Index(i.Entity).Name, Category = i.Category,
                    Jobs = string.Join(", ", i.Jobs.Select(j => _prototypes.TryIndex<JobPrototype>(j, out var job) ? job.LocalizedName : j)),
                    Price = i.Price, Permanent = i.PurchaseMode == CMUPurchaseMode.Permanent,
                    Owned = account.Purchases.ContainsKey(i.ID), Selected = selected.Contains(i.ID),
                }).ToList(),
        };
    }

    public bool Handle(ICommonSession player, EntityUid? atm, CMUEconomyMessage message)
    {
        if (!Enabled || !_preferences.HavePreferencesLoaded(player))
            return false;
        var user = player.UserId.UserId;
        var key = $"ui:{user}:{message.Token}";
        var profile = _preferences.GetPreferences(player.UserId).SelectedCharacterIndex;
        if (message.Action is CMUEconomyAction.Save or CMUEconomyAction.Buy && message.ProfileId != profile)
            return false;
        switch (message.Action)
        {
            case CMUEconomyAction.Refresh:
                return true;
            case CMUEconomyAction.Stake:
                return Store.Mutate(user, RoundId, key, op => { op.Account.StakeEnabled = message.Amount != 0; return true; });
            case CMUEconomyAction.Save:
                if (message.Items.Count > 20)
                    return false;
                return Store.Mutate(user, RoundId, key, op =>
                {
                    var valid = ValidateItems(message.Items, op.Account, null);
                    if (valid.Count != message.Items.Count)
                        return false;
                    op.Account.Loadouts[profile] = valid.Select(i => i.ID).ToList();
                    return true;
                });
            case CMUEconomyAction.Buy:
                return _prototypes.TryIndex<CMULoadoutItemPrototype>(message.Target, out var item) &&
                       item.PurchaseMode == CMUPurchaseMode.Permanent &&
                       Store.Mutate(user, RoundId, key, op => op.Buy(item.ID, item.Price));
        }
        if (!CanUseAtm(player, atm) || player.AttachedEntity is not { } body)
            return false;
        var state = Store.ReadRound(user, RoundId);
        if (!state.DeploymentIssued || state.Settled)
            return false;
        switch (message.Action)
        {
            case CMUEconomyAction.Deposit:
                return Deposit(user, body, key, false);
            case CMUEconomyAction.Transfer:
                if (!Guid.TryParse(message.Target, out var target))
                    return false;
                return Store.Mutate(user, RoundId, key, op => op.Transfer(target, message.Amount));
            case CMUEconomyAction.Withdraw:
                if (message.Amount is <= 0 or > 100_000)
                    return false;
                var spawned = new List<EntityUid>();
                try
                {
                    var success = Store.Mutate(user, RoundId, key, op =>
                    {
                        if (!op.Change(-message.Amount, "CashWithdrawal", "ATM withdrawal"))
                            return false;
                        SpawnCash(body, (int) message.Amount, spawned);
                        return true;
                    });
                    if (!success)
                        return false;
                    foreach (var entity in spawned)
                        GiveItem(body, entity);
                    return true;
                }
                catch
                {
                    foreach (var entity in spawned)
                        if (Exists(entity))
                            Del(entity);
                    throw;
                }
        }
        return false;
    }
}
