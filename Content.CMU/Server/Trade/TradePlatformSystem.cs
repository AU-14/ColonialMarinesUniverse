// SPDX-License-Identifier: AGPL-3.0-only
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.Popups;
using Content.Shared._RMC14.Areas;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Trade;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Trade;

// The trade platform. A parcel put on the pad is sold, and a purchase is left on it, both by a drone that comes ten seconds after the call, and only to a pad under the open sky.
// The drone settles down on the pad, takes the parcel (and pays for it to the card of the seller, less the tax of the colony) or leaves a parcel (paid for from the card of the buyer),
// and rises and is gone.
//
// What is bought and sold is told by the contracts put into the platform (nine at most): only what a contract lists is paid for, and the goods of the contract are sold, those that
// its level has opened. The trade through a contract is its points, and the points are its level.
public sealed class TradePlatformSystem : EntitySystem
{
    [Dependency] private readonly AdminConsoleSystem _admin = default!;
    [Dependency] private readonly AreaSystem _areas = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ColonyBankSystem _bank = default!;
    [Dependency] private readonly ColonyBudgetSystem _budget = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedIdCardSystem _idCards = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TradePricingSystem _prices = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedRoofSystem _roof = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private static readonly SoundSpecifier CallSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");
    private static readonly SoundSpecifier PaidSound = new SoundPathSpecifier("/Audio/Machines/chime.ogg");
    private static readonly SoundSpecifier DeniedSound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

    private static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(1);

    private readonly HashSet<Entity<TradeParcelComponent>> _parcels = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TradePlatformComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<TradePlatformComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<TradePlatformComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<TradePlatformComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<TradePlatformComponent, TradeSellMessage>(OnSell);
        SubscribeLocalEvent<TradePlatformComponent, TradeBuyMessage>(OnBuy);
        SubscribeLocalEvent<TradePlatformComponent, TradeEjectContractMessage>(OnEject);
        SubscribeLocalEvent<TradePlatformComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<TradeContractComponent, ExaminedEvent>(OnContractExamined);
    }

    private void OnInit(Entity<TradePlatformComponent> platform, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(platform, TradePlatformComponent.ContractsContainerId);
    }

    #region The contracts

    private BaseContainer ContractsOf(Entity<TradePlatformComponent> platform)
    {
        return _container.EnsureContainer<Container>(platform, TradePlatformComponent.ContractsContainerId);
    }

    // The contracts in the platform.
    public List<Entity<TradeContractComponent>> GetContracts(Entity<TradePlatformComponent> platform)
    {
        var found = new List<Entity<TradeContractComponent>>();
        foreach (var thing in ContractsOf(platform).ContainedEntities)
        {
            if (TryComp<TradeContractComponent>(thing, out var contract))
                found.Add((thing, contract));
        }

        return found;
    }

    private void OnInteractUsing(Entity<TradePlatformComponent> platform, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<TradeContractComponent>(args.Used, out var contract))
            return;

        args.Handled = true;

        if (platform.Comp.Order != null)
        {
            Deny(platform, args.User, "rumc-trade-busy");
            return;
        }

        var installed = GetContracts(platform);
        if (installed.Count >= platform.Comp.MaxContracts)
        {
            Deny(platform, args.User, "rumc-trade-contracts-full");
            return;
        }

        foreach (var other in installed)
        {
            if (other.Comp.Contract == contract.Contract)
            {
                Deny(platform, args.User, "rumc-trade-contract-present");
                return;
            }
        }

        if (!_hands.TryDrop(args.User, args.Used, checkActionBlocker: false, doDropInteraction: false) || !_container.Insert(args.Used, ContractsOf(platform)))
        {
            Deny(platform, args.User, "rumc-trade-contract-refused");
            return;
        }

        _audio.PlayPvs(PaidSound, platform);
        _popup.PopupEntity(Loc.GetString("rumc-trade-contract-installed", ("company", CompanyOf(contract))), platform, args.User);
        Refresh(platform);
    }

    private void OnEject(Entity<TradePlatformComponent> platform, ref TradeEjectContractMessage args)
    {
        var user = args.Actor;
        var contract = GetEntity(args.Contract);

        if (platform.Comp.Order != null)
        {
            Deny(platform, user, "rumc-trade-busy");
            return;
        }

        var contracts = ContractsOf(platform);
        if (!contracts.Contains(contract))
            return;

        _container.Remove(contract, contracts, destination: Transform(platform).Coordinates);
        _hands.TryPickupAnyHand(user, contract);
        Refresh(platform);
    }

    private void OnExamined(Entity<TradePlatformComponent> platform, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("rumc-trade-platform-examine", ("count", GetContracts(platform).Count), ("max", platform.Comp.MaxContracts)));
    }

    private void OnContractExamined(Entity<TradeContractComponent> contract, ref ExaminedEvent args)
    {
        if (!_proto.TryIndex(contract.Comp.Contract, out var proto))
            return;

        var level = proto.LevelOf(contract.Comp.Points);
        args.PushMarkup(Loc.GetString("rumc-trade-contract-examine",
            ("company", Loc.GetString(proto.Company)),
            ("category", Loc.GetString(proto.Category)),
            ("level", level),
            ("points", contract.Comp.Points)));

        if (proto.NextLevelPoints(contract.Comp.Points) is { } next)
            args.PushMarkup(Loc.GetString("rumc-trade-contract-examine-next", ("next", next)));
    }

    // The company with the category of the contract: the company has a contract for each kind of goods.
    private string CompanyLabel(TradeContractPrototype proto)
    {
        return Loc.GetString("rumc-trade-company-label", ("company", Loc.GetString(proto.Company)), ("category", Loc.GetString(proto.Category)));
    }

    private string CompanyOf(TradeContractComponent contract)
    {
        return _proto.TryIndex(contract.Contract, out var proto) ? CompanyLabel(proto) : contract.Contract.Id;
    }

    // What the trade of the sum brings to a contract: the points, and so the levels.
    private void AddPoints(EntityUid contract, double amount)
    {
        if (amount <= 0 || !TryComp<TradeContractComponent>(contract, out var comp))
            return;

        comp.Points += (int) Math.Floor(amount);
        Dirty(contract, comp);
    }

    #endregion

    #region The window

    private void OnOpened(Entity<TradePlatformComponent> platform, ref BoundUIOpenedEvent args)
    {
        Refresh(platform);
    }

    private void Refresh(Entity<TradePlatformComponent> platform)
    {
        var contracts = GetContracts(platform);
        var parcels = FindParcels(platform);

        double value = 0;
        foreach (var parcel in parcels)
            value += _prices.GetPrice(parcel, contracts);

        var tax = TaxPercent();
        var worth = (int) Math.Floor(value);
        var payout = worth - TaxOf(worth, tax);

        var contractEntries = new List<TradeContractEntry>();
        foreach (var contract in contracts)
        {
            if (!_proto.TryIndex(contract.Comp.Contract, out var proto))
                continue;

            contractEntries.Add(new TradeContractEntry(
                GetNetEntity(contract.Owner),
                CompanyLabel(proto),
                proto.LevelOf(contract.Comp.Points),
                contract.Comp.Points,
                proto.NextLevelPoints(contract.Comp.Points)));
        }

        _ui.SetUiState(platform.Owner, TradeUiKey.Key,
            new TradePlatformBuiState(parcels.Count, worth, (int) tax, payout, IsUnderOpenSky(platform), platform.Comp.Order != null, platform.Comp.MaxContracts, contractEntries, BuildOffers(contracts)));

        foreach (var actor in _ui.GetActors(platform.Owner, TradeUiKey.Key))
        {
            _ui.ServerSendUiMessage(platform.Owner, TradeUiKey.Key, new TradeBalanceMessage(GetBalance(actor)), actor);
        }
    }

    // The list of what the platform sells: the goods that need no contract, then the goods of each contract with those that its level has not opened yet locked.
    private List<TradeOfferEntry> BuildOffers(List<Entity<TradeContractComponent>> contracts)
    {
        var offers = new List<TradeOfferEntry>();

        foreach (var contract in contracts)
        {
            if (!_proto.TryIndex(contract.Comp.Contract, out var proto))
                continue;

            var level = proto.LevelOf(contract.Comp.Points);
            var company = CompanyLabel(proto);

            var lines = new List<(int Index, TradeGood Good, EntProtoId Product)>();
            for (var i = 0; i < proto.Goods.Count; i++)
            {
                var good = proto.Goods[i];
                if (good.Buy > 0 && ProductOf(good) is { } product)
                    lines.Add((i, good, product));
            }

            lines.Sort((a, b) =>
            {
                var byLevel = a.Good.Level.CompareTo(b.Good.Level);
                return byLevel != 0 ? byLevel : a.Good.Buy.CompareTo(b.Good.Buy);
            });

            foreach (var (index, good, product) in lines)
            {
                offers.Add(new TradeOfferEntry($"{proto.ID}:{index}", product, NameOf(product), good.Count, good.Buy, company, good.Level, good.Level > level));
            }
        }

        return offers;
    }

    // What a lot of the good is: its own product, or the entity of its stack.
    private EntProtoId? ProductOf(TradeGood good)
    {
        if (good.Product is { } product)
            return product;

        if (good.Stack != null && _proto.TryIndex<StackPrototype>(good.Stack, out var stack))
            return stack.Spawn;

        return null;
    }

    private string NameOf(EntProtoId product)
    {
        return _proto.TryIndex(product, out var proto) ? proto.Name : product.Id;
    }

    private int? GetBalance(EntityUid actor)
    {
        return _idCards.TryFindIdCard(actor, out var card) && card.Comp.AccountNumber != 0 ? card.Comp.AccountBalance : null;
    }

    #endregion

    #region The tax

    // The tax of the colony on the parcels, in percent.
    private float TaxPercent()
    {
        return Math.Clamp(_admin.GetTradeTax() * 100f, 0f, 50f);
    }

    // The part of the payout that goes to the colony.
    public static int TaxOf(int amount, float taxPercent)
    {
        return Math.Clamp((int) Math.Floor(amount * taxPercent / 100f), 0, amount);
    }

    #endregion

    #region The calls

    private void OnSell(Entity<TradePlatformComponent> platform, ref TradeSellMessage args)
    {
        var user = args.Actor;

        if (!CanCall(platform, user))
            return;

        var parcels = FindParcels(platform);
        if (parcels.Count == 0)
        {
            Deny(platform, user, "rumc-trade-no-parcel");
            return;
        }

        var contracts = GetContracts(platform);
        double value = 0;
        foreach (var parcel in parcels)
            value += _prices.GetPrice(parcel, contracts);

        if (value < 1)
        {
            Deny(platform, user, "rumc-trade-nothing-wanted");
            return;
        }

        if (!_idCards.TryFindIdCard(user, out var card) || card.Comp.AccountNumber == 0)
        {
            Deny(platform, user, "rumc-trade-no-card");
            return;
        }

        Call(platform, user, new TradeOrder
        {
            Kind = TradeOrderKind.Sell,
            ArriveAt = _timing.CurTime + platform.Comp.DroneDelay,
            Card = card.Owner,
        });
    }

    private void OnBuy(Entity<TradePlatformComponent> platform, ref TradeBuyMessage args)
    {
        var user = args.Actor;

        if (!CanCall(platform, user))
            return;

        if (!TryFindLot(platform, args.Offer, out var product, out var count, out var price, out var contract, out var locked))
            return;

        if (locked)
        {
            Deny(platform, user, "rumc-trade-locked");
            return;
        }

        if (!_idCards.TryFindIdCard(user, out var card) || card.Comp.AccountNumber == 0)
        {
            Deny(platform, user, "rumc-trade-no-card");
            return;
        }

        if (card.Comp.AccountBalance < price)
        {
            Deny(platform, user, "rumc-trade-no-money");
            return;
        }

        // Paid at the call, and given back if the drone cannot come.
        card.Comp.AccountBalance -= price;
        Dirty(card.Owner, card.Comp);
        _bank.RecordTransaction(card.Owner, AtmHistoryKind.Purchase, price);

        Call(platform, user, new TradeOrder
        {
            Kind = TradeOrderKind.Buy,
            ArriveAt = _timing.CurTime + platform.Comp.DroneDelay,
            Card = card.Owner,
            Offer = args.Offer,
            Product = product,
            Count = count,
            Paid = price,
            Contract = contract,
        });
    }

    // The lot of an offer of the list of the platform: what it is, how much of it, what it costs, and the contract that it is of and whether that contract has opened it.
    private bool TryFindLot(Entity<TradePlatformComponent> platform, string id, out string product, out int count, out int price, out EntityUid? contract, out bool locked)
    {
        product = string.Empty;
        count = 1;
        price = 0;
        contract = null;
        locked = false;

        var split = id.LastIndexOf(':');
        if (split <= 0 || !int.TryParse(id[(split + 1)..], out var index))
            return false;

        var contractId = id[..split];
        foreach (var installed in GetContracts(platform))
        {
            if (installed.Comp.Contract.Id != contractId || !_proto.TryIndex(installed.Comp.Contract, out var proto))
                continue;

            if (index < 0 || index >= proto.Goods.Count)
                return false;

            var good = proto.Goods[index];
            if (good.Buy <= 0 || ProductOf(good) is not { } made)
                return false;

            product = made;
            count = good.Count;
            price = good.Buy;
            contract = installed.Owner;
            locked = good.Level > proto.LevelOf(installed.Comp.Points);
            return true;
        }

        return false;
    }

    private bool CanCall(Entity<TradePlatformComponent> platform, EntityUid user)
    {
        if (platform.Comp.Order != null)
        {
            Deny(platform, user, "rumc-trade-busy");
            return false;
        }

        if (!IsUnderOpenSky(platform))
        {
            Deny(platform, user, "rumc-trade-no-sky");
            return false;
        }

        return true;
    }

    private void Call(Entity<TradePlatformComponent> platform, EntityUid user, TradeOrder order)
    {
        platform.Comp.Order = order;
        _audio.PlayPvs(CallSound, platform);
        _popup.PopupEntity(Loc.GetString("rumc-trade-called", ("seconds", (int) platform.Comp.DroneDelay.TotalSeconds)), platform, user);
        Refresh(platform);
    }

    private void Deny(Entity<TradePlatformComponent> platform, EntityUid user, string message)
    {
        _audio.PlayPvs(DeniedSound, platform);
        _popup.PopupEntity(Loc.GetString(message), platform, user, PopupType.SmallCaution);
    }

    #endregion

    #region The sky

    // The pad stands under the open sky: on a map with areas, an area that allows a drop; on any other map, no roof of the grid over it and no floor of a level above it.
    public bool IsUnderOpenSky(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        // On a map with areas (the planets of RMC, such as Stable Garrison Redux) the sky is what the area says, as it is for a supply drop: the area must allow it, and no roofing marker must cover the place.
        // The roof of the grid and the floors of the levels above are not looked at there. Such maps stack the same ground on the levels above (on Garrison about three quarters of the ground of the main level
        // has ground over it), and the roof system of the z-levels marks every tile that has a floor above it as rooved, so both would say that there is no sky over the whole colony.
        if (HasComp<AreaGridComponent>(gridUid))
            return _areas.CanSupplyDrop(_transform.ToMapCoordinates(xform.Coordinates));

        var tile = _maps.CoordinatesToTile(gridUid, grid, xform.Coordinates);

        if (TryComp<RoofComponent>(gridUid, out var roof) && _roof.IsRooved((gridUid, grid, roof), tile))
            return false;

        var position = _transform.ToMapCoordinates(xform.Coordinates).Position;

        var level = xform.MapUid;

        // Every level above is looked at: a floor of any of them is a roof.
        for (var depth = 0; depth < 32 && level is { } current; depth++)
        {
            if (!TryComp<CMUZLevelMapComponent>(current, out var z) || z.MapAbove is not { } above || !TryComp<MapComponent>(above, out var aboveMap))
                break;

            if (_maps.TryFindGridAt(new MapCoordinates(position, aboveMap.MapId), out var aboveGrid, out var aboveGridComp) &&
                _maps.TryGetTileRef(aboveGrid, aboveGridComp, _maps.TileIndicesFor(aboveGrid, aboveGridComp, new MapCoordinates(position, aboveMap.MapId)), out var aboveTile) &&
                !aboveTile.Tile.IsEmpty)
            {
                return false;
            }

            level = above;
        }

        return true;
    }

    #endregion

    #region The drone

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var platforms = EntityQueryEnumerator<TradePlatformComponent>();
        while (platforms.MoveNext(out var uid, out var platform))
        {
            if (platform.Order is { } order && platform.Drone == null && now >= order.ArriveAt)
                Launch((uid, platform));

            if (now >= platform.NextRefresh && _ui.IsUiOpen(uid, TradeUiKey.Key))
            {
                platform.NextRefresh = now + RefreshEvery;
                Refresh((uid, platform));
            }
        }

        var drones = EntityQueryEnumerator<TradeDroneComponent>();
        while (drones.MoveNext(out var uid, out var drone))
        {
            Advance((uid, drone), now);
        }
    }

    // The drone comes, if the pad is still under the open sky; if it is not, the call is called off.
    private void Launch(Entity<TradePlatformComponent> platform)
    {
        if (!IsUnderOpenSky(platform))
        {
            CallOff(platform, "rumc-trade-no-sky-arrival");
            return;
        }

        var drone = Spawn(platform.Comp.DronePrototype, Transform(platform).Coordinates);
        var comp = EnsureComp<TradeDroneComponent>(drone);
        comp.Platform = platform;
        comp.Phase = TradeDronePhase.Descend;
        comp.PhaseStart = _timing.CurTime;
        Dirty(drone, comp);

        platform.Comp.Drone = drone;
    }

    private void Advance(Entity<TradeDroneComponent> drone, TimeSpan now)
    {
        var comp = drone.Comp;

        if (!TryComp<TradePlatformComponent>(comp.Platform, out var platform))
        {
            QueueDel(drone);
            return;
        }

        var elapsed = now - comp.PhaseStart;
        switch (comp.Phase)
        {
            case TradeDronePhase.Descend when elapsed >= comp.DescendTime:
                comp.Phase = TradeDronePhase.Hover;
                comp.PhaseStart = now;
                Dirty(drone);
                Carry((comp.Platform, platform));
                break;

            case TradeDronePhase.Hover when elapsed >= comp.HoverTime:
                comp.Phase = TradeDronePhase.Ascend;
                comp.PhaseStart = now;
                Dirty(drone);
                break;

            case TradeDronePhase.Ascend when elapsed >= comp.AscendTime:
                platform.Order = null;
                platform.Drone = null;
                QueueDel(drone);
                break;
        }
    }

    // The drone is at the pad: it takes the parcels and pays, or leaves what was bought.
    private void Carry(Entity<TradePlatformComponent> platform)
    {
        if (platform.Comp.Order is not { } order)
            return;

        if (order.Kind == TradeOrderKind.Sell)
            TakeParcels(platform, order);
        else
            LeaveParcel(platform, order);

        _audio.PlayPvs(PaidSound, platform);
        Refresh(platform);
    }

    private void TakeParcels(Entity<TradePlatformComponent> platform, TradeOrder order)
    {
        var parcels = FindParcels(platform);
        var contracts = GetContracts(platform);
        var credits = new Dictionary<EntityUid, double>();

        double value = 0;
        foreach (var parcel in parcels)
            value += _prices.GetPrice(parcel, contracts, credits);

        foreach (var parcel in parcels)
            QueueDel(parcel);

        var worth = (int) Math.Floor(value);
        var tax = TaxOf(worth, TaxPercent());
        var payout = worth - tax;

        if (payout > 0 && TryComp<IdCardComponent>(order.Card, out var card))
        {
            card.AccountBalance += payout;
            Dirty(order.Card, card);
            _bank.RecordTransaction(order.Card, AtmHistoryKind.Deposit, payout);
        }

        if (tax > 0)
            _budget.AddToBudget(tax);

        // What each contract paid is its trade: the standing with the company.
        foreach (var (contract, credit) in credits)
            AddPoints(contract, credit);

        _popup.PopupEntity(Loc.GetString("rumc-trade-sold", ("worth", worth), ("tax", tax), ("payout", payout)), platform, PopupType.Medium);
    }

    private void LeaveParcel(Entity<TradePlatformComponent> platform, TradeOrder order)
    {
        if (order.Product == null)
            return;

        var parcel = Spawn(platform.Comp.ParcelPrototype, Transform(platform).Coordinates);
        var inside = _container.EnsureContainer<Container>(parcel, TradeParcelComponent.ContainerId);

        foreach (var thing in SpawnProduct(order.Product, order.Count, Transform(platform).Coordinates))
            _container.Insert(thing, inside);

        if (order.Contract is { } contract)
            AddPoints(contract, order.Paid);

        _popup.PopupEntity(Loc.GetString("rumc-trade-delivered"), platform, PopupType.Medium);
    }

    // The things of a purchase: a stack comes as one stack of the count, anything else one by one.
    private List<EntityUid> SpawnProduct(string product, int count, EntityCoordinates where)
    {
        var made = new List<EntityUid>();

        var first = Spawn(product, where);
        made.Add(first);

        if (TryComp<StackComponent>(first, out var stack))
        {
            _stacks.SetCount((first, stack), count);
            return made;
        }

        for (var i = 1; i < count; i++)
            made.Add(Spawn(product, where));

        return made;
    }

    // The drone is not coming: what was paid is given back, and the pad is free again.
    private void CallOff(Entity<TradePlatformComponent> platform, string message)
    {
        if (platform.Comp.Order is { } order)
            Refund(order);

        platform.Comp.Order = null;
        platform.Comp.Drone = null;

        _popup.PopupEntity(Loc.GetString(message), platform, PopupType.MediumCaution);
        Refresh(platform);
    }

    private void Refund(TradeOrder order)
    {
        if (order.Paid <= 0 || !TryComp<IdCardComponent>(order.Card, out var card))
            return;

        card.AccountBalance += order.Paid;
        Dirty(order.Card, card);
        _bank.RecordTransaction(order.Card, AtmHistoryKind.Deposit, order.Paid);
        order.Paid = 0;
    }

    private void OnShutdown(Entity<TradePlatformComponent> platform, ref ComponentShutdown args)
    {
        if (platform.Comp.Order is { } order)
            Refund(order);

        if (platform.Comp.Drone is { } drone)
            QueueDel(drone);
    }

    #endregion

    // The parcels lying on the pad, not in anyone's hands or in a container.
    private List<Entity<TradeParcelComponent>> FindParcels(Entity<TradePlatformComponent> platform)
    {
        _parcels.Clear();
        _lookup.GetEntitiesInRange(Transform(platform).Coordinates, platform.Comp.Radius, _parcels);

        var found = new List<Entity<TradeParcelComponent>>();
        foreach (var parcel in _parcels)
        {
            if (!_container.IsEntityInContainer(parcel))
                found.Add(parcel);
        }

        return found;
    }
}
