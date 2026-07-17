using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Yautja;

[Serializable, NetSerializable]
public sealed class YautjaChainGauntletExecutionAnimationEvent(
    NetEntity target,
    float liftHeight,
    TimeSpan liftDuration,
    TimeSpan dropDuration) : EntityEventArgs
{
    public readonly NetEntity Target = target;
    public readonly float LiftHeight = liftHeight;
    public readonly TimeSpan LiftDuration = liftDuration;
    public readonly TimeSpan DropDuration = dropDuration;
}
