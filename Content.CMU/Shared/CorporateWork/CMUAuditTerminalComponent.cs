using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.CorporateWork;

/// <summary>
///     Records &amp; Audit terminal: compare a submitted expense report against the ledger and flag
///     the entries that don't match. Pays into the corporate budget.
/// </summary>
[RegisterComponent]
public sealed partial class CMUAuditTerminalComponent : Component
{
    /// <summary>
    ///     Selectable difficulty tiers, easiest first.
    /// </summary>
    [DataField(required: true)]
    public List<CMUAuditTier> Tiers = new();

    [DataField(required: true)]
    public ProtoId<LocalizedDatasetPrototype> Items;

    [DataField(required: true)]
    public ProtoId<LocalizedDatasetPrototype> Departments;

    [DataField]
    public int MinAmount = 40;

    [DataField]
    public int MaxAmount = 2500;

    /// <summary>
    ///     Delay after a submitted audit before the terminal will open a new case.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(45);

    [ViewVariables]
    public CMUAuditCase? Case;

    [ViewVariables]
    public TimeSpan NextCaseAt;

    [ViewVariables]
    public CMUAuditResult? LastResult;
}

[DataDefinition]
public sealed partial class CMUAuditTier
{
    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public int Rows;

    [DataField(required: true)]
    public int Errors;

    /// <summary>
    ///     Payout for a perfect audit, before repeat reductions.
    /// </summary>
    [DataField(required: true)]
    public int Reward;
}

public sealed class CMUAuditCase
{
    public required int Tier;
    public required List<CMUAuditEntry> Ledger;
    public required List<CMUAuditEntry> Report;

    /// <summary>
    ///     Indexes into <see cref="Report"/> that don't match the ledger.
    /// </summary>
    public required HashSet<int> Discrepancies;
}
