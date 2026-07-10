using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Radio;

[Serializable, NetSerializable]
public enum RadioJamIntensity : byte
{
    None = 0,
    Light = 1,
    Medium = 2,
    Heavy = 3,
}
