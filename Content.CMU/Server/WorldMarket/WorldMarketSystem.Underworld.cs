// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.Trade;
using Content.Shared.CMU14.WorldMarket;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.WorldMarket;

// The organizations outside the law. The colony buys crates of them with the money of the treasury (they come with the next voyage and stand on the platform), and takes their orders: what is
// brought to the platform for an order is paid to whoever brings it, with no tax. All of it draws the attention of the law, and a law that has looked too much fines the colony and sends an inspection.
public sealed partial class WorldMarketSystem
{
    public const float HeatLimit = 70f;
    public const float HeatAfterInspection = 35f;
    public const int InspectionFine = 600;

    private const string WeaponsIndex = "weapons";

    // What a crate of an organization costs now: its price follows the index of the weapons.
    public int WarePrice(UnderworldWare ware)
    {
        return (int) Math.Ceiling(ware.Price * IndexOf(WeaponsIndex) / 100f);
    }

    // The colony buys a crate: it is paid at once, comes with the next voyage, and the law looks at the colony a little more.
    public bool BuyWare(Entity<WorldMarketComponent> market, string orgId, string wareId)
    {
        if (!_proto.TryIndex<UnderworldOrgPrototype>(orgId, out var org) || org.Wares.FirstOrDefault(w => w.Id == wareId) is not { } ware)
            return false;

        var price = WarePrice(ware);
        if (_budget.GetBudget() < price)
            return false;

        _budget.AddToBudget(-price);

        var state = StateOf(market, orgId);
        state.Heat = Math.Min(100f, state.Heat + ware.Heat);
        state.Standing = Math.Min(100, state.Standing + 2);

        market.Comp.Incoming.Add(new IncomingWare(orgId, wareId, NextVoyageAt() ?? _timing.CurTime));
        return true;
    }

    // The colony takes an order of an organization: from now on what is brought to the platform for it is bought.
    public bool TakeOrder(Entity<WorldMarketComponent> market, string orgId, string orderId)
    {
        if (!_proto.TryIndex<UnderworldOrgPrototype>(orgId, out var org) || org.Orders.FirstOrDefault(o => o.Id == orderId) is not { } order)
            return false;

        var state = StateOf(market, orgId);
        if (state.Orders.Any(taken => taken.Id == orderId))
            return false;

        state.Orders.Add(new TakenOrder(orderId, _timing.CurTime + TimeSpan.FromSeconds(order.DurationSeconds)));
        return true;
    }

    private UnderworldState StateOf(Entity<WorldMarketComponent> market, string org)
    {
        if (!market.Comp.Underworld.TryGetValue(org, out var state))
            market.Comp.Underworld[org] = state = new UnderworldState();

        return state;
    }

    // What an organization pays for the piece of a good now, or null when no order that the colony has taken wants it; and how many pieces it still wants, less what the sale at hand has
    // counted already.
    public int? UnderworldBid(string stack, Dictionary<string, int>? counted, out int wanted)
    {
        wanted = 0;
        if (!TryMarket(out var market))
            return null;

        int? best = null;
        var now = _timing.CurTime;
        var taken = 0;

        foreach (var (orgId, state) in market.Comp.Underworld)
        {
            if (!_proto.TryIndex<UnderworldOrgPrototype>(orgId, out var org))
                continue;

            foreach (var open in state.Orders)
            {
                if (open.Deadline <= now || org.Orders.FirstOrDefault(o => o.Id == open.Id) is not { } order || order.Stack != stack || open.Progress >= order.Amount)
                    continue;

                wanted += order.Amount - open.Progress;
                if (best == null || order.PricePerUnit > best)
                    best = order.PricePerUnit;
            }
        }

        if (counted != null)
            taken = counted.GetValueOrDefault(stack);

        wanted = Math.Max(0, wanted - taken);
        return wanted > 0 ? best : null;
    }

    // What a sale brought to the orders: the progress of each, and the payment of the colony and the attention of the law when one is done.
    private void OnSoldIllegal(Entity<WorldMarketComponent> market, TradeLedger ledger)
    {
        foreach (var (stack, count) in ledger.Illegal)
        {
            var left = count;
            foreach (var (orgId, state) in market.Comp.Underworld)
            {
                if (!_proto.TryIndex<UnderworldOrgPrototype>(orgId, out var org))
                    continue;

                foreach (var open in state.Orders.ToList())
                {
                    if (left <= 0 || org.Orders.FirstOrDefault(o => o.Id == open.Id) is not { } order || order.Stack != stack)
                        continue;

                    var take = Math.Min(left, order.Amount - open.Progress);
                    if (take <= 0)
                        continue;

                    open.Progress += take;
                    left -= take;

                    if (open.Progress < order.Amount)
                        continue;

                    state.Orders.Remove(open);
                    state.Standing = Math.Min(100, state.Standing + 5);
                    state.Heat = Math.Min(100f, state.Heat + order.Heat);
                    _budget.AddToBudget(order.Bonus);
                }
            }
        }
    }

    private void UpdateUnderworld(Entity<WorldMarketComponent> market)
    {
        var now = _timing.CurTime;

        foreach (var (orgId, state) in market.Comp.Underworld)
        {
            state.Orders.RemoveAll(order => order.Deadline <= now);

            // The law forgets: a point a minute.
            state.Heat = Math.Max(0f, state.Heat - 1f / 60f);

            if (state.Heat < HeatLimit)
                continue;

            state.Heat -= HeatAfterInspection;
            _budget.AddToBudget(-InspectionFine);
            market.Comp.Standing = Math.Max(market.Comp.Standing - 1, -4);
            market.Comp.InspectionUntil = now + TimeSpan.FromMinutes(5);

            var name = _proto.TryIndex<UnderworldOrgPrototype>(orgId, out var org) ? Loc.GetString(org.Name) : orgId;
            Tell(market, Loc.GetString("rumc-world-inspection", ("fine", InspectionFine), ("org", name)));
        }
    }

    // The voyage has come: the crates that were bought are set down on the platform of the colony.
    private void DeliverWares(Entity<WorldMarketComponent> market)
    {
        if (market.Comp.Incoming.Count == 0)
            return;

        EntityCoordinates? where = null;
        var platforms = EntityQueryEnumerator<TradePlatformComponent>();
        if (platforms.MoveNext(out var platform, out _))
            where = Transform(platform).Coordinates;

        if (where == null)
        {
            var consoles = EntityQueryEnumerator<Content.Shared.CMU14.ColonyEconomy.AdminConsoleComponent>();
            if (consoles.MoveNext(out var console, out _))
                where = Transform(console).Coordinates;
        }

        if (where is not { } coordinates)
            return;

        foreach (var incoming in market.Comp.Incoming)
        {
            if (!_proto.TryIndex<UnderworldOrgPrototype>(incoming.Org, out var org) || org.Wares.FirstOrDefault(w => w.Id == incoming.Ware) is not { } ware)
                continue;

            Spawn(ware.Crate, coordinates);
        }

        market.Comp.Incoming.Clear();
    }
}
