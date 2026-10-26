// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Server.CMU14.WorldMarket;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Government;
using Content.Shared.CMU14.WorldMarket;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Government;

// The trade with the cities of the world map (see ColonyWorldTrade.cs): the prices, the orders of goods, the orders of the cities, what the colonists put on the shuttle, and what is paid for it. The shuttle itself, its
// landing and its departure, are in .Shuttle.cs.
public sealed partial class ColonyGovernmentSystem
{
    public const float ColonyShare = 0.7f;
    public const int MaxCartLines = 12;
    public const int MaxLots = 20;
    public const int MaxWorldOrders = 5;
    public const int PersonalFeePercent = 10;
    public const float SaturationFloor = 0.15f;
    public const float SaturationKeptPerMinute = 0.95f;

    #region Goods and prices

    // The good that a thing is, if it is one: by the stack that it is made of, or by the entity.
    private ColonyWorldGoodPrototype? GoodOf(EntityUid item)
    {
        TryComp(item, out StackComponent? stack);
        var proto = MetaData(item).EntityPrototype?.ID;
        foreach (var good in _proto.EnumeratePrototypes<ColonyWorldGoodPrototype>())
        {
            if (good.Stack is { } stackId && stack != null && stack.StackTypeId == stackId)
                return good;

            if (good.Item is { } entity && proto == entity.Id)
                return good;
        }

        return null;
    }

    private static bool Matches(List<CityCapability> wanted, WorldMarketCityInfo city)
    {
        return wanted.Any(city.Capabilities.Contains);
    }

    // How a city thinks of a good: 1.25 if it is of the kind that pays more, 0.6 if the good is for other kinds of city and this one is not, and 1 otherwise.
    private static float CityFactor(ColonyWorldGoodPrototype good, WorldMarketCityInfo city)
    {
        if (Matches(good.Premium, city))
            return 1.25f;

        if (good.BoughtBy.Count > 0 && !Matches(good.BoughtBy, city))
            return 0.6f;

        return 1f;
    }

    private static bool CitySells(ColonyWorldGoodPrototype good, WorldMarketCityInfo city)
    {
        return good.SoldBy.Count == 0 || Matches(good.SoldBy, city);
    }

    // What a city asks of the colony for one of a good: the price of the good, a few per cent more or less by the city (the same all the round).
    private static int CityBuyPrice(ColonyWorldGoodPrototype good, WorldMarketCityInfo city)
    {
        var shift = (int) ((uint) (city.Id.GetHashCode() ^ good.ID.GetHashCode()) % 21u) - 10;
        return Math.Max(1, good.BuyPrice * (100 + shift) / 100);
    }

    private static float SaturationMultiplier(ColonyWorldGoodPrototype good, float had)
    {
        return Math.Max(SaturationFloor, 1f / (1f + had / Math.Max(1, good.Capacity)));
    }

    private static string SaturationKey(string city, string good) => city + "|" + good;

    // What a city pays now for one of a good, counting what it has had.
    private int SellPriceNow(ColonyGovernmentComponent comp, ColonyWorldGoodPrototype good, WorldMarketCityInfo city)
    {
        var had = comp.Saturation.GetValueOrDefault(SaturationKey(city.Id, good.ID));
        return (int) Math.Round(good.SellPrice * CityFactor(good, city) * SaturationMultiplier(good, had));
    }

    private WorldMarketCityInfo? TradeCityInfo(ColonyGovernmentComponent comp)
    {
        return comp.TradeCity.Length == 0 ? null : _market.Cities().FirstOrDefault(c => c.Id == comp.TradeCity);
    }

    // What is paid for the goods that are on the shuttle, one unit after another (each raises what the city has had, which lowers the next), with the orders of the city filled first at a higher price. With `apply`
    // the saturation and the orders are changed too. Returns the whole sum, and the sum for each owner of a line.
    private int Appraise(ColonyGovernmentComponent comp, WorldMarketCityInfo city, bool apply, Dictionary<EntityUid, int>? byOwner = null)
    {
        var had = new Dictionary<string, float>();
        var orderLeft = new Dictionary<int, int>();
        var total = 0;

        foreach (var line in AllShipment(comp))
        {
            if (!_proto.TryIndex<ColonyWorldGoodPrototype>(line.Good, out var good))
                continue;

            var key = SaturationKey(city.Id, good.ID);
            if (!had.TryGetValue(key, out var seen))
                seen = comp.Saturation.GetValueOrDefault(key);

            var orders = comp.WorldOrders.Where(o => o.City == city.Id && o.Good == good.ID && o.Until > _timing.CurTime).ToList();
            var sum = 0;
            for (var unit = 0; unit < line.Amount; unit++)
            {
                var price = good.SellPrice * CityFactor(good, city) * SaturationMultiplier(good, seen);

                // The first unit of what an order of the city still wants is paid at the price of the order.
                var order = orders.FirstOrDefault(o => orderLeft.GetValueOrDefault(o.Id, o.Amount - o.Filled) > 0);
                if (order != null)
                {
                    orderLeft[order.Id] = orderLeft.GetValueOrDefault(order.Id, order.Amount - order.Filled) - 1;
                    price *= 1f + order.Percent / 100f;
                }

                sum += (int) Math.Round(price);
                seen += 1f;
            }

            had[key] = seen;
            total += sum;
            if (byOwner != null)
                byOwner[line.Owner] = byOwner.GetValueOrDefault(line.Owner) + sum;
        }

        if (apply)
        {
            foreach (var (key, value) in had)
                comp.Saturation[key] = value;

            foreach (var (id, left) in orderLeft)
            {
                if (comp.WorldOrders.FirstOrDefault(o => o.Id == id) is { } order)
                    order.Filled = order.Amount - left;
            }
        }

        return total;
    }

    // What the shuttle carries: what the colonists put on it by hand, and everything that lies in the crates on it, whoever put it there. The pay for the goods in the crates is the colony's alone.
    private List<ShipmentLine> AllShipment(ColonyGovernmentComponent comp, bool consume = false)
    {
        var lines = new List<ShipmentLine>(comp.Shipment);
        if (comp.Shuttle is not { } shuttle || !Exists(shuttle))
            return lines;

        var owner = Loc.GetString("rumc-world-crate-owner");
        var items = EntityQueryEnumerator<ItemComponent>();
        while (items.MoveNext(out var uid, out _))
        {
            if (!_container.IsEntityInContainer(uid) || GoodOf(uid) is not { } good || !InShuttleCrate(uid, shuttle))
                continue;

            var amount = TryComp(uid, out StackComponent? stack) ? stack.Count : 1;
            if (consume)
                QueueDel(uid);

            if (lines.FirstOrDefault(l => l.Good == good.ID && l.Owner == EntityUid.Invalid) is { } line)
                line.Amount += amount;
            else
                lines.Add(new ShipmentLine(good.ID, amount, owner, EntityUid.Invalid));
        }

        return lines;
    }

    // A thing lies in a crate on the shuttle if one of the containers around it is a crate (or a box), the outermost one stands on the grid of the shuttle, and no one carries it.
    private bool InShuttleCrate(EntityUid item, EntityUid shuttle)
    {
        var crate = false;
        var current = item;
        while (_container.TryGetContainingContainer(current, out var container))
        {
            current = container.Owner;
            if (HasComp<MobStateComponent>(current))
                return false;

            if (HasComp<EntityStorageComponent>(current) || HasComp<StorageComponent>(current))
                crate = true;
        }

        return crate && Transform(current).GridUid == shuttle;
    }

    // The saturation of the cities falls with time, and the cities make orders of their own.
    private void WorldMinute(ColonyGovernmentComponent comp)
    {
        foreach (var key in comp.Saturation.Keys.ToList())
        {
            var value = comp.Saturation[key] * SaturationKeptPerMinute;
            if (value < 0.5f)
                comp.Saturation.Remove(key);
            else
                comp.Saturation[key] = value;
        }
    }

    private void UpdateWorldOrders(ColonyGovernmentComponent comp, TimeSpan now)
    {
        comp.WorldOrders.RemoveAll(o => o.Until <= now || o.Filled >= o.Amount);

        if (comp.NextWorldOrder == TimeSpan.Zero)
            comp.NextWorldOrder = now + TimeSpan.FromMinutes(2);

        if (now < comp.NextWorldOrder)
            return;

        comp.NextWorldOrder = now + TimeSpan.FromMinutes(4);
        if (comp.WorldOrders.Count >= MaxWorldOrders)
            return;

        var cities = _market.Cities();
        var goods = _proto.EnumeratePrototypes<ColonyWorldGoodPrototype>().ToList();
        if (cities.Count == 0 || goods.Count == 0)
            return;

        var city = _random.Pick(cities);
        var good = _random.Pick(goods);
        comp.WorldOrders.Add(new WorldOrder(comp.NextWorldOrderId++, city.Id, good.ID, good.Lot * _random.Next(4, 13), _random.Next(40, 121), now + TimeSpan.FromMinutes(25)));
    }

    #endregion

    #region Orders of goods

    private bool CanOrder(ColonyGovernmentComponent comp)
    {
        return comp.Phase is ShuttlePhase.Idle or ShuttlePhase.Away;
    }

    // The heads order goods with the money of the colony. The price is the price of the city that is picked.
    private void OnOrderColony(Entity<ForemanConsoleComponent> console, ref OrderWorldGoodMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        if (!_proto.TryIndex<ColonyWorldGoodPrototype>(msg.Good, out var good) || msg.Lots < 1 || msg.Lots > MaxLots || !CanOrder(comp) || comp.Cart.Count >= MaxCartLines)
            return;

        if (TradeCityInfo(comp) is not { } city || !CitySells(good, city))
            return;

        var amount = msg.Lots * good.Lot;
        var cost = amount * CityBuyPrice(good, city);
        if (_budget.GetBudget() < cost)
            return;

        _budget.AddToBudget(-cost);
        comp.Cart.Add(new WorldCartLine(comp.NextCartId++, good.ID, amount, string.Empty, null, cost));
        Refresh();
    }

    // A player orders goods with his own money. The price is the reference price of the good and a fee, since the city is not picked yet.
    private void OnOrderPersonal(Entity<CitizenConsoleComponent> console, ref OrderWorldGoodMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        var actor = msg.Actor;
        if (!_proto.TryIndex<ColonyWorldGoodPrototype>(msg.Good, out var good) || msg.Lots < 1 || msg.Lots > MaxLots || !CanOrder(comp) || comp.Cart.Count >= MaxCartLines)
        {
            _popup.PopupEntity(Loc.GetString("rumc-world-order-refused"), console.Owner, actor);
            return;
        }

        var amount = msg.Lots * good.Lot;
        var cost = amount * PersonalPrice(good);
        if (_bank.FindOwnedCard(actor) == null || BalanceOf(actor) < cost)
        {
            _popup.PopupEntity(Loc.GetString("rumc-world-order-no-money", ("sum", cost)), console.Owner, actor);
            return;
        }

        Pay(actor, -cost);
        comp.Cart.Add(new WorldCartLine(comp.NextCartId++, good.ID, amount, MetaData(actor).EntityName, actor, cost));
        Refresh();
    }

    private static int PersonalPrice(ColonyWorldGoodPrototype good)
    {
        return Math.Max(1, good.BuyPrice * (100 + PersonalFeePercent) / 100);
    }

    // An order is cancelled before the shuttle is called: the money goes back to whoever paid.
    private void OnCancelWorldOrder(Entity<ForemanConsoleComponent> console, ref CancelWorldOrderMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var orderId = msg.Order;
        if (!CanOrder(government.Comp) || government.Comp.Cart.FirstOrDefault(l => l.Id == orderId) is not { } line)
            return;

        Refund(government.Comp, line);
        Refresh();
    }

    private void OnCancelPersonalOrder(Entity<CitizenConsoleComponent> console, ref CancelWorldOrderMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var orderId = msg.Order;
        var actor = msg.Actor;
        if (!CanOrder(government.Comp) || government.Comp.Cart.FirstOrDefault(l => l.Id == orderId && l.Payer == actor) is not { } line)
            return;

        Refund(government.Comp, line);
        Refresh();
    }

    private void Refund(ColonyGovernmentComponent comp, WorldCartLine line)
    {
        comp.Cart.Remove(line);
        if (line.Payer is { } payer && Exists(payer) && Pay(payer, line.Cost))
            return;

        _budget.AddToBudget(line.Cost);
    }

    #endregion

    #region What is put on the shuttle

    // A colonist puts goods on the shuttle by using them on the console of the shuttle: they are taken at once and written down as his, so that he gets his share of the pay.
    private void OnShuttleConsoleUsing(Entity<DropshipNavigationComputerComponent> console, ref InteractUsingEvent args)
    {
        if (args.Handled || Transform(console).GridUid is not { } grid || !HasComp<TradeShuttleComponent>(grid) || !TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var user = args.User;
        var item = args.Used;
        if (GoodOf(item) is not { } good)
            return;

        args.Handled = true;
        if (comp.Phase != ShuttlePhase.Landed || TradeCityInfo(comp) is not { } city)
        {
            _popup.PopupEntity(Loc.GetString("rumc-world-intake-closed"), console, user);
            return;
        }

        if (_bank.FindOwnedCard(user) == null)
        {
            _popup.PopupEntity(Loc.GetString("rumc-world-intake-no-account"), console, user);
            return;
        }

        var amount = 1;
        if (TryComp(item, out StackComponent? stack))
        {
            amount = stack.Count;
            if (!_stack.TryUse((item, stack), amount))
                return;
        }
        else
        {
            QueueDel(item);
        }

        var name = MetaData(user).EntityName;
        var line = comp.Shipment.FirstOrDefault(l => l.Good == good.ID && l.Owner == user);
        if (line == null)
            comp.Shipment.Add(new ShipmentLine(good.ID, amount, name, user));
        else
            line.Amount += amount;

        _popup.PopupEntity(Loc.GetString("rumc-world-intake-taken", ("amount", amount), ("good", Loc.GetString(good.Name)), ("price", SellPriceNow(comp, good, city))), console, user);
        Refresh();
    }

    #endregion

    #region The state

    private List<WorldGoodEntry> GoodEntries(ColonyGovernmentComponent comp, WorldMarketCityInfo? city, bool personal)
    {
        var entries = new List<WorldGoodEntry>();
        foreach (var good in _proto.EnumeratePrototypes<ColonyWorldGoodPrototype>().OrderBy(g => g.BuyPrice))
        {
            var buy = personal ? PersonalPrice(good) : city != null && CitySells(good, city) ? CityBuyPrice(good, city) : 0;
            var sell = city == null ? 0 : SellPriceNow(comp, good, city);
            var percent = city == null ? 100 : (int) Math.Round(SaturationMultiplier(good, comp.Saturation.GetValueOrDefault(SaturationKey(city.Id, good.ID))) * 100f);
            entries.Add(new WorldGoodEntry(good.ID, Loc.GetString(good.Name), buy, sell, percent, good.Lot));
        }

        return entries;
    }

    private List<CartEntry> CartEntries(ColonyGovernmentComponent comp, EntityUid? payer)
    {
        return comp.Cart
            .Where(l => payer == null || l.Payer == payer)
            .Select(l => new CartEntry(l.Id, _proto.TryIndex<ColonyWorldGoodPrototype>(l.Good, out var good) ? Loc.GetString(good.Name) : l.Good, l.Amount,
                l.Payer == null ? Loc.GetString("rumc-world-payer-colony") : l.PayerName, l.Cost))
            .ToList();
    }

    private ForemanState BuildForeman(ColonyGovernmentComponent comp, EntityUid console)
    {
        var now = _timing.CurTime;
        var city = TradeCityInfo(comp);

        var zones = new List<ZoneChoiceEntry>();
        var consoleMap = Transform(console).MapID;
        var query = EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var destination, out var xform))
        {
            if (xform.MapID != consoleMap || destination.Destinationtype != DropshipDestinationComponent.DestinationType.Dropship
                || HasComp<Content.Shared.CMU14.Dropship.TacticalLand.EphemeralDropshipDestinationComponent>(uid))
                continue;

            if (destination.Ship is { } ship && ship == comp.Shuttle)
                continue;

            zones.Add(new ZoneChoiceEntry(GetNetEntity(uid), MetaData(uid).EntityName, destination.Ship != null));
        }

        // Zones of the same name are told apart by a number.
        var named = zones.GroupBy(z => z.Name).ToDictionary(g => g.Key, g => g.Count());
        var seen = new Dictionary<string, int>();
        for (var i = 0; i < zones.Count; i++)
        {
            var zoneEntry = zones[i];
            if (named[zoneEntry.Name] <= 1)
                continue;

            seen[zoneEntry.Name] = seen.GetValueOrDefault(zoneEntry.Name) + 1;
            zones[i] = new ZoneChoiceEntry(zoneEntry.Zone, $"{zoneEntry.Name} #{seen[zoneEntry.Name]}", zoneEntry.Occupied);
        }

        var cities = _market.Cities()
            .Select(c => new TradeCityEntry(c.Id, c.Name, CapabilitiesText(c.Capabilities), c.CrisisName))
            .ToList();

        var worldOrders = comp.WorldOrders
            .Where(o => o.Until > now && o.Filled < o.Amount)
            .Select(o => new WorldOrderEntry(o.Id, _market.Cities().FirstOrDefault(c => c.Id == o.City)?.Name ?? o.City,
                _proto.TryIndex<ColonyWorldGoodPrototype>(o.Good, out var good) ? Loc.GetString(good.Name) : o.Good, o.Amount, o.Filled, o.Percent,
                Math.Max(0, (int) Math.Ceiling((o.Until - now).TotalSeconds))))
            .ToList();

        var shipment = AllShipment(comp)
            .Select(l => new ShipmentEntry(_proto.TryIndex<ColonyWorldGoodPrototype>(l.Good, out var good) ? Loc.GetString(good.Name) : l.Good, l.Amount, l.OwnerName))
            .ToList();

        var zoneName = comp.ShuttleZone is { } picked ? zones.FirstOrDefault(z => z.Zone == GetNetEntity(picked))?.Name ?? string.Empty : string.Empty;
        var expected = city != null ? Appraise(comp, city, false) : 0;
        var phaseLeft = comp.Phase is ShuttlePhase.Landed or ShuttlePhase.Away ? Math.Max(0, (int) Math.Ceiling((comp.ShuttleUntil - now).TotalSeconds)) : 0;

        return new ForemanState(comp.Phase, phaseLeft, zoneName, city?.Name ?? string.Empty, comp.ShuttleFailure, zones, cities, GoodEntries(comp, city, false), worldOrders,
            CartEntries(comp, null), shipment, expected, (int) _budget.GetBudget());
    }

    private WorldShopState BuildWorldShop(ColonyGovernmentComponent comp, EntityUid? viewer = null)
    {
        var now = _timing.CurTime;
        var phaseLeft = comp.Phase is ShuttlePhase.Landed or ShuttlePhase.Away ? Math.Max(0, (int) Math.Ceiling((comp.ShuttleUntil - now).TotalSeconds)) : 0;
        return new WorldShopState(comp.Phase, phaseLeft, TradeCityInfo(comp)?.Name ?? string.Empty, GoodEntries(comp, null, true), CartEntries(comp, null));
    }

    private void OnForemanOpened(Entity<ForemanConsoleComponent> console, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is not ForemanUi)
            return;

        var comp = EnsureGovernment().Comp;
        _ui.SetUiState(console.Owner, ForemanUi.Key, BuildForeman(comp, console.Owner));
    }

    private void RefreshForemanUis(ColonyGovernmentComponent comp)
    {
        var consoles = EntityQueryEnumerator<ForemanConsoleComponent>();
        while (consoles.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, ForemanUi.Key))
                _ui.SetUiState(uid, ForemanUi.Key, BuildForeman(comp, uid));
        }
    }

    private void OnSelectCity(Entity<ForemanConsoleComponent> console, ref SelectCityMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        var cityId = msg.City;
        if (comp.Phase is ShuttlePhase.Calling or ShuttlePhase.Landed or ShuttlePhase.Departing || (comp.Cart.Count > 0 && comp.TradeCity != cityId))
            return;

        if (_market.Cities().All(c => c.Id != cityId))
            return;

        comp.TradeCity = cityId;
        Refresh();
    }

    private void OnSelectZone(Entity<ForemanConsoleComponent> console, ref SelectZoneMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        var zone = GetEntity(msg.Zone);
        if (comp.Phase is ShuttlePhase.Calling or ShuttlePhase.Landed or ShuttlePhase.Departing || !TryComp(zone, out DropshipDestinationComponent? destination) || destination.Ship != null)
            return;

        comp.ShuttleZone = zone;
        Refresh();
    }

    #endregion
}
