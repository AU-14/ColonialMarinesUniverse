using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Anatomy.BodyParts;

[Serializable, NetSerializable]
public enum TargetBodyZone : byte
{
    Head = 0,
    Chest = 1,
    GroinPelvis = 2,
    LeftArm = 3,
    RightArm = 4,
    LeftLeg = 5,
    RightLeg = 6,
    LeftHand = 7,
    RightHand = 8,
    LeftFoot = 9,
    RightFoot = 10,
}
