using System;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Intel.Tech;

[DataRecord]
[Serializable, NetSerializable]
public sealed partial record TechUnlockVehicleEvent(string Unlock)
{
    // CMU14: true = grant an extra vehicle past the lift's group/one-use limits
    public bool Additional { get; init; }
}
