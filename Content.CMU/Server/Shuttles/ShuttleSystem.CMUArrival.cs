// ReSharper disable CheckNamespace

using Content.Shared._RMC14.Dropship;

namespace Content.Server.Shuttles.Systems;

// CMU14: dropships take as long to land as their landing sound.
public sealed partial class ShuttleSystem
{
    private float CMUArrivalTime(EntityUid shuttle)
    {
        return TryComp(shuttle, out DropshipComponent? dropship) ? dropship.ArrivalTime : DefaultArrivalTime;
    }
}
