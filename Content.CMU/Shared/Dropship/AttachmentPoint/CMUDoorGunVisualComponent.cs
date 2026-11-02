using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Dropship.AttachmentPoint;

/// <summary>Turns only the gun artwork; the mount transform remains the firing-arc reference.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUDoorGunVisualComponent : Component
{
    [DataField]
    public Vector2 NorthSouthGunOffset;

    [DataField, AutoNetworkedField]
    public Angle AimOffset;

    [DataField, AutoNetworkedField]
    public EntityUid? Operator;

    public TimeSpan NextAimUpdate;
}

[Serializable, NetSerializable]
public sealed class CMUDoorGunAimEvent(NetCoordinates coordinates) : EntityEventArgs
{
    public readonly NetCoordinates Coordinates = coordinates;
}
