// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.CMU14.WorldMarket;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Government;

// What the mayor spends the great sums on: the growth of the city (new lands, new limits), the projects with other cities and factions (an embassy, a mission, a caravan way), the grey business (the
// bribes, the spies, the pressure), and the research of the laboratory of the city. The tables are here; what is done with them is in Server/Government.

[Serializable, NetSerializable]
public enum ProjectKind : byte
{
    Embassy,
    Mission,
    Caravan,
}

// A project with the outside: a permanent embassy with a faction (it costs to keep, it earns the ambassador points of influence and makes the faction think better of the city), a trade mission or a
// caravan way with a city of the map (they last a time, and bring money and the good will of the city). The price is paid once, at the start.
[Prototype("colonyProject")]
public sealed partial class ColonyProjectPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField(required: true)]
    public ProjectKind Kind;

    [DataField]
    public int Cost = 3000;

    [DataField]
    public int UpkeepPerMinute;

    // How long it lasts (an embassy has no end).
    [DataField]
    public int DurationSeconds;

    // How well the faction (for an embassy) or the city (for the others) must think of the city.
    [DataField]
    public int MinRelation = 30;

    // What the city must be, if anything (for a way with a city).
    [DataField]
    public CityCapability? Requires;

    // What it gives while it stands.
    [DataField]
    public float IncomePerMinute;

    [DataField]
    public int DutyCut;

    [DataField]
    public int CallDiscountPercent;

    // The points of influence that the ambassador of the faction gets, for a minute (an embassy).
    [DataField]
    public float PointsPerMinute;

    // At the start: what the faction (or the city) thinks of the city more.
    [DataField]
    public int RelationShift = 5;
}

[Serializable, NetSerializable]
public enum CovertTarget : byte
{
    None,
    Faction,
    City,
}

// A grey affair: for a price, with a chance that falls as the city gets known for such things (the heat). What comes of it is told to the mayor alone; when the heat is too high it breaks out into a scandal.
[Prototype("colonyCovert")]
public sealed partial class ColonyCovertPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public CovertTarget Target = CovertTarget.None;

    [DataField]
    public int Cost = 1000;

    // The chance (0 to 1) when no one is watching.
    [DataField]
    public float SuccessChance = 0.6f;

    // How much it heats the city, whether it succeeds or not.
    [DataField]
    public int Heat = 10;

    [DataField]
    public int SuccessReward;

    [DataField]
    public int SuccessAttitude;

    [DataField]
    public int SuccessRelation;

    [DataField]
    public int SuccessPoints;

    [DataField]
    public int FailureAttitude;

    [DataField]
    public int FailureRelation;

    [DataField]
    public int FailureFine;
}
