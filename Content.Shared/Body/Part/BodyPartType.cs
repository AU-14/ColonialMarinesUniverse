using Robust.Shared.Serialization;

namespace Content.Shared.Body.Part;

[Serializable, NetSerializable]
public enum BodyPartType
{
    Other = 0,
    Torso,
    Head,
    Arm,
    Hand,
    Leg,
    Foot,
    Tail,
}
