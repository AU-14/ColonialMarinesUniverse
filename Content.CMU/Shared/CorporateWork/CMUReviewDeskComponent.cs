namespace Content.Shared.CMU14.CorporateWork;

/// <summary>
///     Which corporate review desk a terminal runs. Each desk generates its own cases.
/// </summary>
public enum CMUReviewDeskKind : byte
{
    /// <summary>Insurance and compensation claims: pay out or deny under the policy.</summary>
    Claims,

    /// <summary>Intercepted colony messages: let them through or flag them.</summary>
    Comms,

    /// <summary>Job applicants: hire or reject against the posting and their references.</summary>
    Hiring,

    /// <summary>Access requests: approve or deny under the day's clearance rules.</summary>
    Clearance,
}

/// <summary>
///     A corporate review desk: a queue of documents, a short list of rules, and a decision for
///     each document. Every correct decision earns its share of the reward and every wrong one
///     cancels a correct one, so guessing pays nothing. The server keeps the answers.
/// </summary>
[RegisterComponent]
public sealed partial class CMUReviewDeskComponent : Component
{
    [DataField(required: true)]
    public CMUReviewDeskKind Kind;

    [DataField(required: true)]
    public LocId Title;

    [DataField(required: true)]
    public LocId Heading;

    [DataField(required: true)]
    public LocId Instructions;

    /// <summary>
    ///     The decisions on offer for each document, first one the default action.
    /// </summary>
    [DataField(required: true)]
    public List<LocId> Options = new();

    [DataField(required: true)]
    public List<CMUReviewDeskTier> Tiers = new();

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    [ViewVariables]
    public CMUReviewDeskCase? Case;

    [ViewVariables]
    public TimeSpan NextCaseAt;

    [ViewVariables]
    public CMUReviewDeskResult? LastResult;
}

[DataDefinition]
public sealed partial class CMUReviewDeskTier
{
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    ///     Documents in the queue.
    /// </summary>
    [DataField(required: true)]
    public int Items;

    /// <summary>
    ///     How many of the desk's rules are in force.
    /// </summary>
    [DataField(required: true)]
    public int Rules;

    /// <summary>
    ///     Payout for a perfect desk, before repeat reductions.
    /// </summary>
    [DataField(required: true)]
    public int Reward;
}

public sealed class CMUReviewDeskCase
{
    public required int Tier;
    public required List<string> Rules;
    public required List<CMUReviewItem> Items;

    /// <summary>
    ///     The right decision for each document, as an index into the desk's options. Never sent to clients.
    /// </summary>
    public required List<int> Answers;
}
