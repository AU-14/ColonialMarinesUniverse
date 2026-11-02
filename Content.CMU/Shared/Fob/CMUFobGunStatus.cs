using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Fob;

[Serializable, NetSerializable]
public enum CMUFobGunCondition : byte { Missing, Displaced, Broken, Reloading, RestartRequired, Empty, Standby, Starting, Active }

[Serializable, NetSerializable]
public readonly record struct CMUFobGunStatus(CMUFobGunCondition Condition, int Ammunition, int Damage);
