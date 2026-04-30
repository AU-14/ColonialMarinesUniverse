using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Medical.HUD;

[Serializable, NetSerializable]
public enum HolocardStatus : byte
{
    None,
    Urgent,
    Emergency,
    Xeno,
    Permadead,
    // Append-only: existing values must not shift (wire compatibility).
    // AutoHolocardSystem only upgrades — manually-set Permadead / Emergency
    // / Urgent are never overwritten by an auto-applied Trauma / OrganFailure
    // / Stable.
    Stable,
    Trauma,
    OrganFailure,
}
