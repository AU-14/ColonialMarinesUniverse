using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Foundation;

[Serializable, NetSerializable]
public enum WoundSize : byte
{
    Small = 0,
    Deep,
    Gaping,
    Massive,
}
