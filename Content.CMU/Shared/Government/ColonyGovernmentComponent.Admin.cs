// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Maths;

namespace Content.Shared.CMU14.Government;

// What the mayor runs the city with: the auctions of plots and the tenders for the orders, the loans to the players, the posts, and the decrees.
public sealed partial class ColonyGovernmentComponent
{
    public readonly List<PlotAuction> Auctions = new();
    public int NextAuctionId = 1;

    public readonly List<Tender> Tenders = new();
    public int NextTenderId = 1;

    public readonly List<LoanOffer> LoanOffers = new();
    public readonly List<PlayerLoan> PlayerLoans = new();
    public int NextLoanOfferId = 1;
    public int NextPlayerLoanId = 1;

    // The people who have let a loan of the city go to default, by the name, and until when the city does not deal with them (no loans, no bids).
    public readonly Dictionary<string, TimeSpan> Defaulters = new();
}

// A plot that is sold at auction: the cell of the zone is kept for it, the best bid is held out of the account of the bidder, and it goes to him when the time is out.
public sealed class PlotAuction(int id, int zone, Vector2i cell, int startPrice, TimeSpan until)
{
    public readonly int Id = id;
    public readonly int Zone = zone;
    public readonly Vector2i Cell = cell;
    public readonly int StartPrice = startPrice;
    public TimeSpan Until = until;

    // The best bid, and who made it (nothing if no one has).
    public int Bid;
    public EntityUid? Leader;
    public string LeaderName = string.Empty;
}

// A tender for an order: the mayor asks who will fill it for the least, and the best price so far is kept. When the time is out the winner gets the order, and no one else may fill it.
public sealed class Tender(int id, string kind, int amount, int maxPrice, TimeSpan until)
{
    public readonly int Id = id;
    public readonly string Kind = kind;
    public readonly int Amount = amount;
    public readonly int MaxPrice = maxPrice;
    public readonly TimeSpan Until = until;
    public int Best;
    public EntityUid? Leader;
    public string LeaderName = string.Empty;
}

// What the mayor offers a player: the sum, the number of the instalments and the interest of the whole term. The player takes it or not.
public sealed class LoanOffer(int id, EntityUid borrower, string name, int principal, int instalments, int interest, TimeSpan until)
{
    public readonly int Id = id;
    public readonly EntityUid Borrower = borrower;
    public readonly string Name = name;
    public readonly int Principal = principal;
    public readonly int Instalments = instalments;
    public readonly int Interest = interest;
    public readonly TimeSpan Until = until;
}

public sealed class PlayerLoan(int id, EntityUid borrower, string name, int principal, int instalment, int left, TimeSpan nextDue)
{
    public readonly int Id = id;
    public readonly EntityUid Borrower = borrower;
    public readonly string Name = name;
    public readonly int Principal = principal;
    public readonly int Instalment = instalment;
    public int Left = left;
    public TimeSpan NextDue = nextDue;
    public int Missed;
}
