using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Stabilizers;

[Serializable, NetSerializable]
public enum CMUOrganStabilizerTarget : byte
{
    Heart,
    Lungs,
    Brain,
    Liver,
    Kidneys,
    Stomach,
    Eyes,
    Ears,
}

[Serializable, NetSerializable]
public enum CMUTraumaGovernorState : byte
{
    Missing,
    Ready,
    CoolingDown,
    Empty,
    Unavailable,
}

[Serializable, NetSerializable]
public readonly record struct CMUTraumaGovernorReadout(
    bool Installed,
    CMUTraumaGovernorState State,
    CMUOrganStabilizerTarget? ActiveTarget,
    float ActiveSecondsRemaining,
    float CooldownSecondsRemaining,
    bool VialLoaded,
    bool VialBypassAvailable);

