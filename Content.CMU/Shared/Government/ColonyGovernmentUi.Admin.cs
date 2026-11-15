// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Government;

// What the windows show of the auctions and the tenders, the loans to the players, the posts and the decrees, and what they send. The entries are the same in the window of the mayor and in the console of
// the people.

[Serializable, NetSerializable]
public sealed class AuctionEntry(int id, string zone, string place, int startPrice, int current, string leader, int leftSeconds, int minBid)
{
    public readonly int Id = id;
    public readonly string Zone = zone;
    public readonly string Place = place;
    public readonly int StartPrice = startPrice;

    // The best bid (0 if none), who made it, and what the next must be at the least.
    public readonly int Current = current;
    public readonly string Leader = leader;
    public readonly int LeftSeconds = leftSeconds;
    public readonly int MinBid = minBid;
}

[Serializable, NetSerializable]
public sealed class TenderEntry(int id, string kind, string description, int amount, int maxPrice, int best, string leader, int leftSeconds)
{
    public readonly int Id = id;
    public readonly string Kind = kind;
    public readonly string Description = description;
    public readonly int Amount = amount;
    public readonly int MaxPrice = maxPrice;

    // The best price for a piece so far (0 if none) and who offered it.
    public readonly int Best = best;
    public readonly string Leader = leader;
    public readonly int LeftSeconds = leftSeconds;
}

[Serializable, NetSerializable]
public sealed class LoanOfferEntry(int id, NetEntity borrower, string name, int principal, int instalments, int interest, int instalment)
{
    public readonly int Id = id;
    public readonly NetEntity Borrower = borrower;
    public readonly string Name = name;
    public readonly int Principal = principal;
    public readonly int Instalments = instalments;
    public readonly int Interest = interest;
    public readonly int Instalment = instalment;
}

[Serializable, NetSerializable]
public sealed class PlayerLoanEntry(int id, string borrower, int principal, int instalment, int left, int missed, int nextSeconds)
{
    public readonly int Id = id;
    public readonly string Borrower = borrower;
    public readonly int Principal = principal;
    public readonly int Instalment = instalment;
    public readonly int Left = left;
    public readonly int Missed = missed;
    public readonly int NextSeconds = nextSeconds;
}

// What the window of the mayor shows of all this: and, in the first place, what the one who is at the console may do of it (the rights).
[Serializable, NetSerializable]
public sealed class AdminState(
    List<AuctionEntry> auctions, List<TenderEntry> tenders, List<LoanOfferEntry> offers, List<PlayerLoanEntry> loans)
{
    public readonly List<AuctionEntry> Auctions = auctions;
    public readonly List<TenderEntry> Tenders = tenders;
    public readonly List<LoanOfferEntry> Offers = offers;
    public readonly List<PlayerLoanEntry> Loans = loans;
}

// What the console of the people shows of all this.
[Serializable, NetSerializable]
public sealed class CitizenAdminState(List<AuctionEntry> auctions, List<TenderEntry> tenders, List<LoanOfferEntry> offers, List<PlayerLoanEntry> loans)
{
    public readonly List<AuctionEntry> Auctions = auctions;
    public readonly List<TenderEntry> Tenders = tenders;
    public readonly List<LoanOfferEntry> Offers = offers;
    public readonly List<PlayerLoanEntry> Loans = loans;
}

// ---------------------------------------------------------------- From the window of the mayor

[Serializable, NetSerializable]
public sealed class StartAuctionMessage(ZoneKind kind, int startPrice, int minutes) : BoundUserInterfaceMessage
{
    public readonly ZoneKind Kind = kind;
    public readonly int StartPrice = startPrice;
    public readonly int Minutes = minutes;
}

[Serializable, NetSerializable]
public sealed class CancelAuctionMessage(int auction) : BoundUserInterfaceMessage
{
    public readonly int Auction = auction;
}

[Serializable, NetSerializable]
public sealed class StartTenderMessage(string kind, int amount, int maxPrice, int minutes) : BoundUserInterfaceMessage
{
    public readonly string Kind = kind;
    public readonly int Amount = amount;
    public readonly int MaxPrice = maxPrice;
    public readonly int Minutes = minutes;
}

[Serializable, NetSerializable]
public sealed class CancelTenderMessage(int tender) : BoundUserInterfaceMessage
{
    public readonly int Tender = tender;
}

[Serializable, NetSerializable]
public sealed class OfferLoanMessage(NetEntity target, int principal, int instalments, int interest) : BoundUserInterfaceMessage
{
    public readonly NetEntity Target = target;
    public readonly int Principal = principal;
    public readonly int Instalments = instalments;
    public readonly int Interest = interest;
}

[Serializable, NetSerializable]
public sealed class WithdrawLoanOfferMessage(int offer) : BoundUserInterfaceMessage
{
    public readonly int Offer = offer;
}

[Serializable, NetSerializable]
public sealed class ForgiveLoanMessage(int loan) : BoundUserInterfaceMessage
{
    public readonly int Loan = loan;
}

// ---------------------------------------------------------------- From the console of the people

[Serializable, NetSerializable]
public sealed class BidAuctionMessage(int auction, int amount) : BoundUserInterfaceMessage
{
    public readonly int Auction = auction;
    public readonly int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class BidTenderMessage(int tender, int price) : BoundUserInterfaceMessage
{
    public readonly int Tender = tender;
    public readonly int Price = price;
}

[Serializable, NetSerializable]
public sealed class AcceptLoanMessage(int offer) : BoundUserInterfaceMessage
{
    public readonly int Offer = offer;
}

[Serializable, NetSerializable]
public sealed class DeclineLoanMessage(int offer) : BoundUserInterfaceMessage
{
    public readonly int Offer = offer;
}

[Serializable, NetSerializable]
public sealed class RepayLoanEarlyMessage(int loan) : BoundUserInterfaceMessage
{
    public readonly int Loan = loan;
}
