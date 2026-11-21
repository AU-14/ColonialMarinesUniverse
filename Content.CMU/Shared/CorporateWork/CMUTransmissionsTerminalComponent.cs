namespace Content.Shared.CMU14.CorporateWork;

/// <summary>
///     Secure Transmissions terminal: crack an intercepted cipher key within a limited number of
///     attempts. Each attempt reports how many symbols are right and in place, and how many are
///     right but misplaced. Pays more the fewer attempts it takes.
/// </summary>
[RegisterComponent]
public sealed partial class CMUTransmissionsTerminalComponent : Component
{
    [DataField(required: true)]
    public List<CMUTransmissionsTier> Tiers = new();

    /// <summary>
    ///     A solve on the final attempt still pays this fraction of the reward.
    /// </summary>
    [DataField]
    public float LastAttemptFraction = 0.5f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    [ViewVariables]
    public CMUTransmissionsCase? Case;

    [ViewVariables]
    public TimeSpan NextCaseAt;

    [ViewVariables]
    public CMUTransmissionsResult? LastResult;
}

[DataDefinition]
public sealed partial class CMUTransmissionsTier
{
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    ///     Symbols in the key.
    /// </summary>
    [DataField(required: true)]
    public int Length;

    /// <summary>
    ///     How many different symbols the key can use.
    /// </summary>
    [DataField(required: true)]
    public int Symbols;

    [DataField(required: true)]
    public int Attempts;

    /// <summary>
    ///     Payout for a first-attempt solve, before repeat reductions.
    /// </summary>
    [DataField(required: true)]
    public int Reward;
}

public sealed class CMUTransmissionsCase
{
    public required int Tier;
    public required int[] Key;
    public readonly List<CMUTransmissionsAttempt> Attempts = new();
}
