using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Radio;

[Serializable, NetSerializable]
public enum RadioJamIntensity : byte
{
    None = 0,
    Light = 1,
    Medium = 2,
    Heavy = 3,
}
