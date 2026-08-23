using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Chemistry.Research;

[Serializable, NetSerializable]
public enum AdminChemicalContractConsoleUi
{
    Key,
}

[Serializable, NetSerializable]
public sealed class AdminChemicalContractPropertyState(
    string id,
    string name,
    string description,
    int maxLevel,
    int selectedLevel)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly string Description = description;
    public readonly int MaxLevel = maxLevel;
    public readonly int SelectedLevel = selectedLevel;
}

[Serializable, NetSerializable]
public sealed class AdminChemicalContractConsoleBuiState(
    List<AdminChemicalContractPropertyState> properties,
    string status) : BoundUserInterfaceState
{
    public readonly List<AdminChemicalContractPropertyState> Properties = properties;
    public readonly string Status = status;
}

[Serializable, NetSerializable]
public sealed class AdminChemicalContractSetPropertyBuiMsg(string property, int level) : BoundUserInterfaceMessage
{
    public readonly string Property = property;
    public readonly int Level = level;
}

[Serializable, NetSerializable]
public sealed class AdminChemicalContractSetAllBuiMsg(int level) : BoundUserInterfaceMessage
{
    public readonly int Level = level;
}

[Serializable, NetSerializable]
public sealed class AdminChemicalContractIssueBuiMsg : BoundUserInterfaceMessage;
