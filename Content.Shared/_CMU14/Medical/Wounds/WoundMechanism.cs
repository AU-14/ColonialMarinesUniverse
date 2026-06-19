using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Wounds;

[Serializable, NetSerializable]
public enum WoundMechanism : byte
{
    Generic = 0,
    Bullet,
    Stab,
    Slash,
    Crush,
    Burn,
    Blast,
    Fragment,
    Surgical,
}

[Serializable, NetSerializable]
[Flags]
public enum WoundMechanismFlags
{
    None = 0,
    Bullet = 1 << 0,
    Stab = 1 << 1,
    Slash = 1 << 2,
    Crush = 1 << 3,
    Burn = 1 << 4,
    Blast = 1 << 5,
    Fragment = 1 << 6,
    Surgical = 1 << 7,
    Generic = 1 << 8,
}

[Serializable, NetSerializable]
public enum WoundTreatmentQuality : byte
{
    Untreated = 0,
    Adequate,
    Optimal,
}

[Serializable, NetSerializable]
[Flags]
public enum WoundCleanupFlags
{
    None = 0,
    RetainedFragment = 1 << 0,
    PoorClosure = 1 << 1,
    CharredTissue = 1 << 2,
    CrushDebris = 1 << 3,
    DirtyDressing = 1 << 4,
}

[Serializable, NetSerializable]
public enum ExternalBleedTier : byte
{
    None = 0,
    Minor,
    Moderate,
    Severe,
    Arterial,
}
