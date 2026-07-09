using Content.Shared._AU14.Chemistry.Reagents;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Chemistry.Research;

[Serializable, NetSerializable]
public enum ResearchDataTerminalUI
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ResearchDataTerminalBuiState(
    List<GeneratedReagentData> ids,
    Dictionary<string, (string, TimeSpan, bool, GeneratedReagentData)> data,
    TimeSpan nextUpdate,
    int credits,
    int clearance,
    int upgradecost,
    bool picked) : BoundUserInterfaceState
{
    public readonly List<GeneratedReagentData> IDs = ids;
    public readonly Dictionary<string, (string, TimeSpan, bool, GeneratedReagentData)> Data = data;
    public readonly TimeSpan NextUpdate = nextUpdate;
    public readonly int Credits = credits;
    public readonly int Clearance = clearance;
    public readonly int UpgradeCost = upgradecost;
    public readonly bool Picked = picked;
    
}

[Serializable, NetSerializable]
public sealed class ResearchDataTerminalPickChemBuiMsg(string pick) : BoundUserInterfaceMessage
{
    public readonly string Pick = pick;
}

[Serializable, NetSerializable]
public sealed class ResearchDataTerminalAttemptUpgradeBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ResearchDataTerminalPrintChemBuiMsg(string chem) : BoundUserInterfaceMessage
{
    public readonly string Chem = chem;
}
