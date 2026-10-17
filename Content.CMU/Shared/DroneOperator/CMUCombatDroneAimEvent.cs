using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.DroneOperator;

[Serializable, NetSerializable]
public sealed class CMUCombatDroneAimEvent(Angle angle) : EntityEventArgs
{
    public readonly Angle Angle = angle;
}
