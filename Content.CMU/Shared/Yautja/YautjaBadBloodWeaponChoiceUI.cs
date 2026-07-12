using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Yautja;

[Serializable, NetSerializable]
public enum YautjaBadBloodWeaponChoiceUI : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class YautjaBadBloodWeaponChoiceBuiState(
    List<YautjaGearKind> choices,
    YautjaGearKind? pendingChoice) : BoundUserInterfaceState
{
    public readonly List<YautjaGearKind> Choices = choices;
    public readonly YautjaGearKind? PendingChoice = pendingChoice;
}

[Serializable, NetSerializable]
public sealed class YautjaBadBloodWeaponChoiceMsg(YautjaGearKind choice) : BoundUserInterfaceMessage
{
    public readonly YautjaGearKind Choice = choice;
}
