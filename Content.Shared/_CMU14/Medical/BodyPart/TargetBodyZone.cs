using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.BodyPart;

[Serializable, NetSerializable]
public enum TargetBodyZone : byte
{
    Head = 0,
    Chest,
    GroinPelvis,
    LeftArm,
    RightArm,
    LeftLeg,
    RightLeg,
}
