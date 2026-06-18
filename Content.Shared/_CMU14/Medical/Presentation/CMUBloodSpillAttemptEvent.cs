using Content.Shared.Chemistry.Components;
using Robust.Shared.Map;

namespace Content.Shared._CMU14.Medical.Presentation;

[ByRefEvent]
public record struct CMUBloodSpillAttemptEvent(EntityUid Body, Solution Solution, bool FullSpill)
{
    public bool Handled;
}

[ByRefEvent]
public record struct CMUBloodPuddleAttemptEvent(TileRef Tile, Solution Solution)
{
    public bool Handled;
}
