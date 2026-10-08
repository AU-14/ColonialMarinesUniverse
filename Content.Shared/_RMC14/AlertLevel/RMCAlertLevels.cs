using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.AlertLevel;

// TODO RMC14 make these entities
[Serializable, NetSerializable]
public enum RMCAlertLevels
{
    Green = 0,
    Blue,
    Red,
    Black, // CMU14: darkened ship, submarine stealth posture below delta
    Delta,
}
