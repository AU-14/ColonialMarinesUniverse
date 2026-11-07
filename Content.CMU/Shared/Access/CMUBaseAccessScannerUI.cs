using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Access;

[Serializable, NetSerializable]
public enum CMUBaseAccessScannerUi
{
    Key,
}

[Serializable, NetSerializable]
public enum CMUBaseAccessDirection : byte
{
    Entering,
    Leaving,
}

[Serializable, NetSerializable]
public sealed class CMUBaseAccessLogEntry(
    TimeSpan time,
    string name,
    string rank,
    CMUBaseAccessDirection direction,
    string approver)
{
    public TimeSpan Time { get; } = time;
    public string Name { get; } = name;
    public string Rank { get; } = rank;
    public CMUBaseAccessDirection Direction { get; } = direction;
    public string Approver { get; } = approver;
}

[Serializable, NetSerializable]
public sealed class CMUBaseAccessScannerBuiState(
    bool cardInserted,
    TimeSpan? cardInsertedAt,
    string? cardPrototype,
    bool authorized,
    string operatorName,
    string operatorRank,
    CMUBaseAccessDirection direction,
    string statusMessage,
    CMUBaseAccessLogEntry[] entries) : BoundUserInterfaceState
{
    public bool CardInserted { get; } = cardInserted;
    public TimeSpan? CardInsertedAt { get; } = cardInsertedAt;
    public string? CardPrototype { get; } = cardPrototype;
    public bool Authorized { get; } = authorized;
    public string OperatorName { get; } = operatorName;
    public string OperatorRank { get; } = operatorRank;
    public CMUBaseAccessDirection Direction { get; } = direction;
    public string StatusMessage { get; } = statusMessage;
    public CMUBaseAccessLogEntry[] Entries { get; } = entries;
}

[Serializable, NetSerializable]
public sealed class CMUBaseAccessInsertOperatorCardBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUBaseAccessEjectOperatorCardBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUBaseAccessSetDirectionBuiMsg(CMUBaseAccessDirection direction) : BoundUserInterfaceMessage
{
    public CMUBaseAccessDirection Direction { get; } = direction;
}
