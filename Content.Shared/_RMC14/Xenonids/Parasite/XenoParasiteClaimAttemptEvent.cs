namespace Content.Shared._RMC14.Xenonids.Parasite;

/// <summary>
/// Raised on the parasite source before a ghost takeover is accepted.
/// Downstream content can add eligibility rules without coupling them to the generic role system.
/// </summary>
public sealed class XenoParasiteClaimAttemptEvent(EntityUid user) : CancellableEntityEventArgs
{
    public EntityUid User { get; } = user;

    /// <summary>
    /// Allows a reserved claimant to bypass the ordinary post-death wait.
    /// </summary>
    public bool BypassDeathTime;
}
