using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.CorporateWork;

/// <summary>
///     Logistics terminal: pick which crates go on the outbound shuttle without breaking its
///     weight or hazard limits. Pays by how close the load comes to the best possible one.
/// </summary>
[RegisterComponent]
public sealed partial class CMULogisticsTerminalComponent : Component
{
    [DataField(required: true)]
    public List<CMULogisticsTier> Tiers = new();

    [DataField(required: true)]
    public ProtoId<LocalizedDatasetPrototype> Cargo;

    /// <summary>
    ///     Shuttle weight and hazard limits as a fraction of the manifest's totals.
    /// </summary>
    [DataField]
    public float LimitFraction = 0.5f;

    /// <summary>
    ///     Loads worth less than this fraction of the best possible load pay nothing.
    /// </summary>
    [DataField]
    public float MinimumRatio = 0.5f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    [ViewVariables]
    public CMULogisticsCase? Case;

    [ViewVariables]
    public TimeSpan NextCaseAt;

    [ViewVariables]
    public CMULogisticsResult? LastResult;
}

[DataDefinition]
public sealed partial class CMULogisticsTier
{
    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public int Crates;

    /// <summary>
    ///     Payout for the best possible load, before repeat reductions.
    /// </summary>
    [DataField(required: true)]
    public int Reward;
}

public sealed class CMULogisticsCase
{
    public required int Tier;
    public required List<CMULogisticsCrate> Crates;
    public required int WeightLimit;
    public required int HazardLimit;
    public required int BestValue;
}
