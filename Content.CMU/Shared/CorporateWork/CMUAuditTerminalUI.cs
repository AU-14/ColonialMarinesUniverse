using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.CorporateWork;

[Serializable, NetSerializable]
public enum CMUAuditTerminalUi : byte
{
    Key,
}

[Serializable, NetSerializable]
public readonly record struct CMUAuditEntry(string Item, string Department, int Amount);

[Serializable, NetSerializable]
public readonly record struct CMUAuditTierInfo(string Name, int Rows, int Errors, int Reward);

[Serializable, NetSerializable]
public readonly record struct CMUAuditResult(int Found, int Missed, int FalseFlags, int Payout);

[Serializable, NetSerializable]
public sealed class CMUAuditTerminalBuiState(
    List<CMUAuditTierInfo> tiers,
    int? activeTier,
    List<CMUAuditEntry> ledger,
    List<CMUAuditEntry> report,
    TimeSpan nextCaseAt,
    CMUAuditResult? lastResult) : BoundUserInterfaceState
{
    public readonly List<CMUAuditTierInfo> Tiers = tiers;
    public readonly int? ActiveTier = activeTier;
    public readonly List<CMUAuditEntry> Ledger = ledger;
    public readonly List<CMUAuditEntry> Report = report;
    public readonly TimeSpan NextCaseAt = nextCaseAt;
    public readonly CMUAuditResult? LastResult = lastResult;
}

[Serializable, NetSerializable]
public sealed class CMUAuditStartBuiMsg(int tier) : BoundUserInterfaceMessage
{
    public readonly int Tier = tier;
}

[Serializable, NetSerializable]
public sealed class CMUAuditSubmitBuiMsg(List<int> flagged) : BoundUserInterfaceMessage
{
    public readonly List<int> Flagged = flagged;
}
