using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Content.Server.Administration.Logs;
using Content.Server.AU14.Round;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Labels.Components;
using Content.Shared.Lock;
using Content.Shared.Paper;
using Content.Server.Store.Components;
using Content.Shared._RMC14.ARES;
using Content.Shared._RMC14.ARES.Logs;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._AU14.CCVar;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.AU14.util;
using Content.Shared.Cargo.Components;
using Content.Shared.Chasm;
using Content.Shared.Coordinates;
using Content.Shared.Database;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Random.Helpers;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using static Content.Shared._RMC14.Requisitions.Components.RequisitionsElevatorMode;

namespace Content.Server._RMC14.Requisitions;

public sealed partial class RequisitionsSystem : SharedRequisitionsSystem
{
    [Dependency] private IAdminLogManager _adminLogs = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private ChasmSystem _chasm = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private ARESCoreSystem _core = default!;
    [Dependency] private EntityStorageSystem _entityStorage = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PhysicsSystem _physics = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private XenoSystem _xeno = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private PricingSystem _pricing = default!;

    private static readonly EntProtoId AccountId = "RMCASRSAccount";
    private static readonly EntProtoId PaperRequisitionInvoice = "RMCPaperRequisitionInvoice";
    private static readonly EntProtoId<IFFFactionComponent> MarineFaction = "FactionMarine";

    private EntityQuery<ChasmComponent> _chasmQuery;
    private EntityQuery<ChasmFallingComponent> _chasmFallingQuery;
    private int _gain;
    private int _freeCratesXenoDivider;
    private bool _sellCargoRewards;

    private readonly HashSet<Entity<MobStateComponent>> _toPit = new();

    private static readonly EntProtoId<ARESLogTypeComponent> LogCat = "ARESTabRequisitionsLogs";

    public override void Initialize()
    {
        base.Initialize();

        _chasmQuery = GetEntityQuery<ChasmComponent>();
        _chasmFallingQuery = GetEntityQuery<ChasmFallingComponent>();
        SubscribeLocalEvent<ColonyAtmComponent, EntInsertedIntoContainerMessage>(OnMoneyInserted);

        SubscribeLocalEvent<RequisitionsComputerComponent, MapInitEvent>(OnComputerMapInit);
        SubscribeLocalEvent<RequisitionsComputerComponent, ComponentStartup>(OnComputerStartup);
        SubscribeLocalEvent<RequisitionsComputerComponent, BeforeActivatableUIOpenEvent>(OnComputerBeforeActivatableUIOpen);

        Subs.BuiEvents<RequisitionsComputerComponent>(RequisitionsUIKey.Key, subs =>
        {
            subs.Event<RequisitionsBuyMsg>(OnBuy);
            subs.Event<RequisitionsPlatformMsg>(OnPlatform);
        });

        Subs.CVar(_config, RMCCVars.RMCRequisitionsBalanceGain, v => _gain = v, true);
        Subs.CVar(_config, RMCCVars.RMCRequisitionsFreeCratesXenoDivider, v => _freeCratesXenoDivider = v, true);
        Subs.CVar(_config, AU14CCVars.SellCargoRewards, v => _sellCargoRewards = v, true);
    }

    private void OnComputerStartup(EntityUid uid, RequisitionsComputerComponent comp, ComponentStartup args)
    {
        ApplyPlatoonCatalogToComputer(uid, comp);
        ResetStock((uid, comp));
    }

    private void OnComputerMapInit(EntityUid uid, RequisitionsComputerComponent comp, MapInitEvent args)
    {
        // Assign a faction-specific account where applicable
        comp.Account = GetAccount(comp.Faction);

        // Also apply platoon catalog in case the console needs a custom catalog based on current round
        ApplyPlatoonCatalogToComputer(uid, comp);
        ResetStock((uid, comp));
        Dirty(uid, comp);
    }

    private void ApplyPlatoonCatalogToComputer(EntityUid consoleUid, RequisitionsComputerComponent comp)
    {
        if (comp == null)
            return;

        var faction = comp.Faction ?? "none";
        Log.Debug($"[Requisitions] Applying platoon catalog for console {consoleUid} faction={faction}");
        var platoonSys = EntityManager.EntitySysManager.GetEntitySystem<PlatoonSpawnRuleSystem>();
        var govPlatoon = platoonSys?.SelectedGovforPlatoon;
        var opPlatoon = platoonSys?.SelectedOpforPlatoon;

        PlatoonPrototype? chosenPlatoon = null;
        if (string.Equals(faction, "govfor", StringComparison.OrdinalIgnoreCase))
            chosenPlatoon = govPlatoon;
        else if (string.Equals(faction, "opfor", StringComparison.OrdinalIgnoreCase))
            chosenPlatoon = opPlatoon;

        if (chosenPlatoon == null)
        {
            Log.Debug($"[Requisitions] No chosen platoon found for faction {faction}");
            return;
        }

        var catalogProtoId = chosenPlatoon.Reqlist;
        Log.Debug($"[Requisitions] Chosen platoon ID {chosenPlatoon.ID} reqlist={catalogProtoId}");
        if (string.IsNullOrEmpty(catalogProtoId))
            return;

        if (!_prototypeManager.TryIndex<EntityPrototype>(catalogProtoId, out var catalogProto))
        {
            Log.Debug($"[Requisitions] Catalog prototype {catalogProtoId} not found");
            return;
        }

        if (!catalogProto.Components.TryGetValue("RequisitionsComputer", out var compEntry))
        {
            Log.Debug($"[Requisitions] Catalog prototype {catalogProtoId} has no RequisitionsComputer component");
            return;
        }

        if (compEntry.Component is not RequisitionsComputerComponent catalogComp)
        {
            Log.Debug($"[Requisitions] Catalog prototype {catalogProtoId} RequisitionsComputer component has unexpected type");
            return;
        }

        comp.Categories = catalogComp.Categories != null
            ? new List<RequisitionsCategory>(catalogComp.Categories)
            : new List<RequisitionsCategory>();

        Dirty(consoleUid, comp);
        Log.Debug($"[Requisitions] Applied catalog {catalogProtoId} to console {consoleUid}");
    }

    private void OnComputerBeforeActivatableUIOpen(Entity<RequisitionsComputerComponent> computer, ref BeforeActivatableUIOpenEvent args)
    {
        SetUILastInteracted(computer);
        SendUIState(computer);
    }

    private void OnBuy(Entity<RequisitionsComputerComponent> computer, ref RequisitionsBuyMsg args)
    {
        var actor = args.Actor;
        if (args.Category >= computer.Comp.Categories.Count)
        {
            Log.Error($"Player {ToPrettyString(actor)} tried to buy out of bounds requisitions order: category {args.Category}");
            return;
        }

        var category = computer.Comp.Categories[args.Category];
        if (args.Order >= category.Entries.Count)
        {
            Log.Error($"Player {ToPrettyString(actor)} tried to buy out of bounds requisitions order: category {args.Category}");
            return;
        }

        var order = category.Entries[args.Order];
        // Ensure we check the correct faction account for balance and cache it
        computer.Comp.Account ??= GetAccount(computer.Comp.Faction);
        var accountEnt = computer.Comp.Account.Value;
        if (!TryComp(accountEnt, out RequisitionsAccountComponent? account)
            || account.Balance < order.Cost)
            return;

        if (GetElevator(computer) is not { } elevator)
            return;

        if (IsFull(elevator))
            return;

        if (!TryTakeStock(computer, args.Category, args.Order, order))
            return;

        account.Balance -= order.Cost;
        elevator.Comp.Orders.Add(order);
        SendUIStateAll();
        _adminLogs.Add(LogType.RMCRequisitionsBuy, $"{ToPrettyString(args.Actor):actor} bought requisitions crate {order.Name} with crate {order.Crate} for {order.Cost}");

        if (!_prototypeManager.TryIndex<EntityPrototype>(order.Crate, out var prototype))
            return;

        _core.CreateARESLog(computer.Owner,
            LogCat,
            (string)$"{Name(actor)} bought {prototype.Name} for {order.Cost}$");
    }

    private void OnPlatform(Entity<RequisitionsComputerComponent> computer, ref RequisitionsPlatformMsg args)
    {
        if (GetElevator(computer) is not { } elevator)
            return;

        var comp = elevator.Comp;
        if (comp.NextMode != null || comp.Busy)
            return;

        if (comp.Mode == Lowering || comp.Mode == Raising)
            return;

        if (args.Raise && comp.Mode == Raised)
            return;

        if (!args.Raise && comp.Mode == Lowered)
            return;

        RequisitionsElevatorMode? nextMode = comp.Mode switch
        {
            Lowered => Raising,
            Raised => Lowering,
            _ => null
        };

        if (nextMode == null)
            return;

        if (nextMode == Lowering)
        {
            var mask = (int)(CollisionGroup.MobLayer | CollisionGroup.MobMask);
            foreach (var entity in _physics.GetEntitiesIntersectingBody(elevator, mask, false))
            {
                if (HasComp<MobStateComponent>(entity))
                    return;
            }
        }

        comp.ToggledAt = _timing.CurTime;
        comp.Busy = true;
        SetMode(elevator, Preparing, nextMode);
        Dirty(elevator);

        if (nextMode == Raising)
            _core.CreateARESLog(computer.Owner, LogCat, (string)$"{Name(args.Actor)} raised the requisitions elevator");
        if (nextMode == Lowering)
            _core.CreateARESLog(computer.Owner, LogCat, (string)$"{Name(args.Actor)} lowered the requisitions elevator");
    }

    // Returns the first existing account matching faction, or creates a new one.
    // The original (no faction param) used a single global account, we replicate this.
    private Entity<RequisitionsAccountComponent> GetAccount(string? faction = null)
    {
        var factionKey = string.IsNullOrEmpty(faction) || faction == "none"
            ? "unassigned" // use the shared global account so we're not stealing from a faction
            : faction;
        var query = EntityQueryEnumerator<RequisitionsAccountComponent>();
        while (query.MoveNext(out var uid, out var account))
        {
            if (account.Faction == factionKey)
                return (uid, account);
        }

        return CreateAccount(factionKey);
    }

    private Entity<RequisitionsAccountComponent> CreateAccount(string faction)
    {
        var newAccount = Spawn(AccountId, MapCoordinates.Nullspace);
        var comp = EnsureComp<RequisitionsAccountComponent>(newAccount);
        comp.Faction = faction;

        // Faction specific starting balance
        switch (faction)
        {
            case "govfor":
            case "opfor":
                comp.Balance = 20000;
                break;
            case "colony":
                comp.Balance = 450;
                // Colony accounts should not receive random military deliveries (flares, batteries, etc.)
                comp.RandomCrates.Clear();
                break;
        }

        return (newAccount, comp);
    }

    private void UpdateRailings(Entity<RequisitionsElevatorComponent> elevator, RequisitionsRailingMode mode)
    {
        var coordinates = _transform.GetMapCoordinates(elevator);
        var railings = _lookup.GetEntitiesInRange<RequisitionsRailingComponent>(coordinates, elevator.Comp.Radius + 5);
        foreach (var railing in railings)
        {
            SetRailingMode(railing, mode);
        }
    }

    private void UpdateGears(Entity<RequisitionsElevatorComponent> elevator, RequisitionsGearMode mode)
    {
        var coordinates = _transform.GetMapCoordinates(elevator);
        var railings = _lookup.GetEntitiesInRange<RequisitionsGearComponent>(coordinates, elevator.Comp.Radius + 5);
        foreach (var railing in railings)
        {
            if (railing.Comp.Mode == mode)
                continue;

            railing.Comp.Mode = mode;
            Dirty(railing);
        }
    }

    private void SendUIFeedback(Entity<RequisitionsComputerComponent> computerEnt, string flavorText)
    {
        if (!TryComp(computerEnt, out RequisitionsComputerComponent? computerComp))
            return;

        _chatSystem.TrySendInGameICMessage(computerEnt,
            flavorText,
            InGameICChatType.Speak,
            ChatTransmitRange.GhostRangeLimit,
            nameOverride: Loc.GetString("requisition-paperwork-receiver-name"));

        _audio.PlayPvs(computerComp.IncomingSurplus, computerEnt);
    }

    private void SendUIFeedback(string flavorText)
    {
        var query = EntityQueryEnumerator<RequisitionsComputerComponent>();
        while (query.MoveNext(out var uid, out var computer))
        {
            if (computer.IsLastInteracted)
                SendUIFeedback((uid, computer), flavorText);
        }
    }

    private void SetUILastInteracted(Entity<RequisitionsComputerComponent> computerEnt)
    {
        var query = EntityQueryEnumerator<RequisitionsComputerComponent>();
        while (query.MoveNext(out _, out var otherComputer))
        {
            otherComputer.IsLastInteracted = false;
        }

        if (!TryComp(computerEnt, out RequisitionsComputerComponent? selectedComputer))
            return;

        selectedComputer.IsLastInteracted = true;
    }

    private void TryPlayAudio(Entity<RequisitionsElevatorComponent> elevator)
    {
        var comp = elevator.Comp;
        if (comp.Audio != null)
            return;

        var time = _timing.CurTime;
        if (comp.NextMode == Lowering || comp.Mode == Lowering)
        {
            if (time < comp.ToggledAt + comp.LowerSoundDelay)
                return;

            comp.Audio = _audio.PlayPvs(comp.LoweringSound, elevator)?.Entity;
            return;
        }

        if (comp.NextMode == Raising || comp.Mode == Raising)
        {
            if (time < comp.ToggledAt + comp.RaiseSoundDelay)
                return;

            comp.Audio = _audio.PlayPvs(comp.RaisingSound, elevator)?.Entity;
        }
    }

    private void SetMode(Entity<RequisitionsElevatorComponent> elevator, RequisitionsElevatorMode mode, RequisitionsElevatorMode? nextMode)
    {
        elevator.Comp.Mode = mode;
        elevator.Comp.NextMode = nextMode;
        Dirty(elevator);

        RequisitionsGearMode? gearMode = mode switch
        {
            Lowered or Raised or Preparing => RequisitionsGearMode.Static,
            Lowering or Raising => RequisitionsGearMode.Moving,
            _ => null
        };

        if (gearMode != null)
            UpdateGears(elevator, gearMode.Value);

        RequisitionsRailingMode? railingMode = (mode, nextMode) switch
        {
            (Lowered, _) => RequisitionsRailingMode.Raised,
            (Raised, _) => RequisitionsRailingMode.Lowering,
            (_, Lowering) => RequisitionsRailingMode.Raising,
            _ => null
        };

        if (railingMode != null)
            UpdateRailings(elevator, railingMode.Value);

        SendUIStateAll();
    }

    private void SpawnOrders(Entity<RequisitionsElevatorComponent> elevator)
    {
        var comp = elevator.Comp;
        if (comp.Mode == Raised)
        {
            var coordinates = _transform.GetMoverCoordinates(elevator);
            var xOffset = comp.Radius;
            var yOffset = comp.Radius;
            int remainingDeliveries = GetElevatorCapacity(elevator);
            foreach (var order in comp.Orders)
            {
                var crate = SpawnAtPosition(order.Crate, coordinates.Offset(new Vector2(xOffset, yOffset)));
                remainingDeliveries--;

                foreach (var prototype in order.Entities)
                {
                    var entity = Spawn(prototype, MapCoordinates.Nullspace);
                    _entityStorage.Insert(entity, crate);
                }

                // If this order came from a department console, attach a department note
                // instead of the generic invoice so it shows on the crate label.
                if (order.DeptName != null)
                {
                    ApplyDepartmentCrateMetadata(crate, coordinates, order);
                }
                else
                {
                    PrintInvoice(crate, coordinates, PaperRequisitionInvoice);
                }

                yOffset--;
                if (yOffset < -comp.Radius)
                {
                    yOffset = comp.Radius;
                    xOffset--;
                }

                if (xOffset < -comp.Radius)
                    xOffset = comp.Radius;
            }

            comp.Orders.Clear();

            var query = EntityQueryEnumerator<RequisitionsCustomDeliveryComponent>();

            while (query.MoveNext(out var entityUid, out _))
            {
                // If elevator is full, abort and break out of the loop. Any remaining custom deliveries will be on
                // the next elevator shipment.
                if (remainingDeliveries <= 0)
                    break;

                // Remove the component so it doesn't get "delivered" again next elevator cycle.
                RemCompDeferred<RequisitionsCustomDeliveryComponent>(entityUid);

                // Teleport to the spot.
                _transform.SetCoordinates(entityUid, coordinates.Offset(new Vector2(xOffset, yOffset)));
                remainingDeliveries--; // Decrement available delivery slots count.

                // Update the next spot to teleport to.
                yOffset--;
                if (yOffset < -comp.Radius)
                {
                    yOffset = comp.Radius;
                    xOffset--;
                }

                if (xOffset < -comp.Radius)
                    xOffset = comp.Radius;
            }
        }
    }

    private bool Sell(Entity<RequisitionsElevatorComponent> elevator)
    {
        var account = GetAccount(elevator.Comp.Faction);
        var entities = _lookup.GetEntitiesIntersecting(elevator);
        var soldAny = false;
        var rewards = 0;

        foreach (var entity in entities)
        {
            if (entity == elevator.Comp.Audio)
                continue;

            // Instead of blacklist, we use a whitelist to selectively control generated funds from selling
            // if (HasComp<CargoSellBlacklistComponent>(entity))
            //     continue;
            if (!HasComp<CargoSellWhitelistComponent>(entity))
            {
                QueueDel(entity);
                continue;
            }

            if (_sellCargoRewards)
            {
                var entRewards = SubmitInvoices(entity);
                if (TryComp(entity, out RequisitionsCrateComponent? crate))
                    entRewards += crate.Reward;
                else
                    entRewards += (int)Math.Round(_pricing.GetPrice(entity));

                if (entRewards > 0)
                    soldAny = true;

                rewards += entRewards;
            }
            else
                soldAny = true; // cvar is off, sell is allowed without rewards

            QueueDel(entity);
        }

        if (rewards > 0)
        {
            ChangeBudget(rewards, elevator.Comp.Faction);
            if (elevator.Comp.Faction != "colony")
            {
                SendUIFeedback(Loc.GetString("requisition-paperwork-reward-message", ("amount", rewards)));
            }
        }

        return soldAny;
    }

    private void GetCrateWeight(Entity<RequisitionsAccountComponent> account, Dictionary<EntProtoId, float> crates, out Entity<RequisitionsComputerComponent> computer)
    {
        // TODO RMC14 price scaling
        computer = default;
        var computers = EntityQueryEnumerator<RequisitionsComputerComponent>();
        while (computers.MoveNext(out var uid, out var comp))
        {
            // Prefer computers whose account matches this account entity reference
            if (comp.Account != account)
                continue;

            computer = (uid, comp);
            foreach (var category in comp.Categories)
            {
                foreach (var entry in category.Entries)
                {
                    if (crates.ContainsKey(entry.Crate))
                        crates[entry.Crate] = 10000f / entry.Cost;
                }
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        var updateUI = false;
        var accounts = EntityQueryEnumerator<RequisitionsAccountComponent>();
        while (accounts.MoveNext(out var uid, out var account))
        {
            // Disabled periodic budget gain
            // if (time > account.NextGain)
            // {
            //     account.NextGain = time + account.GainEvery;
            //     account.Balance += _gain;
            //     Dirty(uid, account);
            //     updateUI = true;
            // }

            var xenos = _xeno.GetGroundXenosAlive();
            var randomCrates = CollectionsMarshal.AsSpan(account.RandomCrates);
            foreach (ref var pool in randomCrates)
            {
                if (pool.Next == default)
                    pool.Next = time + pool.Every;

                if (pool.Next >= time)
                    continue;

                var crates = Math.Max(0, Math.Sqrt((float)xenos / _freeCratesXenoDivider));

                if (crates < pool.Minimum && pool.Given < pool.MinimumFor)
                    crates = pool.Minimum;

                pool.Next = time + pool.Every;
                pool.Given++;
                pool.Fraction = crates - (int)crates;

                if (pool.Fraction >= 1)
                {
                    var add = (int)pool.Fraction;
                    pool.Fraction = pool.Fraction - add;
                    crates += add;
                }

                if (crates < 1)
                    continue;

                var crateCosts = new Dictionary<EntProtoId, float>();
                foreach (var choice in pool.Choices)
                {
                    crateCosts[choice] = 0;
                }

                if (crateCosts.Count == 0)
                    continue;

                GetCrateWeight((uid, account), crateCosts, out var computer);
                if (computer == default)
                    continue;

                if (GetElevator(computer) is not { } elevator)
                    continue;

                for (var i = 0; i < crates; i++)
                {
                    var crate = _random.Pick(crateCosts);
                    elevator.Comp.Orders.Add(new RequisitionsEntry { Crate = crate });
                }
            }
        }

        var computers = EntityQueryEnumerator<RequisitionsComputerComponent>();
        while (computers.MoveNext(out var uid, out var computer))
        {
            if (ProcessStock((uid, computer), time))
                updateUI = true;
        }

        var elevators = EntityQueryEnumerator<RequisitionsElevatorComponent>();
        while (elevators.MoveNext(out var uid, out var elevator))
        {
            if (ProcessElevator((uid, elevator)))
                updateUI = true;

            if (!_chasmQuery.TryComp(uid, out var chasm))
                continue;

            if (time < elevator.NextChasmCheck)
                continue;

            elevator.NextChasmCheck = time + elevator.ChasmCheckEvery;

            if (_net.IsClient)
                continue;

            if (elevator.Mode != Raised && elevator.Mode != Preparing)
            {
                _toPit.Clear();
                _lookup.GetEntitiesInRange(uid.ToCoordinates(), elevator.Radius + 0.25f, _toPit);

                foreach (var toPit in _toPit)
                {
                    if (_chasmFallingQuery.HasComp(toPit))
                        continue;

                    _chasm.StartFalling(uid, chasm, toPit);
                    _audio.PlayEntity(chasm.FallingSound, toPit, uid);
                }
            }
        }

        if (updateUI)
            SendUIStateAll();
    }

    private void ResetStock(Entity<RequisitionsComputerComponent> computer)
    {
        computer.Comp.Stock.Clear();
        EnsureStockEntries(computer, _timing.CurTime);
    }

    private static bool IsLimitedStock(RequisitionsEntry entry)
    {
        return entry.MaxStock > 0;
    }

    private void EnsureStockEntries(Entity<RequisitionsComputerComponent> computer, TimeSpan time)
    {
        var validKeys = new HashSet<(int Category, int Order)>();

        for (var categoryIndex = 0; categoryIndex < computer.Comp.Categories.Count; categoryIndex++)
        {
            var category = computer.Comp.Categories[categoryIndex];
            for (var orderIndex = 0; orderIndex < category.Entries.Count; orderIndex++)
            {
                var entry = category.Entries[orderIndex];
                if (!IsLimitedStock(entry))
                    continue;

                var key = (categoryIndex, orderIndex);
                validKeys.Add(key);
                if (computer.Comp.Stock.ContainsKey(key))
                    continue;

                var maxStock = Math.Max(0, entry.MaxStock);
                var startingStock = entry.StartingStock < 0
                    ? maxStock
                    : Math.Clamp(entry.StartingStock, 0, maxStock);

                computer.Comp.Stock[key] = new RequisitionsStockStatus
                {
                    Current = startingStock,
                    NextReplenish = startingStock < maxStock ? GetNextStockReplenish(time, entry) : TimeSpan.Zero,
                };
            }
        }

        foreach (var key in computer.Comp.Stock.Keys.ToArray())
        {
            if (!validKeys.Contains(key))
                computer.Comp.Stock.Remove(key);
        }
    }

    private TimeSpan GetNextStockReplenish(TimeSpan time, RequisitionsEntry entry)
    {
        return time + (entry.StockReplenishDelay > TimeSpan.Zero
            ? entry.StockReplenishDelay
            : TimeSpan.Zero);
    }

    private bool TryTakeStock(
        Entity<RequisitionsComputerComponent> computer,
        int category,
        int order,
        RequisitionsEntry entry)
    {
        if (!IsLimitedStock(entry))
            return true;

        EnsureStockEntries(computer, _timing.CurTime);
        var key = (category, order);
        if (!computer.Comp.Stock.TryGetValue(key, out var stock) ||
            stock.Current <= 0)
        {
            return false;
        }

        stock.Current--;
        if (stock.Current < entry.MaxStock && stock.NextReplenish == TimeSpan.Zero)
            stock.NextReplenish = GetNextStockReplenish(_timing.CurTime, entry);

        return true;
    }

    public bool TryReserveStock(Entity<RequisitionsComputerComponent> computer, int category, int order)
    {
        if (category < 0 ||
            category >= computer.Comp.Categories.Count)
        {
            return false;
        }

        var requisitionsCategory = computer.Comp.Categories[category];
        if (order < 0 ||
            order >= requisitionsCategory.Entries.Count)
        {
            return false;
        }

        var entry = requisitionsCategory.Entries[order];
        if (!IsLimitedStock(entry))
            return true;

        if (!TryTakeStock(computer, category, order, entry))
            return false;

        SendUIStateAll();
        return true;
    }

    private bool ProcessStock(Entity<RequisitionsComputerComponent> computer, TimeSpan time)
    {
        EnsureStockEntries(computer, time);

        var updateUi = false;
        var waitingForStock = false;
        for (var categoryIndex = 0; categoryIndex < computer.Comp.Categories.Count; categoryIndex++)
        {
            var category = computer.Comp.Categories[categoryIndex];
            for (var orderIndex = 0; orderIndex < category.Entries.Count; orderIndex++)
            {
                var entry = category.Entries[orderIndex];
                if (!IsLimitedStock(entry))
                    continue;

                var key = (categoryIndex, orderIndex);
                if (!computer.Comp.Stock.TryGetValue(key, out var stock))
                    continue;

                if (stock.Current >= entry.MaxStock)
                {
                    stock.NextReplenish = TimeSpan.Zero;
                    continue;
                }

                waitingForStock = true;
                if (stock.NextReplenish == TimeSpan.Zero)
                    stock.NextReplenish = GetNextStockReplenish(time, entry);

                if (entry.StockReplenishDelay <= TimeSpan.Zero)
                {
                    stock.Current = entry.MaxStock;
                    stock.NextReplenish = TimeSpan.Zero;
                    updateUi = true;
                    continue;
                }

                while (stock.Current < entry.MaxStock &&
                       time >= stock.NextReplenish)
                {
                    stock.Current = Math.Min(entry.MaxStock, stock.Current + Math.Max(1, entry.StockReplenishAmount));
                    updateUi = true;

                    if (stock.Current >= entry.MaxStock)
                    {
                        stock.NextReplenish = TimeSpan.Zero;
                        break;
                    }

                    stock.NextReplenish += entry.StockReplenishDelay;
                }
            }
        }

        if (waitingForStock && time >= computer.Comp.NextStockUiUpdate)
        {
            computer.Comp.NextStockUiUpdate = time + TimeSpan.FromSeconds(1);
            updateUi = true;
        }

        return updateUi;
    }

    protected override List<RequisitionsStockInfo> GetStockInfo(Entity<RequisitionsComputerComponent> computer)
    {
        var time = _timing.CurTime;
        EnsureStockEntries(computer, time);

        var stockInfo = new List<RequisitionsStockInfo>();
        for (var categoryIndex = 0; categoryIndex < computer.Comp.Categories.Count; categoryIndex++)
        {
            var category = computer.Comp.Categories[categoryIndex];
            for (var orderIndex = 0; orderIndex < category.Entries.Count; orderIndex++)
            {
                var entry = category.Entries[orderIndex];
                if (!IsLimitedStock(entry))
                    continue;

                var key = (categoryIndex, orderIndex);
                if (!computer.Comp.Stock.TryGetValue(key, out var stock))
                    continue;

                var secondsUntilReplenish = 0;
                if (stock.Current < entry.MaxStock && stock.NextReplenish > time)
                    secondsUntilReplenish = (int) Math.Ceiling((stock.NextReplenish - time).TotalSeconds);

                stockInfo.Add(new RequisitionsStockInfo(
                    categoryIndex,
                    orderIndex,
                    stock.Current,
                    entry.MaxStock,
                    secondsUntilReplenish));
            }
        }

        return stockInfo;
    }

    private bool ProcessElevator(Entity<RequisitionsElevatorComponent> ent)
    {
        var time = _timing.CurTime;
        var elevator = ent.Comp;
        if (time > elevator.ToggledAt + elevator.ToggleDelay)
        {
            elevator.ToggledAt = null;
            elevator.Busy = false;
            Dirty(ent);
            SendUIStateAll();
            return false;
        }

        if (elevator.ToggledAt == null)
            return false;

        TryPlayAudio(ent);

        var delay = elevator.NextMode == Raising ? elevator.RaiseDelay : elevator.LowerDelay;
        if (elevator.Mode == Preparing &&
            elevator.NextMode != null &&
            time > elevator.ToggledAt + delay)
        {
            SetMode(ent, elevator.NextMode.Value, null);
            return false;
        }

        if (elevator.Mode != Lowering && elevator.Mode != Raising)
            return false;

        var startDelay = delay + elevator.NextMode switch
        {
            Lowering => elevator.LowerDelay,
            Raising => elevator.RaiseDelay,
            _ => TimeSpan.Zero,
        };

        var moveDelay = startDelay + elevator.Mode switch
        {
            Lowering => elevator.LowerDelay,
            Raising => elevator.RaiseDelay,
            _ => TimeSpan.Zero,
        };

        if (time > elevator.ToggledAt + moveDelay)
        {
            elevator.Audio = null;

            var mode = elevator.Mode switch
            {
                Raising => Raised,
                Lowering => Lowered,
                _ => elevator.Mode,
            };
            SetMode(ent, mode, elevator.NextMode);

            SpawnOrders(ent);

            return true;
        }

        if (elevator.Mode == Lowering &&
            time > elevator.ToggledAt + delay)
        {
            if (Sell(ent))
                return true;
        }

        return false;
    }

    private void OnMoneyInserted(EntityUid uid, ColonyAtmComponent comp, EntInsertedIntoContainerMessage args)
    {
        int stackCount = 1;
        if (TryComp(args.Entity, out StackComponent? stack))
            stackCount = stack.Count;

        // Add to requisitions budget for each item in the stack
        _adminLogs.Add(LogType.RMCRequisitionsBuy, $"ATM submission: +{stackCount} to requisitions budget");

        // Try to credit a faction-specific account near the ATM first (prefer elevator, then computer)
        string? faction = null;
        var coords = _transform.GetMapCoordinates(uid);

        // Search for nearby elevators with a faction
        var nearbyElevators = _lookup.GetEntitiesInRange<RequisitionsElevatorComponent>(coords, 10);
        Entity<RequisitionsElevatorComponent>? nearestElevator = null;
        var nearestElevatorDist = float.MaxValue;
        foreach (var elev in nearbyElevators)
        {
            var elevCoords = _transform.GetMapCoordinates(elev);
            if (coords.MapId != elevCoords.MapId)
                continue;
            if (string.IsNullOrEmpty(elev.Comp.Faction) || elev.Comp.Faction == "none")
                continue;
            var d = (elevCoords.Position - coords.Position).LengthSquared();
            if (d < nearestElevatorDist)
            {
                nearestElevator = elev;
                nearestElevatorDist = d;
            }
        }

        if (nearestElevator != null)
        {
            faction = nearestElevator.Value.Comp.Faction;
        }
        else
        {
            // If no elevator found, search for nearby computers with a faction
            var nearbyComputers = _lookup.GetEntitiesInRange<RequisitionsComputerComponent>(coords, 10);
            Entity<RequisitionsComputerComponent>? nearestComputer = null;
            var nearestComputerDist = float.MaxValue;
            foreach (var compEnt in nearbyComputers)
            {
                var compCoords = _transform.GetMapCoordinates(compEnt);
                if (coords.MapId != compCoords.MapId)
                    continue;
                if (string.IsNullOrEmpty(compEnt.Comp.Faction) || compEnt.Comp.Faction == "none")
                    continue;
                var d = (compCoords.Position - coords.Position).LengthSquared();
                if (d < nearestComputerDist)
                {
                    nearestComputer = compEnt;
                    nearestComputerDist = d;
                }
            }

            if (nearestComputer != null)
                faction = nearestComputer.Value.Comp.Faction;
        }

        Entity<RequisitionsAccountComponent> reqAccount;
        if (!string.IsNullOrEmpty(faction) && faction != "none")
            reqAccount = GetAccount(faction);
        else
        {
            Log.Debug($"[Requisitions] No faction specified for GetAccount, faction: {faction}, using \"unassigned\" account.");
            reqAccount = GetAccount();
        }

        reqAccount.Comp.Balance += stackCount;
        Dirty(reqAccount);

        QueueDel(args.Entity);
        SendUIStateAll();
    }

    public void ReapplyPlatoonCatalogs()
    {
        Log.Debug("[Requisitions] Reapplying platoon catalogs to all consoles");
        var computers = EntityQueryEnumerator<RequisitionsComputerComponent>();
        while (computers.MoveNext(out var uid, out var comp))
        {
            ApplyPlatoonCatalogToComputer(uid, comp);
            ResetStock((uid, comp));
            Dirty(uid, comp);
        }
    }

    /// <summary>
    ///     Locks a crate to the department's access and attaches a paper note with order info.
    /// </summary>
    private void ApplyDepartmentCrateMetadata(EntityUid crate, EntityCoordinates coordinates, RequisitionsEntry order)
    {
        var lockSys = EntityManager.System<LockSystem>();
        var accessSys = EntityManager.System<AccessReaderSystem>();

        // Lock the crate with department access
        if (!string.IsNullOrEmpty(order.DeptAccessLevel))
        {
            var accessReader = EnsureComp<AccessReaderComponent>(crate);
            accessSys.SetAccesses((crate, accessReader),
                new List<HashSet<ProtoId<AccessLevelPrototype>>>
                {
                    new() { order.DeptAccessLevel }
                });

            var lockComp = EnsureComp<LockComponent>(crate);
            lockSys.Lock(crate, null, lockComp);
        }

        // Spawn a requisition invoice with department order information and attach as label
        var noteContent =
            $"[head=2]{order.DeptName ?? "Department"} Order[/head]\n" +
            $"[bold]Ordered by:[/bold] {order.DeptOrderedBy ?? "Unknown"}\n" +
            $"[bold]Reason:[/bold] {order.DeptReason ?? "N/A"}\n" +
            $"[bold]Deliver to:[/bold] {order.DeptDeliverTo ?? "N/A"}";

        var paper = Spawn(PaperRequisitionInvoice, coordinates);
        if (TryComp<PaperComponent>(paper, out var paperComp))
        {
            _metaSystem.SetEntityName(paper, $"{order.DeptName ?? "Dept."} Order Note");
            _paperSystem.SetContent((paper, paperComp), noteContent);
        }

        // Attach the note as a paper label on the crate
        if (TryComp<PaperLabelComponent>(crate, out var label))
        {
            _slots.TryInsert(crate, label.LabelSlot, paper, null);
        }
    }

    /// <summary>
    /// Attempts to deduct <paramref name="amount"/> from the specified faction's requisitions account.
    /// Returns false if the account has insufficient funds.
    /// </summary>
    public bool TrySpendFunds(string faction, int amount)
    {
        var account = GetAccount(faction);
        if (!TryComp(account, out RequisitionsAccountComponent? comp) || comp.Balance < amount)
            return false;

        comp.Balance -= amount;
        return true;
    }
}
