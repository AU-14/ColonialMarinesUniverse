namespace Content.Shared.CMU14.Round.Antags.Arsonist;

/// <summary>
/// Colony arsonist. Counts distinct structures that catch fire while they are alive; the
/// CMB is alerted after the first few fires and a bounty is posted once enough of the
/// colony has burned.
/// </summary>
[RegisterComponent]
public sealed partial class ArsonistComponent : Component
{
    /// <summary>
    /// Structure fires before the CMB sends its first alert.
    /// </summary>
    [DataField]
    public int AlertThreshold = 2;

    /// <summary>
    /// Structure fires before the arsonist is posted as wanted.
    /// </summary>
    [DataField]
    public int WantedThreshold = 8;

    /// <summary>
    /// Bounty added per structure fire once the arsonist is wanted.
    /// </summary>
    [DataField]
    public int BountyPerFire = 150;

    /// <summary>
    /// Ceiling the bounty stops at once escalation starts.
    /// </summary>
    [DataField]
    public int MaxBounty = 6000;

    public int FiresCount;

    /// <summary>
    /// Structures already counted, so extinguish-and-reignite cycles count each one once.
    /// </summary>
    public readonly HashSet<EntityUid> Burned = new();
    public bool Alerted;
}
