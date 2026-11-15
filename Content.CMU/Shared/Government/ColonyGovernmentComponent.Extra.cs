// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared.CMU14.Government;

// The rest of the state of the government: the credit and the investments, the cities of the map, and the licences that the mayor has given.
public sealed partial class ColonyGovernmentComponent
{
    // The credit rating of the city (0 to 100): what the lenders go by. A loan paid on time raises it, an instalment missed lowers it hard, and so does a default.
    public int CreditRating = 50;

    public readonly List<Loan> Loans = new();
    public int NextLoanId = 1;

    // When a lender that was let down will lend again.
    public readonly Dictionary<string, TimeSpan> LenderBlockedUntil = new();

    public readonly List<Investment> Investments = new();
    public int NextInvestmentId = 1;

    // What each city of the map thinks of this one (0 to 100), and the agreements that are in force.
    public readonly Dictionary<string, int> CityRelation = new();
    public readonly List<ActiveAccord> Accords = new();

    // The licences that were given, by the number on the card.
    public readonly Dictionary<string, LicenseRecord> Licenses = new();
    public int NextLicenseNumber = 100;
}

// A loan: how much one instalment is, how many are left, when the next is due, and how many have been missed.
public sealed class Loan(int id, string lender, int instalment, int left, TimeSpan nextDue)
{
    public readonly int Id = id;
    public readonly string Lender = lender;
    public int Instalment = instalment;
    public int Left = left;
    public TimeSpan NextDue = nextDue;
    public int Missed;
}

// A sum that is put into a thing: when it is over, and what came of it.
public sealed class Investment(int id, string proto, TimeSpan readyAt)
{
    public readonly int Id = id;
    public readonly string Proto = proto;
    public readonly TimeSpan ReadyAt = readyAt;
    public bool Done;
    public bool Success;
}

public sealed class ActiveAccord(string id, string city, TimeSpan until)
{
    public readonly string Id = id;
    public readonly string City = city;
    public readonly TimeSpan Until = until;
}

// A licence that was given: whose, what for, until when, whether the mayor has taken it back, and the card (if it still exists).
public sealed class LicenseRecord(string serial, string type, string holder, TimeSpan issuedAt, TimeSpan expiresAt)
{
    public readonly string Serial = serial;
    public readonly string Type = type;
    public readonly string Holder = holder;
    public readonly TimeSpan IssuedAt = issuedAt;
    public readonly TimeSpan ExpiresAt = expiresAt;
    public bool Revoked;
    public EntityUid? Card;
}
