using Robust.Shared.Serialization;

namespace Content.Shared.AU14.AllianceConsole;

[Serializable, NetSerializable]
public enum AllianceConsoleUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class AllianceConsoleBuiState : BoundUserInterfaceState
{
    public readonly string Faction;
    public readonly Dictionary<string, AllianceStatus> FactionStatuses;
    public readonly List<string> ControllableFactions;

    public AllianceConsoleBuiState(
        string faction,
        Dictionary<string, AllianceStatus> factionStatuses,
        List<string> controllableFactions)
    {
        Faction = faction;
        FactionStatuses = factionStatuses;
        ControllableFactions = controllableFactions;
    }
}

[Serializable, NetSerializable]
public sealed class AllianceConsoleSetFactionStatusMsg : BoundUserInterfaceMessage
{
    public readonly string TargetFaction;
    public readonly AllianceStatus Status;

    public AllianceConsoleSetFactionStatusMsg(string targetFaction, AllianceStatus status)
    {
        TargetFaction = targetFaction;
        Status = status;
    }
}
