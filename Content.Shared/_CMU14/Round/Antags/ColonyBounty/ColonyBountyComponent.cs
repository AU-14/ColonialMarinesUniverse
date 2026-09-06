using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Round.Antags.ColonyBounty;

/// <summary>
/// Marks a colony antag as carrying a CMB bounty. Creates their wanted record on spawn
/// and pays the colony budget once when the bounty resolves.
/// </summary>
[RegisterComponent]
public sealed partial class ColonyBountyComponent : Component
{
    /// <summary>
    /// Bounty paid into the colony budget when the antag is resolved.
    /// </summary>
    [DataField]
    public int Bounty = 1000;

    /// <summary>
    /// Wanted reason shown on the criminal record.
    /// </summary>
    [DataField]
    public string Reason = string.Empty;

    /// <summary>
    /// Exact station record name; otherwise <see cref="RecordNamePrefix"/> plus the antag's name.
    /// </summary>
    [DataField]
    public string? RecordName;

    /// <summary>
    /// Prefix prepended to the antag's name when creating the record.
    /// </summary>
    [DataField]
    public string? RecordNamePrefix;

    /// <summary>
    /// Attach the criminal record to the antag's own colonist record when one exists,
    /// so the console shows their identity instead of an alias.
    /// </summary>
    [DataField]
    public bool AttachToOwnRecord;

    /// <summary>
    /// Include the antag's fingerprints on the record.
    /// </summary>
    [DataField]
    public bool IncludePrints;

    /// <summary>
    /// Include the antag's DNA on the record.
    /// </summary>
    [DataField]
    public bool IncludeDna;

    /// <summary>
    /// Being cuffed resolves the bounty.
    /// </summary>
    [DataField]
    public bool CuffedCounts = true;

    /// <summary>
    /// Dying resolves the bounty.
    /// </summary>
    [DataField]
    public bool DeadCounts = true;

    /// <summary>
    /// Paper prototype faxed to the CMB when the bounty resolves.
    /// </summary>
    [DataField]
    public EntProtoId? CapturedFaxPaper;

    /// <summary>
    /// Optional second fax machine name that also receives the resolution fax.
    /// </summary>
    [DataField]
    public string? CapturedFaxExtraRecipient;

    /// <summary>
    /// Set once the bounty has been paid; prevents repeat payouts.
    /// </summary>
    public bool Paid;

    /// <summary>
    /// True when the payout was for a capture rather than a kill. Only valid once Paid.
    /// </summary>
    public bool Captured;

    /// <summary>
    /// Set once the wanted record exists; cleared again if the antag had no station yet.
    /// </summary>
    public bool Registered;
}
