using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Round.Antags.ColonyBounty;

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
    /// Append the antag's non-default spoken languages to the wanted record as a lead.
    /// </summary>
    [DataField]
    public bool IncludeLanguages;

    /// <summary>
    /// Being booked as Detained on the records console resolves the bounty.
    /// Cuffing alone must not, or the payout confirms identities for free.
    /// </summary>
    [DataField]
    public bool CaptureCounts = true;

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
    /// Set once law enforcement could identify the antag (own record scanned or Detained
    /// booked); drives the cover-blown briefing.
    /// </summary>
    public bool CoverBlown;

    /// <summary>
    /// Set once the bounty has been paid; prevents repeat payouts.
    /// </summary>
    public bool Paid;

    /// <summary>
    /// True when the payout came from a Detained booking rather than a kill. Only valid once Paid.
    /// </summary>
    public bool Captured;

    /// <summary>
    /// Set once the wanted record exists; cleared again if the antag had no station yet.
    /// </summary>
    public bool Registered;

    /// <summary>
    /// The antag's own station record id, cached so the per-tick Detained check stays O(1).
    /// Lookup by name would rescan every record on every update.
    /// </summary>
    public uint? OwnRecordId;

    /// <summary>
    /// The name OwnRecordId was resolved under; a mismatch means the antag copied a new
    /// identity and the copy's record is watched too.
    /// </summary>
    public string? OwnRecordName;

    /// <summary>
    /// Record of a copied identity, if the antag renamed after registration; booking it
    /// Detained resolves the bounty like the own record does.
    /// </summary>
    public uint? CopiedRecordId;

    /// <summary>
    /// The alias record the bounty was registered under, so escalation can raise the
    /// bounty marshals see on the console.
    /// </summary>
    public uint? AliasRecordId;
}
