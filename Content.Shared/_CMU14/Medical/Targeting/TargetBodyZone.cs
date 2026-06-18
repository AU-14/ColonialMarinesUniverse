using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Targeting;

[Serializable, NetSerializable]
public enum TargetBodyZone : byte
{
    Head = 0,
    Chest,
    GroinPelvis,
    LeftArm,
    RightArm,
    LeftHand,
    RightHand,
    LeftLeg,
    RightLeg,
    LeftFoot,
    RightFoot,
}
