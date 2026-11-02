// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.Government;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.Government;

// The selling of finished goods between the players (see ColonyTrade.cs). Here: the sales tax, the settling of a sale (the money goes from the buyer to the seller, a share to the budget), the offers from hand to
// hand, and the auctions of things at the console of the people. The market stalls are in MarketStallSystem and use the same settling.
public sealed partial class ColonyGovernmentSystem
{
    public const string ConsignmentContainer = "consignment";
    public const int MaxSaleOffers = 8;
    public const int SaleOfferMinutes = 3;
    public const int MaxItemAuctions = 4;
    public const int MaxSalePrice = 100000;

    [Dependency] private readonly SharedContainerSystem _container = default!;

    #region Settling

    public int SalesTaxPercent()
    {
        return TryGovernment(out var government) ? government.Comp.SalesTax : 0;
    }

    // Whether a person holds a trade licence that is in force (the licences are by the name of the holder).
    public bool HasTradeLicense(string name)
    {
        if (!TryGovernment(out var government))
            return false;

        var now = _timing.CurTime;
        return government.Comp.Licenses.Values.Any(r => r.Type == TradeLicense && !r.Revoked && r.Holder == name && r.ExpiresAt > now);
    }

    public const string TradeLicense = "RUMCLicenseTrade";

    // The money of a sale: from the account of the buyer to the account of the seller, less the sales tax, which goes to the budget. False, and nothing is changed, if either has no account or the buyer has not
    // the price.
    public bool Settle(EntityUid seller, EntityUid buyer, int price)
    {
        if (price < 1 || !Exists(seller) || !Exists(buyer) || _bank.FindOwnedCard(seller) == null || _bank.FindOwnedCard(buyer) == null || BalanceOf(buyer) < price)
            return false;

        var tax = price * SalesTaxPercent() / 100;
        Pay(buyer, -price);
        Pay(seller, price - tax);
        if (tax > 0)
            _budget.AddToBudget(tax);

        return true;
    }

    private void OnSetSalesTax(Entity<ColonyGovernmentConsoleComponent> console, ref SetSalesTaxMessage msg)
    {
        EnsureGovernment().Comp.SalesTax = Math.Clamp(msg.Tax, 0, 15);
        Refresh();
    }

    #endregion

    #region From hand to hand

    private void OnOfferSale(Entity<CitizenConsoleComponent> console, ref OfferSaleMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        var actor = msg.Actor;
        var buyer = GetEntity(msg.Buyer);
        if (buyer == actor || !Near(console.Owner, actor) || !Near(console.Owner, buyer) || msg.Price < 1 || msg.Price > MaxSalePrice)
            return;

        if (!_hands.TryGetActiveItem(actor, out var held) || comp.SaleOffers.Count >= MaxSaleOffers || comp.SaleOffers.Any(o => o.Seller == actor))
        {
            _popup.PopupEntity(Loc.GetString("rumc-trade-offer-refused"), console.Owner, actor);
            return;
        }

        comp.SaleOffers.Add(new SaleOffer(comp.NextSaleOfferId++, actor, MetaData(actor).EntityName, buyer, MetaData(buyer).EntityName, held.Value, MetaData(held.Value).EntityName,
            msg.Price, _timing.CurTime + TimeSpan.FromMinutes(SaleOfferMinutes)));

        _popup.PopupEntity(Loc.GetString("rumc-trade-offered", ("name", MetaData(buyer).EntityName)), console.Owner, actor);
        _popup.PopupEntity(Loc.GetString("rumc-trade-offered-to-you", ("name", MetaData(actor).EntityName)), console.Owner, buyer);
        Refresh();
    }

    // The buyer takes it: the thing must still be in the hand of the seller, both stand by the console, and the buyer has the price. The thing goes from hand to hand and the money after it, in one step.
    private void OnAcceptSale(Entity<CitizenConsoleComponent> console, ref AcceptSaleMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var actor = msg.Actor;
        var offerId = msg.Offer;
        if (comp.SaleOffers.FirstOrDefault(o => o.Id == offerId) is not { } offer || offer.Buyer != actor)
            return;

        string? why = null;
        if (!Near(console.Owner, offer.Seller) || !Near(console.Owner, offer.Buyer))
            why = Loc.GetString("rumc-trade-both-here");
        else if (!Exists(offer.Item) || !_hands.IsHolding(offer.Seller, offer.Item))
            why = Loc.GetString("rumc-trade-item-gone");
        else if (_bank.FindOwnedCard(offer.Seller) == null || _bank.FindOwnedCard(offer.Buyer) == null)
            why = Loc.GetString("rumc-trade-no-account");
        else if (BalanceOf(offer.Buyer) < offer.Price)
            why = Loc.GetString("rumc-trade-no-money", ("sum", offer.Price));

        if (why != null)
        {
            _popup.PopupEntity(why, console.Owner, actor);
            return;
        }

        comp.SaleOffers.Remove(offer);
        _hands.TryDrop(offer.Seller, offer.Item, checkActionBlocker: false, doDropInteraction: false);
        Settle(offer.Seller, offer.Buyer, offer.Price);
        _hands.TryPickupAnyHand(offer.Buyer, offer.Item);

        _popup.PopupEntity(Loc.GetString("rumc-trade-sold", ("item", offer.ItemName), ("sum", offer.Price)), console.Owner, offer.Seller);
        _popup.PopupEntity(Loc.GetString("rumc-trade-bought", ("item", offer.ItemName), ("sum", offer.Price)), console.Owner, offer.Buyer);
        Refresh();
    }

    // Either side may take an offer back.
    private void OnDeclineSale(Entity<CitizenConsoleComponent> console, ref DeclineSaleMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var actor = msg.Actor;
        var offerId = msg.Offer;
        government.Comp.SaleOffers.RemoveAll(o => o.Id == offerId && (o.Seller == actor || o.Buyer == actor));
        Refresh();
    }

    private void UpdateSaleOffers(ColonyGovernmentComponent comp, TimeSpan now)
    {
        comp.SaleOffers.RemoveAll(o => o.Until <= now || !Exists(o.Seller) || !Exists(o.Buyer));
    }

    #endregion

    #region Auctions of things

    private static int MinItemBid(ItemAuction auction)
    {
        return auction.Leader == null ? auction.StartPrice : auction.Bid + Math.Max(5, auction.Bid / 20);
    }

    private void OnStartItemAuction(Entity<CitizenConsoleComponent> console, ref StartItemAuctionMessage msg)
    {
        var comp = EnsureGovernment().Comp;
        var actor = msg.Actor;
        if (!Near(console.Owner, actor) || msg.StartPrice < 1 || msg.StartPrice > MaxSalePrice || !AuctionMinutes.Contains(msg.Minutes))
            return;

        if (!_hands.TryGetActiveItem(actor, out var held) || comp.ItemAuctions.Count >= MaxItemAuctions || _bank.FindOwnedCard(actor) == null)
        {
            _popup.PopupEntity(Loc.GetString("rumc-trade-auction-refused"), console.Owner, actor);
            return;
        }

        // The thing is put into the console, where it is kept until the auction is over.
        var item = held.Value;
        var container = _container.EnsureContainer<Container>(console.Owner, ConsignmentContainer);
        if (!_hands.TryDrop(actor, item, checkActionBlocker: false, doDropInteraction: false) || !_container.Insert(item, container))
        {
            _popup.PopupEntity(Loc.GetString("rumc-trade-auction-refused"), console.Owner, actor);
            return;
        }

        comp.ItemAuctions.Add(new ItemAuction(comp.NextItemAuctionId++, console.Owner, actor, MetaData(actor).EntityName, item, MetaData(item).EntityName, msg.StartPrice,
            _timing.CurTime + TimeSpan.FromMinutes(msg.Minutes)));

        Refresh();
    }

    // The seller may take his thing back before anyone has bid.
    private void OnCancelItemAuction(Entity<CitizenConsoleComponent> console, ref CancelItemAuctionMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var actor = msg.Actor;
        var auctionId = msg.Auction;
        if (government.Comp.ItemAuctions.FirstOrDefault(a => a.Id == auctionId) is not { } auction || auction.Seller != actor || auction.Leader != null)
            return;

        government.Comp.ItemAuctions.Remove(auction);
        ReturnItem(auction, auction.Seller);
        Refresh();
    }

    private void OnBidItemAuction(Entity<CitizenConsoleComponent> console, ref BidItemAuctionMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var actor = msg.Actor;
        var auctionId = msg.Auction;
        var amount = msg.Amount;
        var now = _timing.CurTime;
        if (comp.ItemAuctions.FirstOrDefault(a => a.Id == auctionId) is not { } auction || !Near(console.Owner, actor))
            return;

        var name = MetaData(actor).EntityName;
        var held = auction.Leader == actor ? auction.Bid : 0;

        string? why = null;
        if (auction.Seller == actor)
            why = Loc.GetString("rumc-trade-own-auction");
        else if (IsDefaulter(comp, name, now))
            why = Loc.GetString("rumc-auction-defaulter");
        else if (amount < MinItemBid(auction))
            why = Loc.GetString("rumc-auction-too-low", ("min", MinItemBid(auction)));
        else if (_bank.FindOwnedCard(actor) == null || BalanceOf(actor) + held < amount)
            why = Loc.GetString("rumc-auction-no-money", ("sum", amount));

        if (why != null)
        {
            _popup.PopupEntity(why, console.Owner, actor);
            return;
        }

        // The old best bid goes back to its owner first, and then the new one is held.
        RefundItemBid(auction);
        Pay(actor, -amount);

        auction.Bid = amount;
        auction.Leader = actor;
        auction.LeaderName = name;

        var floor = now + TimeSpan.FromSeconds(AuctionExtendSeconds);
        if (auction.Until < floor)
            auction.Until = floor;

        _popup.PopupEntity(Loc.GetString("rumc-auction-bid-taken", ("sum", amount)), console.Owner, actor);
        Refresh();
    }

    private void RefundItemBid(ItemAuction auction)
    {
        if (auction.Leader == null || auction.Bid <= 0)
            return;

        if (!Exists(auction.Leader.Value) || !Pay(auction.Leader.Value, auction.Bid))
            _budget.AddToBudget(auction.Bid);
    }

    // The thing comes out of the console into the hands of a person (or onto the floor by the console if he is gone).
    private void ReturnItem(ItemAuction auction, EntityUid to)
    {
        if (!Exists(auction.Item))
            return;

        var container = _container.EnsureContainer<Container>(auction.Console, ConsignmentContainer);
        var coords = Exists(to) ? Transform(to).Coordinates : Transform(auction.Console).Coordinates;
        if (_container.Remove(auction.Item, container, destination: coords) && Exists(to))
            _hands.TryPickupAnyHand(to, auction.Item);
    }

    // The time is out: the thing goes to the one who bid highest, and the price (less the tax) to the seller; with no bids it goes back to the seller.
    private void UpdateItemAuctions(ColonyGovernmentComponent comp, TimeSpan now)
    {
        foreach (var auction in comp.ItemAuctions.Where(a => a.Until <= now).ToList())
        {
            comp.ItemAuctions.Remove(auction);

            if (auction.Leader is not { } winner || !Exists(winner))
            {
                // A winner who is gone leaves the price in the budget; the thing goes back to the seller.
                if (auction.Bid > 0)
                    _budget.AddToBudget(auction.Bid);

                ReturnItem(auction, auction.Seller);
                continue;
            }

            var tax = auction.Bid * comp.SalesTax / 100;
            if (Exists(auction.Seller))
                Pay(auction.Seller, auction.Bid - tax);
            else
                tax = auction.Bid;

            if (tax > 0)
                _budget.AddToBudget(tax);

            ReturnItem(auction, winner);
            if (Exists(winner))
                _popup.PopupEntity(Loc.GetString("rumc-trade-auction-won", ("item", auction.ItemName), ("sum", auction.Bid)), winner, winner);
        }
    }

    #endregion

    #region The state

    private TradeState BuildTrade(ColonyGovernmentComponent comp)
    {
        var now = _timing.CurTime;

        var offers = comp.SaleOffers
            .Select(o => new SaleOfferEntry(o.Id, GetNetEntity(o.Seller), o.SellerName, GetNetEntity(o.Buyer), o.BuyerName, o.ItemName, o.Price,
                Math.Max(0, (int) Math.Ceiling((o.Until - now).TotalSeconds))))
            .ToList();

        var auctions = comp.ItemAuctions
            .Select(a => new ItemAuctionEntry(a.Id, a.SellerName, a.ItemName, a.StartPrice, a.Leader == null ? 0 : a.Bid, a.LeaderName,
                Math.Max(0, (int) Math.Ceiling((a.Until - now).TotalSeconds)), MinItemBid(a)))
            .ToList();

        return new TradeState(offers, auctions, comp.SalesTax);
    }

    #endregion
}
