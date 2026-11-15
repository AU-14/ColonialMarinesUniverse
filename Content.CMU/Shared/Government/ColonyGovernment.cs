// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Government;

// The foreign policy of the colony: what the administrator decides about the factions of the ambassadors for the whole round: the treaties with them, and the petitions and ultimatums that they
// send. The tables are here; what is done with them is in Server/Government.

[Serializable, NetSerializable]
public enum TreatyKind : byte
{
    Trade,
    Defense,
    Sanction,
}

// A treaty that the colony can make with a faction (with the ambassador of it): for a time, and for a price, it gives what is set here to the colony and, for a trade or a defense, a share of the
// favour to the ambassador. A sanction is the other kind: it costs money and sours the faction, and the others like it.
[Prototype("colonyTreaty")]
public sealed partial class ColonyTreatyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public TreatyKind Kind = TreatyKind.Trade;

    [DataField]
    public int MoneyCost;

    [DataField]
    public int DurationSeconds = 1200;

    // How well the faction must think of the colony for it to sign (a sanction has no such need).
    [DataField]
    public int MinAttitude = 15;

    // While it is in force.
    [DataField]
    public float IncomePerMinute;

    [DataField]
    public int DutyCut;

    [DataField]
    public int CallDiscountPercent;

    // The points of influence that the ambassador of the partner gets, for a minute (a part of a point is kept and added up).
    [DataField]
    public float PartnerPointsPerMinute;

    // When it is signed: how much the faction thinks of the colony more (or less), what the other factions think of it, and how much the trust of the partner in its mother faction changes.
    [DataField]
    public int AttitudeShift = 5;

    [DataField]
    public int OthersAttitudeShift;

    [DataField]
    public int TrustShift;
}

// What an ambassador asks of the colony. A request can be put off or turned down at a small price; an ultimatum has a time, and what is not answered in it is paid for.
[Prototype("colonyPetition")]
public sealed partial class ColonyPetitionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    // The text; the faction is the "faction" of it.
    [DataField(required: true)]
    public LocId Text;

    [DataField]
    public bool Ultimatum;

    [DataField]
    public int DeadlineSeconds = 300;

    // The faction asks only when it thinks of the colony within these bounds: the ultimatums come from those who think badly of it, the requests from those who do not.
    [DataField]
    public int MinAttitude = -20;

    [DataField]
    public int MaxAttitude = 100;

    [DataField]
    public float Weight = 1f;

    // To agree: what it costs, what it brings, what it does to the faction's thought of the colony, and the points that the ambassador gets.
    [DataField]
    public int AcceptCost;

    [DataField]
    public int AcceptReward;

    [DataField]
    public int AcceptAttitude = 8;

    [DataField]
    public int AcceptPoints;

    // To turn down.
    [DataField]
    public int DeclineAttitude = -6;

    // Not to answer an ultimatum in time.
    [DataField]
    public int IgnoreAttitude = -15;
}

// ---------------------------------------------------------------- Credit

// A lender: the city borrows of it up to a limit that goes with the credit rating, and pays the loan back in instalments (one a cycle) with the interest of the whole term. An instalment that is
// missed lowers the rating; three missed are a default (the lender takes a part of the treasury, and does not lend again for a time).
[Prototype("colonyLender")]
public sealed partial class ColonyLenderPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    // The limit at a rating of fifty; it goes up and down with the rating (from half of it at nought to one and a half times at a hundred).
    [DataField]
    public int MaxAmount = 10000;

    [DataField]
    public int MinRating;

    // What is added to the sum for the whole term, in percent; how many instalments it is paid in; and how long a cycle is.
    [DataField]
    public int InterestPercent = 12;

    [DataField]
    public int Instalments = 6;

    [DataField]
    public int CycleSeconds = 300;
}

// A thing that the city can put money into: it takes a time and then either pays (the sum, and more, and perhaps an income for the rest of the round) or fails (a part of the sum comes back).
[Prototype("colonyInvestment")]
public sealed partial class ColonyInvestmentPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public int Cost = 5000;

    [DataField]
    public int DurationSeconds = 480;

    // The chance (0 to 1) at the credit rating that is fifty; the rating moves it a little.
    [DataField]
    public float SuccessChance = 0.8f;

    // What comes back, as a share of the cost, when it pays and when it fails.
    [DataField]
    public float PayoutMultiplier = 1.8f;

    [DataField]
    public float SalvageMultiplier = 0.3f;

    // What it brings for a minute, for the rest of the round, if it pays.
    [DataField]
    public float IncomePerMinute;

    [DataField]
    public int RatingOnSuccess = 3;

    [DataField]
    public int RatingOnFailure = -4;
}

// ---------------------------------------------------------------- The cities of the map

// An agreement of the city with another city of the map: for a time, and for a price, it gives what is set here. It needs the other city to think well enough of this one, and perhaps to be a thing
// (a trade hub).
[Prototype("colonyAccord")]
public sealed partial class ColonyAccordPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public int MoneyCost = 2000;

    [DataField]
    public int DurationSeconds = 1500;

    // How well the other city must think of this one.
    [DataField]
    public int MinRelation = 30;

    // What the other city must be, if anything.
    [DataField]
    public Content.Shared.CMU14.WorldMarket.CityCapability? Requires;

    [DataField]
    public float IncomePerMinute;

    [DataField]
    public int DutyCut;

    [DataField]
    public int CallDiscountPercent;

    // How much better the other city thinks of this one for it.
    [DataField]
    public int RelationShift = 5;
}

// ---------------------------------------------------------------- Licenses

// A licence that the mayor can give to a person: for a fee that the person pays from the bank account, and for a time. It is a card that he carries; the law officers look at it, and write the
// fines themselves.
[Prototype("colonyLicense")]
public sealed partial class ColonyLicensePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public int Fee = 300;

    [DataField]
    public int DurationSeconds = 1800;

    // The card that is printed for this licence (the sprite of it is its own).
    [DataField]
    public EntProtoId Card = "RUMCCityLicenseCard";
}
