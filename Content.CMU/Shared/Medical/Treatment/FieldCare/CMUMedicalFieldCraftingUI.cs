using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Treatment.FieldCare;

[Serializable, NetSerializable]
public enum CMUMedicalFieldCraftingUI : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CMUMedicalFieldCraftingBuiState(List<CMUMedicalFieldCraftingOption> options) : BoundUserInterfaceState
{
    public readonly List<CMUMedicalFieldCraftingOption> Options = options;
}

[Serializable, NetSerializable]
public sealed record CMUMedicalFieldCraftingOption(
    CMUFieldTreatmentFamily Family,
    CMUFieldTreatmentBaseKind BaseKind,
    EntProtoId Product,
    int IngredientCost);

[Serializable, NetSerializable]
public sealed class CMUMedicalFieldCraftingChooseBuiMsg(
    CMUFieldTreatmentFamily family,
    CMUFieldTreatmentBaseKind baseKind) : BoundUserInterfaceMessage
{
    public readonly CMUFieldTreatmentFamily Family = family;
    public readonly CMUFieldTreatmentBaseKind BaseKind = baseKind;
}

[Serializable, NetSerializable]
public sealed class CMUMedicalFieldCraftingOpenRequestEvent : EntityEventArgs;
