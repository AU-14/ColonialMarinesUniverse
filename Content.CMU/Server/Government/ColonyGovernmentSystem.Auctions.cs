// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.Government;

namespace Content.Server.CMU14.Government;

// The auctions of the plots and the tenders for the orders: the players bid against each other, so that the price is not the price of a list. A bid on a plot is held out of the account of the bidder at once (and
// given back when someone bids higher), so that the winner has paid when he wins. A tender goes the other way: the one who offers to fill the order for the least gets it, and no one else may fill it.
public sealed partial class ColonyGovernmentSystem
{
    public const int MaxAuctions = 3;
    public const int MaxTenders = 3;
    public const int MaxStartPrice = 20000;
    public const int AuctionExtendSeconds = 30;

    private static readonly int[] AuctionMinutes = { 3, 5, 10 };

    private bool IsDefaulter(ColonyGovernmentComponent comp, string name, TimeSpan now)
    {
        return comp.Defaulters.TryGetValue(name, out var until) && until > now;
    }

    #region Plot auctions

    // What the next bid must be at the least: the start price, and then a twentieth more than the best bid (five at the least).
    private static int MinBid(PlotAuction auction)
    {
        return auction.Leader == null ? auction.StartPrice : auction.Bid + Math.Max(5, auction.Bid / 20);
    }

    private void OnStartAuction(Entity<ColonyGovernmentConsoleComponent> console, ref StartAuctionMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        if (comp.Auctions.Count >= MaxAuctions || msg.StartPrice < 1 || msg.StartPrice > MaxStartPrice || !AuctionMinutes.Contains(msg.Minutes))
            return;

        if (FreeCell(comp, msg.Kind) is not { } free)
            return;

        var auction = new PlotAuction(comp.NextAuctionId++, free.zone.Id, free.cell, msg.StartPrice, _timing.CurTime + TimeSpan.FromMinutes(msg.Minutes));
        comp.Auctions.Add(auction);

        Refresh();
    }

    private void OnCancelAuction(Entity<ColonyGovernmentConsoleComponent> console, ref CancelAuctionMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var auctionId = msg.Auction;
        if (government.Comp.Auctions.FirstOrDefault(a => a.Id == auctionId) is not { } auction)
            return;

        DropAuction(government.Comp, auction);
        Refresh();
    }

    // An auction is taken off: the best bid is given back.
    private void DropAuction(ColonyGovernmentComponent comp, PlotAuction auction)
    {
        comp.Auctions.Remove(auction);
        RefundBid(auction);
    }

    private void RefundBid(PlotAuction auction)
    {
        if (auction.Leader == null || auction.Bid <= 0)
            return;

        if (!Exists(auction.Leader.Value) || !Pay(auction.Leader.Value, auction.Bid))
            _budget.AddToBudget(auction.Bid);
    }

    private void CancelAuctionsInZone(ColonyGovernmentComponent comp, int zoneId)
    {
        foreach (var auction in comp.Auctions.Where(a => a.Zone == zoneId).ToList())
            DropAuction(comp, auction);
    }

    private void OnBidAuction(Entity<CitizenConsoleComponent> console, ref BidAuctionMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var actor = msg.Actor;
        var auctionId = msg.Auction;
        var amount = msg.Amount;
        var now = _timing.CurTime;
        if (comp.Auctions.FirstOrDefault(a => a.Id == auctionId) is not { } auction || !Near(console.Owner, actor))
            return;

        var name = MetaData(actor).EntityName;
        var held = auction.Leader == actor ? auction.Bid : 0;

        string? why = null;
        if (IsDefaulter(comp, name, now))
            why = Loc.GetString("rumc-auction-defaulter");
        else if (comp.Plots.Count(p => p.Holder == name) >= MaxPlotsPerPerson)
            why = Loc.GetString("rumc-auction-plot-limit", ("max", MaxPlotsPerPerson));
        else if (amount < MinBid(auction))
            why = Loc.GetString("rumc-auction-too-low", ("min", MinBid(auction)));
        else if (_bank.FindOwnedCard(actor) == null || BalanceOf(actor) + held < amount)
            why = Loc.GetString("rumc-auction-no-money", ("sum", amount));

        if (why != null)
        {
            _popup.PopupEntity(why, console.Owner, actor);
            return;
        }

        // The old best bid goes back to its owner first, and then the new one is held.
        RefundBid(auction);
        Pay(actor, -amount);

        auction.Bid = amount;
        auction.Leader = actor;
        auction.LeaderName = name;

        // A bid at the last moment gives the others a little time to answer.
        var floor = now + TimeSpan.FromSeconds(AuctionExtendSeconds);
        if (auction.Until < floor)
            auction.Until = floor;

        _popup.PopupEntity(Loc.GetString("rumc-auction-bid-taken", ("sum", amount)), console.Owner, actor);
        Refresh();
    }

    // The time is out: the winner gets the plot (the price he held goes to the budget), or the cell is freed if no one bid.
    private void UpdateAuctions(ColonyGovernmentComponent comp, TimeSpan now)
    {
        foreach (var auction in comp.Auctions.Where(a => a.Until <= now).ToList())
        {
            comp.Auctions.Remove(auction);
            var zone = comp.Zones.FirstOrDefault(z => z.Id == auction.Zone);
            if (zone == null)
            {
                RefundBid(auction);
                continue;
            }

            if (auction.Leader is not { } winner || !Exists(winner))
            {
                // A winner who is gone leaves the price in the budget.
                if (auction.Bid > 0)
                    _budget.AddToBudget(auction.Bid);

                continue;
            }

            _budget.AddToBudget(auction.Bid);
            var plot = new LandPlot(comp.NextPlotId++, zone.Id, auction.Cell, auction.LeaderName, winner, now);
            comp.Plots.Add(plot);
            plot.Card = PrintDeed(plot, zone, winner);
            _hands.TryPickupAnyHand(winner, plot.Card.Value);
        }
    }

    private List<AuctionEntry> AuctionEntries(ColonyGovernmentComponent comp, TimeSpan now)
    {
        var entries = new List<AuctionEntry>();
        foreach (var auction in comp.Auctions)
        {
            var zone = comp.Zones.FirstOrDefault(z => z.Id == auction.Zone);
            if (zone == null)
                continue;

            var corner = PlotCorner(zone, auction.Cell);
            entries.Add(new AuctionEntry(auction.Id, ZoneName(zone), Loc.GetString("rumc-auction-place", ("x", corner.X), ("y", corner.Y)), auction.StartPrice,
                auction.Leader == null ? 0 : auction.Bid, auction.LeaderName, Math.Max(0, (int) Math.Ceiling((auction.Until - now).TotalSeconds)), MinBid(auction)));
        }

        return entries;
    }

    #endregion

    #region Tenders

    private void OnStartTender(Entity<ColonyGovernmentConsoleComponent> console, ref StartTenderMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        if (!_proto.TryIndex<ColonyOrderKindPrototype>(msg.Kind, out var kind) || comp.Tenders.Count >= MaxTenders || !AuctionMinutes.Contains(msg.Minutes))
            return;

        if (msg.Amount < 1 || msg.Amount > kind.MaxAmount || msg.MaxPrice < 1 || msg.MaxPrice > kind.UnitPrice * 3)
            return;

        comp.Tenders.Add(new Tender(comp.NextTenderId++, kind.ID, msg.Amount, msg.MaxPrice, _timing.CurTime + TimeSpan.FromMinutes(msg.Minutes)));
        Refresh();
    }

    private void OnCancelTender(Entity<ColonyGovernmentConsoleComponent> console, ref CancelTenderMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var tenderId = msg.Tender;
        if (government.Comp.Tenders.FirstOrDefault(t => t.Id == tenderId) is not { } tender)
            return;

        government.Comp.Tenders.Remove(tender);
        Refresh();
    }

    private void OnBidTender(Entity<CitizenConsoleComponent> console, ref BidTenderMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var actor = msg.Actor;
        var tenderId = msg.Tender;
        var price = msg.Price;
        if (comp.Tenders.FirstOrDefault(t => t.Id == tenderId) is not { } tender || !Near(console.Owner, actor))
            return;

        var name = MetaData(actor).EntityName;
        string? why = null;
        if (IsDefaulter(comp, name, _timing.CurTime))
            why = Loc.GetString("rumc-auction-defaulter");
        else if (price < 1 || price > tender.MaxPrice || (tender.Leader != null && price >= tender.Best))
            why = Loc.GetString("rumc-tender-too-high", ("max", tender.Leader == null ? tender.MaxPrice : tender.Best - 1));

        if (why != null)
        {
            _popup.PopupEntity(why, console.Owner, actor);
            return;
        }

        tender.Best = price;
        tender.Leader = actor;
        tender.LeaderName = name;

        _popup.PopupEntity(Loc.GetString("rumc-tender-bid-taken", ("price", price)), console.Owner, actor);
        Refresh();
    }

    // The time is out: the winner gets the order at his price, and the money for all of it is taken out of the budget (if there is not enough, the tender is void).
    private void UpdateTenders(ColonyGovernmentComponent comp, TimeSpan now)
    {
        foreach (var tender in comp.Tenders.Where(t => t.Until <= now).ToList())
        {
            comp.Tenders.Remove(tender);
            if (!_proto.TryIndex<ColonyOrderKindPrototype>(tender.Kind, out var kind))
                continue;

            if (tender.Leader == null || !Exists(tender.Leader.Value))
            {
                continue;
            }

            var escrow = tender.Amount * tender.Best;
            if (_budget.GetBudget() < escrow || comp.Orders.Count >= MaxOrders)
            {
                continue;
            }

            _budget.AddToBudget(-escrow);
            comp.Orders.Add(new MunicipalOrder(comp.NextOrderId++, kind.ID, tender.Amount, tender.Best, now + TimeSpan.FromMinutes(OrderLifetimeMinutes)) { Contractor = tender.LeaderName });
        }
    }

    private List<TenderEntry> TenderEntries(ColonyGovernmentComponent comp, TimeSpan now)
    {
        var entries = new List<TenderEntry>();
        foreach (var tender in comp.Tenders)
        {
            if (!_proto.TryIndex<ColonyOrderKindPrototype>(tender.Kind, out var kind))
                continue;

            entries.Add(new TenderEntry(tender.Id, Loc.GetString(kind.Name), Loc.GetString(kind.Description), tender.Amount, tender.MaxPrice,
                tender.Leader == null ? 0 : tender.Best, tender.LeaderName, Math.Max(0, (int) Math.Ceiling((tender.Until - now).TotalSeconds))));
        }

        return entries;
    }

    #endregion
}
