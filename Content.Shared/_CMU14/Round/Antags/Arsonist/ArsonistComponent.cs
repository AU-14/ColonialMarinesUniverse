namespace Content.Shared._CMU14.Round.Antags.Arsonist;

/// <summary>
/// Colony arsonist. Counts structure fires while they are alive; the CMB is alerted at
/// the first fire and a bounty is posted once enough of the colony has burned.
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

    public int FiresCount;
    public bool Alerted;
}
