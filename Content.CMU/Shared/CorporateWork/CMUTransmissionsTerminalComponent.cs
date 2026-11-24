namespace Content.Shared.CMU14.CorporateWork;

/// <summary>
///     Secure Transmissions terminal: break into an intercepted signal. The signal is a grid of codes.
///     The first pick comes from the top row; after that picks alternate between the column and the row
///     of the last pick, and no cell is used twice. Every pick goes into a buffer of limited length, and
///     each target sequence that ends up in the buffer, in order, pays out.
/// </summary>
[RegisterComponent]
public sealed partial class CMUTransmissionsTerminalComponent : Component
{
    [DataField(required: true)]
    public List<CMUTransmissionsTier> Tiers = new();

    /// <summary>
    ///     The codes the grid is made of.
    /// </summary>
    [DataField]
    public List<string> Codes = new() { "1C", "55", "7A", "BD", "E9", "FF" };

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
    ///     The grid is Size by Size codes.
    /// </summary>
    [DataField(required: true)]
    public int Size;

    /// <summary>
    ///     How many picks fit in the buffer.
    /// </summary>
    [DataField(required: true)]
    public int Buffer;

    /// <summary>
    ///     The length of each target sequence. Together they fit in the buffer.
    /// </summary>
    [DataField(required: true)]
    public List<int> Sequences = new();

    /// <summary>
    ///     Payout for completing every sequence, before repeat reductions. Each sequence is worth its
    ///     share of the total length.
    /// </summary>
    [DataField(required: true)]
    public int Reward;

    /// <summary>
    ///     If set, the case closes this long after the first pick.
    /// </summary>
    [DataField]
    public TimeSpan? TimeLimit;
}

public sealed class CMUTransmissionsCase
{
    public required int Tier;

    /// <summary>
    ///     Code index for each cell, row by row.
    /// </summary>
    public required int[] Grid;

    public required List<int[]> Sequences;

    /// <summary>
    ///     A path of cells that completes every sequence. Proof the case can be solved; never sent to clients.
    /// </summary>
    public required List<int> Solution;

    public readonly List<int> Picks = new();

    public TimeSpan? Deadline;

    public EntityUid? LastUser;
}
