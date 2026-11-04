// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Government;

// What the window shows of the credit, the cities of the map and the licences, and what it sends about them.

// ---------------------------------------------------------------- Credit

// A lender: how much more it will lend now, on what terms, and why it will not (nothing if it will).
[Serializable, NetSerializable]
public sealed class LenderEntry(string id, string name, string description, int available, int interestPercent, int instalments, int cycleSeconds, string blocked)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly int Available = available;
    public readonly int InterestPercent = interestPercent;
    public readonly int Instalments = instalments;
    public readonly int CycleSeconds = cycleSeconds;
    public readonly string Blocked = blocked;
}

[Serializable, NetSerializable]
public sealed class LoanEntry(int id, string lender, int instalment, int left, int nextSeconds, int missed)
{
    public readonly int Id = id;
    public readonly string Lender = lender;
    public readonly int Instalment = instalment;
    public readonly int Left = left;
    public readonly int NextSeconds = nextSeconds;
    public readonly int Missed = missed;
}

[Serializable, NetSerializable]
public sealed class InvestmentEntry(string id, string name, string description, int cost, int durationSeconds, int chancePercent, int payout, float income, string blocked)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly int Cost = cost;
    public readonly int DurationSeconds = durationSeconds;
    public readonly int ChancePercent = chancePercent;
    public readonly int Payout = payout;
    public readonly float Income = income;
    public readonly string Blocked = blocked;
}

[Serializable, NetSerializable]
public sealed class ActiveInvestmentEntry(int id, string name, int leftSeconds, bool done, bool success)
{
    public readonly int Id = id;
    public readonly string Name = name;
    public readonly int LeftSeconds = leftSeconds;
    public readonly bool Done = done;
    public readonly bool Success = success;
}

[Serializable, NetSerializable]
public sealed class CreditState(int rating, List<LenderEntry> lenders, List<LoanEntry> loans, List<InvestmentEntry> investments, List<ActiveInvestmentEntry> active)
{
    public readonly int Rating = rating;
    public readonly List<LenderEntry> Lenders = lenders;
    public readonly List<LoanEntry> Loans = loans;
    public readonly List<InvestmentEntry> Investments = investments;
    public readonly List<ActiveInvestmentEntry> Active = active;
}

// ---------------------------------------------------------------- The cities of the map

[Serializable, NetSerializable]
public sealed class CityEntry(string id, string name, int relation, string capabilities, string crisis, int crisisLeftSeconds)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly int Relation = relation;
    public readonly string Capabilities = capabilities;

    // The trouble that the city is in (nothing if none), and how long it goes on.
    public readonly string Crisis = crisis;
    public readonly int CrisisLeftSeconds = crisisLeftSeconds;
}

[Serializable, NetSerializable]
public sealed class AccordEntry(string id, string name, string description, int moneyCost, int durationSeconds, int minRelation, string effects, string requires)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly int MoneyCost = moneyCost;
    public readonly int DurationSeconds = durationSeconds;
    public readonly int MinRelation = minRelation;
    public readonly string Effects = effects;

    // What the other city must be (nothing if nothing).
    public readonly string Requires = requires;
}

[Serializable, NetSerializable]
public sealed class ActiveAccordEntry(string id, string name, string city, int leftSeconds)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string City = city;
    public readonly int LeftSeconds = leftSeconds;
}

[Serializable, NetSerializable]
public sealed class CitiesState(List<CityEntry> cities, List<AccordEntry> accords, List<ActiveAccordEntry> active, int giftCost, int aidCost, int accordSlots)
{
    public readonly List<CityEntry> Cities = cities;
    public readonly List<AccordEntry> Accords = accords;
    public readonly List<ActiveAccordEntry> Active = active;
    public readonly int GiftCost = giftCost;
    public readonly int AidCost = aidCost;
    public readonly int AccordSlots = accordSlots;
}

// ---------------------------------------------------------------- Licences

[Serializable, NetSerializable]
public sealed class LicenseTypeEntry(string id, string name, string description, int fee, int durationSeconds)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly int Fee = fee;
    public readonly int DurationSeconds = durationSeconds;
}

// A licence that was given: how long is left of it (below nothing if it has run out), and whether it was taken back.
[Serializable, NetSerializable]
public sealed class LicenseEntry(string serial, string type, string holder, int leftSeconds, bool revoked)
{
    public readonly string Serial = serial;
    public readonly string Type = type;
    public readonly string Holder = holder;
    public readonly int LeftSeconds = leftSeconds;
    public readonly bool Revoked = revoked;
}

// A person near the console, to whom a licence can be given.
[Serializable, NetSerializable]
public sealed class NearbyEntry(NetEntity entity, string name)
{
    public readonly NetEntity Entity = entity;
    public readonly string Name = name;
}

[Serializable, NetSerializable]
public sealed class LicensesState(List<LicenseTypeEntry> types, List<LicenseEntry> issued, List<NearbyEntry> nearby)
{
    public readonly List<LicenseTypeEntry> Types = types;
    public readonly List<LicenseEntry> Issued = issued;
    public readonly List<NearbyEntry> Nearby = nearby;
}

// ---------------------------------------------------------------- From the window

[Serializable, NetSerializable]
public sealed class TakeLoanMessage(string lender, int amount) : BoundUserInterfaceMessage
{
    public readonly string Lender = lender;
    public readonly int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class RepayLoanMessage(int loan) : BoundUserInterfaceMessage
{
    public readonly int Loan = loan;
}

[Serializable, NetSerializable]
public sealed class InvestMessage(string investment) : BoundUserInterfaceMessage
{
    public readonly string Investment = investment;
}

[Serializable, NetSerializable]
public sealed class CityGiftMessage(string city) : BoundUserInterfaceMessage
{
    public readonly string City = city;
}

[Serializable, NetSerializable]
public sealed class CityAidMessage(string city) : BoundUserInterfaceMessage
{
    public readonly string City = city;
}

[Serializable, NetSerializable]
public sealed class SignAccordMessage(string accord, string city) : BoundUserInterfaceMessage
{
    public readonly string Accord = accord;
    public readonly string City = city;
}

[Serializable, NetSerializable]
public sealed class BreakAccordMessage(string accord, string city) : BoundUserInterfaceMessage
{
    public readonly string Accord = accord;
    public readonly string City = city;
}

[Serializable, NetSerializable]
public sealed class IssueLicenseMessage(NetEntity target, string license) : BoundUserInterfaceMessage
{
    public readonly NetEntity Target = target;
    public readonly string License = license;
}

[Serializable, NetSerializable]
public sealed class RevokeLicenseMessage(string serial) : BoundUserInterfaceMessage
{
    public readonly string Serial = serial;
}
