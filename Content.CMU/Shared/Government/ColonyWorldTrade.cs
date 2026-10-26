// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.CMU14.WorldMarket;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Government;

// The trade of the colony with the cities of the world map, by a trade shuttle. The heads (who have the access of the orders console) use the console of the foreman: they pick a landing zone and a city of the
// map, and call the shuttle; it comes down on the zone with what was bought, stays a time (the colonists unload it and put on it what they sell, at the console of the shuttle), and goes away with the goods, which are
// paid for when it has come to the city. A shuttle that stands on a landing zone holds it: no other shuttle may land there. One shuttle serves the colony. Goods may be ordered with the money of the colony (by the
// heads alone) and with the money of a player. The sale is unlimited, but the more of a good a city has had, the less it pays for the next (and the price comes back with time). Of what is paid for the goods, 70% goes
// to the budget and 30% to the players who put them on the shuttle. The tables are here; what is done with them is in Server/Government.

// A thing that is bought of the cities and sold to them. A good is known by the stack or the entity that it is made of.
[Prototype("colonyWorldGood")]
public sealed partial class ColonyWorldGoodPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public ProtoId<StackPrototype>? Stack;

    // For a good that is not a stack: the entity that is accepted at the shuttle, and the one that is brought when it is bought.
    [DataField]
    public EntProtoId? Item;

    [DataField]
    public EntProtoId? Spawn;

    // What the colony pays for one when it buys (before the city's own price), and what a city pays for one when it buys of the colony (before what it has had already).
    [DataField]
    public int BuyPrice = 30;

    [DataField]
    public int SellPrice = 20;

    // How many a city takes before it pays half the price for the next.
    [DataField]
    public int Capacity = 60;

    // The goods are ordered in lots of this many.
    [DataField]
    public int Lot = 5;

    // The kinds of city that pay more for it; that sell it (nothing: every city sells it); that buy it at the full price (nothing: every city does, and the others at a lower price).
    [DataField]
    public List<CityCapability> Premium = new();

    [DataField]
    public List<CityCapability> SoldBy = new();

    [DataField]
    public List<CityCapability> BoughtBy = new();
}

// A mark on the console of the foreman: the console for the trade shuttle, which only the heads may use.
[RegisterComponent]
public sealed partial class ForemanConsoleComponent : Component;

// A mark on the grid of the trade shuttle: the console of the shuttle takes the goods that the colonists put on it.
[RegisterComponent]
public sealed partial class TradeShuttleComponent : Component;

[Serializable, NetSerializable]
public enum ForemanUi : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum ShuttlePhase : byte
{
    Idle,
    Calling,
    Landed,
    Departing,
    Away,
}

// ---------------------------------------------------------------- What the windows show

[Serializable, NetSerializable]
public sealed class ZoneChoiceEntry(NetEntity zone, string name, bool occupied)
{
    public readonly NetEntity Zone = zone;
    public readonly string Name = name;
    public readonly bool Occupied = occupied;
}

[Serializable, NetSerializable]
public sealed class TradeCityEntry(string id, string name, string capabilities, string crisis)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Capabilities = capabilities;
    public readonly string Crisis = crisis;
}

// A good, as it is in the city that is picked: what it costs to buy (0 if the city does not sell it), what the city pays now for one, and how much of the full price that is (the more has been sold, the less).
[Serializable, NetSerializable]
public sealed class WorldGoodEntry(string id, string name, int buyPrice, int sellPrice, int percent, int lot)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly int BuyPrice = buyPrice;
    public readonly int SellPrice = sellPrice;
    public readonly int Percent = percent;
    public readonly int Lot = lot;
}

[Serializable, NetSerializable]
public sealed class WorldOrderEntry(int id, string city, string good, int amount, int filled, int percent, int leftSeconds)
{
    public readonly int Id = id;
    public readonly string City = city;
    public readonly string Good = good;
    public readonly int Amount = amount;
    public readonly int Filled = filled;

    // How much more than the usual price a city pays for what fills the order, in per cent.
    public readonly int Percent = percent;
    public readonly int LeftSeconds = leftSeconds;
}

[Serializable, NetSerializable]
public sealed class CartEntry(int id, string good, int amount, string payer, int cost)
{
    public readonly int Id = id;
    public readonly string Good = good;
    public readonly int Amount = amount;
    public readonly string Payer = payer;
    public readonly int Cost = cost;
}

[Serializable, NetSerializable]
public sealed class ShipmentEntry(string good, int amount, string owner)
{
    public readonly string Good = good;
    public readonly int Amount = amount;
    public readonly string Owner = owner;
}

[Serializable, NetSerializable]
public sealed class ForemanState(
    ShuttlePhase phase, int phaseSeconds, string zone, string city, string failure,
    List<ZoneChoiceEntry> zones, List<TradeCityEntry> cities, List<WorldGoodEntry> goods, List<WorldOrderEntry> worldOrders,
    List<CartEntry> cart, List<ShipmentEntry> shipment, int expectedPayout, int budget) : BoundUserInterfaceState
{
    public readonly ShuttlePhase Phase = phase;

    // The time until the phase ends, if it has an end.
    public readonly int PhaseSeconds = phaseSeconds;

    // The zone and the city that are picked (empty if none).
    public readonly string Zone = zone;
    public readonly string City = city;
    public readonly string Failure = failure;

    public readonly List<ZoneChoiceEntry> Zones = zones;
    public readonly List<TradeCityEntry> Cities = cities;
    public readonly List<WorldGoodEntry> Goods = goods;
    public readonly List<WorldOrderEntry> WorldOrders = worldOrders;
    public readonly List<CartEntry> Cart = cart;
    public readonly List<ShipmentEntry> Shipment = shipment;

    // What the goods that are on the shuttle would bring in all, at the prices of now.
    public readonly int ExpectedPayout = expectedPayout;
    public readonly int Budget = budget;
}

// What the console of the people shows of the world trade: the goods that a player may order with his own money, his orders, and where the shuttle is.
[Serializable, NetSerializable]
public sealed class WorldShopState(ShuttlePhase phase, int phaseSeconds, string city, List<WorldGoodEntry> goods, List<CartEntry> orders)
{
    public readonly ShuttlePhase Phase = phase;
    public readonly int PhaseSeconds = phaseSeconds;
    public readonly string City = city;
    public readonly List<WorldGoodEntry> Goods = goods;
    public readonly List<CartEntry> Orders = orders;
}

// ---------------------------------------------------------------- From the console of the foreman and the console of the people

[Serializable, NetSerializable]
public sealed class SelectZoneMessage(NetEntity zone) : BoundUserInterfaceMessage
{
    public readonly NetEntity Zone = zone;
}

[Serializable, NetSerializable]
public sealed class SelectCityMessage(string city) : BoundUserInterfaceMessage
{
    public readonly string City = city;
}

// An order of goods: by the heads with the money of the colony (the foreman console), by a player with his own (the console of the people).
[Serializable, NetSerializable]
public sealed class OrderWorldGoodMessage(string good, int lots) : BoundUserInterfaceMessage
{
    public readonly string Good = good;
    public readonly int Lots = lots;
}

[Serializable, NetSerializable]
public sealed class CancelWorldOrderMessage(int order) : BoundUserInterfaceMessage
{
    public readonly int Order = order;
}

[Serializable, NetSerializable]
public sealed class CallShuttleMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DepartShuttleMessage : BoundUserInterfaceMessage;
