// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared.CMU14.Government;

// The trade with the cities of the world map by the trade shuttle (see ColonyWorldTrade.cs): where the shuttle is, what is ordered and what is put on it, what the cities have had of each good, the orders of the
// cities, and the payments that wait for the shuttle to come to the city.
public sealed partial class ColonyGovernmentComponent
{
    public ShuttlePhase Phase;

    // The landing zone and the city that the heads picked, the shuttle (the grid) and the mark that it returns to, the maps that were made for it, and when the phase ends.
    public EntityUid? ShuttleZone;
    public string TradeCity = string.Empty;
    public EntityUid? Shuttle;
    public EntityUid? ShuttleReturn;
    public readonly HashSet<EntityUid> ShuttleRoots = new();
    public TimeSpan ShuttleUntil;
    public string ShuttleFailure = string.Empty;

    // The orders of goods that wait for the next trip (they were paid for when they were made), and what has been put on the shuttle to be sold.
    public readonly List<WorldCartLine> Cart = new();
    public int NextCartId = 1;
    public readonly List<ShipmentLine> Shipment = new();

    // How much of a good a city has had ("city|good"); it falls with time.
    public readonly Dictionary<string, float> Saturation = new();

    // The orders of the cities (they pay more for what fills them), and when the next is made.
    public readonly List<WorldOrder> WorldOrders = new();
    public int NextWorldOrderId = 1;
    public TimeSpan NextWorldOrder;

    // The payments that wait for the shuttle to come to the city.
    public readonly List<PendingPayout> Payouts = new();
}

// An order of goods: how much, who paid (a player, or the colony if there is no payer), and what it cost.
public sealed class WorldCartLine(int id, string good, int amount, string payerName, EntityUid? payer, int cost)
{
    public readonly int Id = id;
    public readonly string Good = good;
    public readonly int Amount = amount;
    public readonly string PayerName = payerName;
    public readonly EntityUid? Payer = payer;
    public readonly int Cost = cost;
}

// What a colonist put on the shuttle: the good, how many, and whose it is (so that he gets his share of the pay).
public sealed class ShipmentLine(string good, int amount, string ownerName, EntityUid owner)
{
    public readonly string Good = good;
    public int Amount = amount;
    public readonly string OwnerName = ownerName;
    public readonly EntityUid Owner = owner;
}

public sealed class WorldOrder(int id, string city, string good, int amount, int percent, TimeSpan until)
{
    public readonly int Id = id;
    public readonly string City = city;
    public readonly string Good = good;
    public readonly int Amount = amount;
    public readonly int Percent = percent;
    public readonly TimeSpan Until = until;
    public int Filled;
}

public sealed class PayoutShare(EntityUid owner, string name, int sum)
{
    public readonly EntityUid Owner = owner;
    public readonly string Name = name;
    public readonly int Sum = sum;
}

public sealed class PendingPayout(TimeSpan at, string city, int colony, List<PayoutShare> shares)
{
    public readonly TimeSpan At = at;
    public readonly string City = city;
    public readonly int Colony = colony;
    public readonly List<PayoutShare> Shares = shares;
}
