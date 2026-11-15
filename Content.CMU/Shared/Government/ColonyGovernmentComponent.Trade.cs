// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared.CMU14.Government;

// The selling between the players (see ColonyTrade.cs): the sales tax, the offers from hand to hand, and the auctions of things.
public sealed partial class ColonyGovernmentComponent
{
    // The share of the price of every sale that goes to the budget, in per cent.
    public int SalesTax = 3;

    public readonly List<SaleOffer> SaleOffers = new();
    public int NextSaleOfferId = 1;

    public readonly List<ItemAuction> ItemAuctions = new();
    public int NextItemAuctionId = 1;
}

// An offer: the seller holds the thing in his hand, the buyer is named, the price is set. If the buyer takes it, the thing goes from the hand of the seller to the hand of the buyer.
public sealed class SaleOffer(int id, EntityUid seller, string sellerName, EntityUid buyer, string buyerName, EntityUid item, string itemName, int price, TimeSpan until)
{
    public readonly int Id = id;
    public readonly EntityUid Seller = seller;
    public readonly string SellerName = sellerName;
    public readonly EntityUid Buyer = buyer;
    public readonly string BuyerName = buyerName;
    public readonly EntityUid Item = item;
    public readonly string ItemName = itemName;
    public readonly int Price = price;
    public readonly TimeSpan Until = until;
}

// An auction of a thing: the thing is kept in the console that it was put into, the best bid is held out of the account of the bidder, and the thing goes to him when the time is out.
public sealed class ItemAuction(int id, EntityUid console, EntityUid seller, string sellerName, EntityUid item, string itemName, int startPrice, TimeSpan until)
{
    public readonly int Id = id;
    public readonly EntityUid Console = console;
    public readonly EntityUid Seller = seller;
    public readonly string SellerName = sellerName;
    public readonly EntityUid Item = item;
    public readonly string ItemName = itemName;
    public readonly int StartPrice = startPrice;
    public TimeSpan Until = until;
    public int Bid;
    public EntityUid? Leader;
    public string LeaderName = string.Empty;
}
