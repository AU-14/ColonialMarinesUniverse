// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.WorldMarket;

// Key of the window of the exchange with the mother faction, which the console of the ambassador opens.
[Serializable, NetSerializable]
public enum AmbassadorMotherUi : byte
{
    Key,
}

// One thing that can be ordered: what it is, which of the kinds, of which faction, what it costs in points, and why it cannot be ordered now (nothing if it can).
[Serializable, NetSerializable]
public sealed class MotherOfferEntry(string id, MotherOfferGroup group, string faction, string name, string description, int cost, string blocked, List<TargetEntry>? targets = null)
{
    public readonly string Id = id;
    public readonly MotherOfferGroup Group = group;
    public readonly string Faction = faction;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly int Cost = cost;
    public readonly string Blocked = blocked;

    // Where it can be sent (a transport): the points that it can be put down at, or the cities that it can take people out of. Nothing for the rest.
    public readonly List<TargetEntry> Targets = targets ?? new();
}

// A place that a transport can be sent to (what it is called and what it is for the server).
[Serializable, NetSerializable]
public sealed class TargetEntry(string id, string name)
{
    public readonly string Id = id;
    public readonly string Name = name;
}

// A task of the mother faction as the ambassador sees it: a report or a mission, what it asks, how long there is, what is done of it (a line for each requirement), whether a report can be
// sent now, and what is given for it.
[Serializable, NetSerializable]
public sealed class MotherTaskEntry(bool report, string name, string description, int secondsLeft, List<string> lines, bool canSend, string reward)
{
    public readonly bool Report = report;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly int SecondsLeft = secondsLeft;
    public readonly List<string> Lines = lines;
    public readonly bool CanSend = canSend;
    public readonly string Reward = reward;
}

// What the window of the exchange shows: the two scales of the ambassador (the money of the budget and the points of influence), how much money one point is changed for, the trust of the
// mother faction and the money that it is going to send (and when), its tasks, and the offers.
[Serializable, NetSerializable]
public sealed class AmbassadorMotherState(
    float budget,
    int points,
    int moneyPerPoint,
    int trust,
    int stipendSeconds,
    int stipendAmount,
    List<MotherTaskEntry> tasks,
    List<MotherOfferEntry> offers) : BoundUserInterfaceState
{
    public readonly float Budget = budget;
    public readonly int Points = points;
    public readonly int MoneyPerPoint = moneyPerPoint;
    public readonly int Trust = trust;
    public readonly int StipendSeconds = stipendSeconds;
    public readonly int StipendAmount = stipendAmount;
    public readonly List<MotherTaskEntry> Tasks = tasks;
    public readonly List<MotherOfferEntry> Offers = offers;
}

// The report that the mother faction asked for is sent.
[Serializable, NetSerializable]
public sealed class MotherReportMessage : BoundUserInterfaceMessage;

// An order (see WorldMotherOfferPrototype), paid in points of influence.
[Serializable, NetSerializable]
public sealed class MotherOrderMessage(string offer, string target = "") : BoundUserInterfaceMessage
{
    public readonly string Offer = offer;
    public readonly string Target = target;
}

// Points of influence changed for money (the subsidy of the mother faction) at the set rate.
[Serializable, NetSerializable]
public sealed class MotherSubsidyMessage(int points) : BoundUserInterfaceMessage
{
    public readonly int Points = points;
}
