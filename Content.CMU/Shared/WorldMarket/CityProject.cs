// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared.CMU14.WorldMarket;

// Something that a faction has ordered for a city (see WorldCityActionPrototype) and that is still going on: it is built until ReadyAt, then stands until ExpiresAt. It gives its favour once,
// when it is ready (Rewarded).
public sealed class CityProject
{
    public string Faction = string.Empty;
    public string City = string.Empty;
    public string Action = string.Empty;
    public TimeSpan ReadyAt;
    public TimeSpan ExpiresAt;
    public bool Rewarded;
    public int Gain;
    public int Charges;
    public int Paid;

    // The faction that a joint work was done with (nothing for the others).
    public string Partner = string.Empty;

    // When a great building next brings its point of influence (nothing until it is ready).
    public TimeSpan NextPoint;

    // Until when an official has been bribed to hold it up: it brings in no money and keeps no trouble away.
    public TimeSpan FrozenUntil;
}

// A harm that a faction did in secret (to remember it for a while): who, to whom (nothing for a trouble stirred up), where, what (a text), when, and whether it has been leaked already.
public sealed class HarmRecord
{
    public string Offender = string.Empty;
    public string Victim = string.Empty;
    public string City = string.Empty;
    public string Kind = string.Empty;
    public TimeSpan At;
    public bool Leaked;
}

// A spy that a faction has put in the headquarters of another: whose, and until when it sees what the other does.
public sealed class SpyOrder
{
    public string Target = string.Empty;
    public TimeSpan Until;
}

// The scouts that a faction has sent into a place: they are back at ReadyAt, and what they found out is good until Until.
public sealed class ScoutReport
{
    public TimeSpan ReadyAt;
    public TimeSpan Until;
}

// A trouble that a city is in.
public sealed class CityCrisis
{
    public string Id = string.Empty;
    public TimeSpan StartedAt;
    public TimeSpan EndsAt;
}

// A holiday from a tax that an ambassador has ordered of the mother faction: the tax is lower by Points of percent until Until.
public sealed class TaxBreak
{
    public WorldTaxKind Kind;
    public float Points;
    public TimeSpan Until;
    public string Faction = string.Empty;
}

// A task that the mother faction has given to a faction (see WorldMotherTaskPrototype): until when, and what the counters of the faction were when it was given.
public sealed class MotherTask
{
    public string Faction = string.Empty;
    public string Task = string.Empty;
    public TimeSpan Deadline;
    public float BaseEarned;
    public int BaseProjects;
    public int BaseCrises;
    public int BaseWorkers;
}

// What a faction has done in the cities, counted for the whole round (a task counts the difference from when it was given).
public sealed class FactionStats
{
    public float Earned;
    public int Projects;
    public int Crises;
    public int Workers;
}

// A region of the map that a faction has marked as its interest: a circle round a place, until a time.
public sealed class InterestZone
{
    public string Faction = string.Empty;
    public float Latitude;
    public float Longitude;
    public TimeSpan Until;
}
