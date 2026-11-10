using Robust.Shared.GameObjects;

namespace Content.Shared._RMC14.Xenonids;

[ByRefEvent]
public record struct XenoGibTargetAttemptEvent(EntityUid Source, EntityUid Target)
{
    public bool Handled;
}
