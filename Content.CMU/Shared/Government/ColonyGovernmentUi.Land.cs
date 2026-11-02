// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Government;

// The land and the symbols in the window: what the two last tabs show, and what they send.

[Serializable, NetSerializable]
public sealed class ZoneKindEntry(ZoneKind kind, string name, string description, int rent)
{
    public readonly ZoneKind Kind = kind;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly int Rent = rent;
}

[Serializable, NetSerializable]
public sealed class ZoneEntry(int id, string name, int size, int x, int y, int plots, int capacity)
{
    public readonly int Id = id;
    public readonly string Name = name;
    public readonly int Size = size;
    public readonly int X = x;
    public readonly int Y = y;
    public readonly int Plots = plots;
    public readonly int Capacity = capacity;
}

[Serializable, NetSerializable]
public sealed class PlotEntry(int id, string zone, string holder, int rent, int missed, string place)
{
    public readonly int Id = id;
    public readonly string Zone = zone;
    public readonly string Holder = holder;
    public readonly int Rent = rent;
    public readonly int Missed = missed;
    public readonly string Place = place;
}

[Serializable, NetSerializable]
public sealed class RequestEntry(int id, string name, string kind, bool canAllot)
{
    public readonly int Id = id;
    public readonly string Name = name;
    public readonly string Kind = kind;
    public readonly bool CanAllot = canAllot;
}

[Serializable, NetSerializable]
public sealed class LandState(
    int tax, bool enforce, int maxZones, int surveyCost, int cycleSeconds, int rentPerCycle, string permitName, int permitFee,
    List<ZoneKindEntry> kinds, List<ZoneEntry> zones, List<PlotEntry> plots, List<RequestEntry> requests, int salesTax)
{
    public readonly int Tax = tax;

    // The share of the price of every sale between the players that goes to the budget, in per cent.
    public readonly int SalesTax = salesTax;
    public readonly bool Enforce = enforce;
    public readonly int MaxZones = maxZones;
    public readonly int SurveyCost = surveyCost;
    public readonly int CycleSeconds = cycleSeconds;

    // What the plots that are held bring in a cycle, with the tax.
    public readonly int RentPerCycle = rentPerCycle;

    // The licence that lets a person build where it is not allowed otherwise, and its fee.
    public readonly string PermitName = permitName;
    public readonly int PermitFee = permitFee;

    public readonly List<ZoneKindEntry> Kinds = kinds;
    public readonly List<ZoneEntry> Zones = zones;
    public readonly List<PlotEntry> Plots = plots;
    public readonly List<RequestEntry> Requests = requests;
}

// ---------------------------------------------------------------- From the window

[Serializable, NetSerializable]
public sealed class SetLandTaxMessage(int tax) : BoundUserInterfaceMessage
{
    public readonly int Tax = tax;
}

[Serializable, NetSerializable]
public sealed class SetEnforceMessage(bool enforce) : BoundUserInterfaceMessage
{
    public readonly bool Enforce = enforce;
}

[Serializable, NetSerializable]
public sealed class IssueSurveyorMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class RemoveZoneMessage(int zone) : BoundUserInterfaceMessage
{
    public readonly int Zone = zone;
}

[Serializable, NetSerializable]
public sealed class AllotPlotMessage(int request) : BoundUserInterfaceMessage
{
    public readonly int Request = request;
}

[Serializable, NetSerializable]
public sealed class DeclineRequestMessage(int request) : BoundUserInterfaceMessage
{
    public readonly int Request = request;
}

[Serializable, NetSerializable]
public sealed class RevokePlotMessage(int plot) : BoundUserInterfaceMessage
{
    public readonly int Plot = plot;
}
