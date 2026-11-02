// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Government;

// The selling of finished goods between the players, in three ways. The buyer always pays out of his own pocket and gets the thing in his hands in the same step, so that neither can cheat the other, and a share
// of the price (the sales tax, which the mayor sets) goes to the budget.
//   - from hand to hand: a seller who holds a thing offers it to a person who stands by the console of the people, who takes it or not;
//   - a market stall: a table with a place for the goods, each with a price; whoever comes may buy, and the owner is paid at once (the owner needs a trade licence);
//   - an auction of a thing: the thing is put into the console of the people, the players bid, and the highest takes it at the end.
// The tables are here; what is done with them is in Server/Government.

// A market stall: the goods in it are in a container, and each has the price that the owner set.
[RegisterComponent]
public sealed partial class MarketStallComponent : Component
{
    public const string ContainerId = "stall";

    [DataField]
    public int MaxItems = 12;

    // Who holds the stall (by the body), and by what name; empty if no one.
    public EntityUid? Owner;
    public string OwnerName = string.Empty;

    // The price of each thing that is in it (a thing with no price is not for sale).
    public readonly Dictionary<EntityUid, int> Prices = new();
}

[Serializable, NetSerializable]
public enum MarketStallUi : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class StallItemEntry(NetEntity item, string name, int price)
{
    public readonly NetEntity Item = item;
    public readonly string Name = name;
    public readonly int Price = price;
}

[Serializable, NetSerializable]
public sealed class StallState(string owner, List<StallItemEntry> items, int tax, int maxItems) : BoundUserInterfaceState
{
    public readonly string Owner = owner;
    public readonly List<StallItemEntry> Items = items;
    public readonly int Tax = tax;
    public readonly int MaxItems = maxItems;
}

// ---------------------------------------------------------------- From the window of the stall

[Serializable, NetSerializable]
public sealed class SetStallPriceMessage(NetEntity item, int price) : BoundUserInterfaceMessage
{
    public readonly NetEntity Item = item;
    public readonly int Price = price;
}

[Serializable, NetSerializable]
public sealed class TakeStallItemMessage(NetEntity item) : BoundUserInterfaceMessage
{
    public readonly NetEntity Item = item;
}

[Serializable, NetSerializable]
public sealed class BuyStallItemMessage(NetEntity item) : BoundUserInterfaceMessage
{
    public readonly NetEntity Item = item;
}

[Serializable, NetSerializable]
public sealed class ReleaseStallMessage : BoundUserInterfaceMessage;

// ---------------------------------------------------------------- What the console of the people shows

[Serializable, NetSerializable]
public sealed class SaleOfferEntry(int id, NetEntity seller, string sellerName, NetEntity buyer, string buyerName, string item, int price, int leftSeconds)
{
    public readonly int Id = id;
    public readonly NetEntity Seller = seller;
    public readonly string SellerName = sellerName;
    public readonly NetEntity Buyer = buyer;
    public readonly string BuyerName = buyerName;
    public readonly string Item = item;
    public readonly int Price = price;
    public readonly int LeftSeconds = leftSeconds;
}

[Serializable, NetSerializable]
public sealed class ItemAuctionEntry(int id, string seller, string item, int startPrice, int current, string leader, int leftSeconds, int minBid)
{
    public readonly int Id = id;
    public readonly string Seller = seller;
    public readonly string Item = item;
    public readonly int StartPrice = startPrice;
    public readonly int Current = current;
    public readonly string Leader = leader;
    public readonly int LeftSeconds = leftSeconds;
    public readonly int MinBid = minBid;
}

[Serializable, NetSerializable]
public sealed class TradeState(List<SaleOfferEntry> offers, List<ItemAuctionEntry> auctions, int tax)
{
    public readonly List<SaleOfferEntry> Offers = offers;
    public readonly List<ItemAuctionEntry> Auctions = auctions;
    public readonly int Tax = tax;
}

// ---------------------------------------------------------------- From the console of the people and the window of the mayor

[Serializable, NetSerializable]
public sealed class OfferSaleMessage(NetEntity buyer, int price) : BoundUserInterfaceMessage
{
    public readonly NetEntity Buyer = buyer;
    public readonly int Price = price;
}

[Serializable, NetSerializable]
public sealed class AcceptSaleMessage(int offer) : BoundUserInterfaceMessage
{
    public readonly int Offer = offer;
}

[Serializable, NetSerializable]
public sealed class DeclineSaleMessage(int offer) : BoundUserInterfaceMessage
{
    public readonly int Offer = offer;
}

[Serializable, NetSerializable]
public sealed class StartItemAuctionMessage(int startPrice, int minutes) : BoundUserInterfaceMessage
{
    public readonly int StartPrice = startPrice;
    public readonly int Minutes = minutes;
}

[Serializable, NetSerializable]
public sealed class CancelItemAuctionMessage(int auction) : BoundUserInterfaceMessage
{
    public readonly int Auction = auction;
}

[Serializable, NetSerializable]
public sealed class BidItemAuctionMessage(int auction, int amount) : BoundUserInterfaceMessage
{
    public readonly int Auction = auction;
    public readonly int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class SetSalesTaxMessage(int tax) : BoundUserInterfaceMessage
{
    public readonly int Tax = tax;
}
