using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja;

[Serializable, NetSerializable]
public enum YautjaBadBloodWeaponChoiceUI : byte
{
    Key
}

// one weapon the bracer offers: the server reads the prototype from the bracer's own gear, the client only draws it
[Serializable, NetSerializable]
public readonly record struct YautjaBadBloodWeaponChoiceEntry(YautjaGearKind Kind, EntProtoId Prototype);

[Serializable, NetSerializable]
public sealed class YautjaBadBloodWeaponChoiceBuiState(
    List<YautjaBadBloodWeaponChoiceEntry> choices,
    YautjaGearKind? pendingChoice) : BoundUserInterfaceState
{
    public readonly List<YautjaBadBloodWeaponChoiceEntry> Choices = choices;
    public readonly YautjaGearKind? PendingChoice = pendingChoice;
}

[Serializable, NetSerializable]
public sealed class YautjaBadBloodWeaponChoiceMsg(YautjaGearKind choice) : BoundUserInterfaceMessage
{
    public readonly YautjaGearKind Choice = choice;
}
