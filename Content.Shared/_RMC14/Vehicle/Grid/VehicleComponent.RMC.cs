using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Vehicle.Components;

public sealed partial class VehicleComponent
{
    [DataField, AutoNetworkedField]
    public VehicleMovementKind MovementKind = VehicleMovementKind.Standard;
}

[Serializable, NetSerializable]
public enum VehicleMovementKind : byte
{
    Standard,
    Grid,
}
