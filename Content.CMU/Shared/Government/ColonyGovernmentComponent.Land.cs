// SPDX-License-Identifier: AGPL-3.0-only
namespace Content.Shared.CMU14.Government;

// The land and the symbols of the city: the zones that the mayor has marked, the plots that he has given, the requests for a plot, and the flag, the arms and the holidays.
public sealed partial class ColonyGovernmentComponent
{
    public readonly List<LandZone> Zones = new();
    public int NextZoneId = 1;

    public readonly List<LandPlot> Plots = new();
    public int NextPlotId = 1;

    public readonly List<PlotRequest> Requests = new();
    public int NextRequestId = 1;

    // The land tax is a share (in per cent) that is added to the rent; the mayor sets it. With the enforcement on, nothing is built outside the zones, or on a plot of another, without a permit.
    public int LandTax = 5;
    public bool Enforce;
    public TimeSpan NextRent;
}

// A zone: a square on a grid, marked by the corner of the lowest coordinates (in the tiles of the grid) and the side of it (5, 10 or 15 tiles).
public sealed class LandZone(int id, ZoneKind kind, EntityUid grid, Vector2i min, int size)
{
    public readonly int Id = id;
    public readonly ZoneKind Kind = kind;
    public readonly EntityUid Grid = grid;
    public readonly Vector2i Min = min;
    public readonly int Size = size;
}

// A plot is a square of five tiles by five in a zone: the number of the cell of the zone says where it is.
public sealed class LandPlot(int id, int zone, Vector2i cell, string holder, EntityUid? holderEntity, TimeSpan since)
{
    public const int Side = 5;

    public readonly int Id = id;
    public readonly int Zone = zone;
    public readonly Vector2i Cell = cell;
    public string Holder = holder;
    public EntityUid? HolderEntity = holderEntity;
    public readonly TimeSpan Since = since;
    public int Missed;
    public EntityUid? Card;
}

public sealed class PlotRequest(int id, EntityUid person, string name, ZoneKind kind)
{
    public readonly int Id = id;
    public readonly EntityUid Person = person;
    public readonly string Name = name;
    public readonly ZoneKind Kind = kind;
}
