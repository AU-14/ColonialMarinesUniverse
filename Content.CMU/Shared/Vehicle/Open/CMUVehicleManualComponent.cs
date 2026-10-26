using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// A vehicle's field manual, read in hand: its window shows the vehicle, and clicking a part opens
/// its page.
/// </summary>
[RegisterComponent]
public sealed partial class CMUVehicleManualComponent : Component
{
    [DataField(required: true)]
    public ProtoId<CMUVehicleManualPrototype> Manual;
}

[Serializable, NetSerializable]
public enum CMUVehicleManualUiKey : byte
{
    Key,
}
