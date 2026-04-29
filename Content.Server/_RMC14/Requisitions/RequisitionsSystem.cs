using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Content.Server.Administration.Logs;
using Content.Server.AU14.Round;
using Content.Server.Cargo.Components;
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
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids;
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
    [Dependency] private readonly IAdminLogManager _adminLogs = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ChasmSystem _chasm = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private static readonly EntProtoId AccountId = "RMCASRSAccount";
    private static readonly EntProtoId PaperRequisitionInvoice = "RMCPaperRequisitionInvoice";
    private static readonly EntProtoId<IFFFactionComponent> MarineFaction = "FactionMarine";

    private EntityQuery<ChasmComponent> _chasmQuery;
    private EntityQuery<ChasmFallingComponent> _chasmFallingQuery;
    private int _gain;
    private int _freeCratesXenoDivider;

    private readonly HashSet<Entity<MobStateComponent>> _toPit = new();

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

    }

    private void OnComputerStartup(EntityUid uid, RequisitionsComputerComponent comp, ComponentStartup args)
    {
        ApplyPlatoonCatalogToComputer(uid, comp);
    }

    private void OnComputerMapInit(EntityUid uid, RequisitionsComputerComponent comp, MapInitEvent args)
    {
        // Assign a faction-specific account where applicable
        comp.Account = GetAccount(comp.Faction);

        // Also apply platoon catalog in case the console needs a custom catalog based on current round
        ApplyPlatoonCatalogToComputer(uid, comp);
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
        // Ensure we check the correct faction account for balance
        var accountEnt = computer.Comp.Account ?? GetAccount(computer.Comp.Faction);
        if (!TryComp(accountEnt, out RequisitionsAccountComponent? account) ||
            account.Balance < order.Cost)
        {
            return;
        }

        if (GetElevator(computer) is not { } elevator)
            return;

        if (IsFull(elevator))
            return;

        account.Balance -= order.Cost;
        elevator.Comp.Orders.Add(order);
        SendUIStateAll();
        _adminLogs.Add(LogType.RMCRequisitionsBuy, $"{ToPrettyString(args.Actor):actor} bought requisitions crate {order.Name} with crate {order.Crate} for {order.Cost}");
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
            var mask = (int) (CollisionGroup.MobLayer | CollisionGroup.MobMask);
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
    }

    // Returns the first existing account matching faction, or creates a new one.
    // If faction is null or "none", behaves like the original GetAccount (single global account).
    private Entity<RequisitionsAccountComponent> GetAccount(string? faction = null)
     {
        var query = EntityQueryEnumerator<RequisitionsAccountComponent>();

        // Prefer an account matching faction if provided
        if (!string.IsNullOrEmpty(faction) && faction != "none")
        {
            while (query.MoveNext(out var uid, out var account))
            {
                if (account.Faction == faction)
                    return (uid, account);
            }
            // No matching account found, spawn a new faction account
            var newAccount = Spawn(AccountId, MapCoordinates.Nullspace);
            var newAccountComp = EnsureComp<RequisitionsAccountComponent>(newAccount);
            newAccountComp.Faction = faction;

            // Set faction-specific starting balance
            if (faction == "govfor" || faction == "opfor")
            {
                newAccountComp.Balance = 20000;
            }
            else if (faction == "colony")
            {
                newAccountComp.Balance = 450;
                // Colony accounts should not receive random military deliveries (flares, batteries, etc.)
                newAccountComp.RandomCrates.Clear();
            }

            return (newAccount, newAccountComp);
        }

        // Fallback to the old behavior: return any existing account or create one
        while (query.MoveNext(out var anyUid, out var anyAccount))
        {
            return (anyUid, anyAccount);
        }

        var created = Spawn(AccountId, MapCoordinates.Nullspace);
        var createdComp = EnsureComp<RequisitionsAccountComponent>(created);
        return (created, createdComp);
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
        // Deposits from selling items go to the elevator's nearest account based on faction
        var account = GetAccount(elevator.Comp.Faction);
         var entities = _lookup.GetEntitiesIntersecting(elevator);
         var soldAny = false;
         var rewards = 0;
         foreach (var entity in entities)
         {
             if (entity == elevator.Comp.Audio)
                 continue;

             if (HasComp<CargoSellBlacklistComponent>(entity))
                 continue;

             rewards += SubmitInvoices(entity);

             if (TryComp(entity, out RequisitionsCrateComponent? crate))
             {
                 rewards += crate.Reward;
                 soldAny = true;
             }

             QueueDel(entity);
         }

         // Colony ASRS does not receive budget or feedback for selling items
         if (elevator.Comp.Faction != "colony")
         {
             if (rewards > 0)
                 SendUIFeedback(Loc.GetString("requisition-paperwork-reward-message", ("amount", rewards)));

             account.Comp.Balance += rewards;
         }

         if (soldAny)
             Dirty(account);

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

                var crates = Math.Max(0, Math.Sqrt((float) xenos / _freeCratesXenoDivider));

                if (crates < pool.Minimum && pool.Given < pool.MinimumFor)
                    crates = pool.Minimum;

                pool.Next = time + pool.Every;
                pool.Given++;
                pool.Fraction = crates - (int) crates;

                if (pool.Fraction >= 1)
                {
                    var add = (int) pool.Fraction;
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
            reqAccount = GetAccount();

        reqAccount.Comp.Balance += stackCount;
        Dirty(reqAccount);

        EntityManager.QueueDeleteEntity(args.Entity);
        SendUIStateAll();
    }

    public void ReapplyPlatoonCatalogs()
    {
        Log.Debug("[Requisitions] Reapplying platoon catalogs to all consoles");
        var computers = EntityQueryEnumerator<RequisitionsComputerComponent>();
        while (computers.MoveNext(out var uid, out var comp))
        {
            ApplyPlatoonCatalogToComputer(uid, comp);
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
}
