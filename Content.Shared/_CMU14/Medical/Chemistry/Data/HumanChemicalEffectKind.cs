using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Chemistry.Data;

[Serializable, NetSerializable]
public enum HumanChemicalEffectKind : byte
{
    None = 0,
    BruteRecovery,
    BurnRecovery,
    ToxinConditionRecovery,
    OxygenConditionRecovery,
    OxygenConditionClear,
    OrganRepair,
    OrganSymptomSuppression,
    RegionDamage,
    OrganDamage,
}
