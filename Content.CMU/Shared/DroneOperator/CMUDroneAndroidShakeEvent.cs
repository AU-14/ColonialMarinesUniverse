using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.DroneOperator;

[Serializable, NetSerializable]
public sealed class CMUDroneAndroidShakeEvent(NetEntity drone, float duration) : EntityEventArgs
{
    public readonly NetEntity Drone = drone;

    public readonly float Duration = duration;
}
