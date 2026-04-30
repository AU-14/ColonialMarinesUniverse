using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Wounds;

[Serializable, NetSerializable]
public enum WoundSize : byte
{
    Small = 0,
    Deep,
    Gaping,
    Massive,
}
