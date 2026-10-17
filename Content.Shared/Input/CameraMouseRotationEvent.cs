using Robust.Shared.Serialization;

namespace Content.Shared.Input;

// CMU14 - Start: predicted mouse-driven camera rotation messages.
[Serializable, NetSerializable]
public sealed class CameraMouseRotationEvent(double radians) : EntityEventArgs
{
    public readonly double Radians = radians;
}

[Serializable, NetSerializable]
public sealed class CameraMouseRotationAckEvent(double radians) : EntityEventArgs
{
    public readonly double Radians = radians;
}
// CMU14 - End
