// ReSharper disable CheckNamespace

using Content.Shared._RMC14.Dropship;

namespace Content.Server.Shuttles.Systems;

// CMU14: a dropship can set its own approach time.
public sealed partial class ShuttleSystem
{
    private float CMUArrivalTime(EntityUid shuttle)
    {
        return TryComp(shuttle, out DropshipComponent? dropship) && dropship.ArrivalTime is { } arrival
            ? arrival
            : DefaultArrivalTime;
    }
}
