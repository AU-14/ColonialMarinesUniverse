namespace Content.Shared._CMU14.Round.Antags.BountyHunter;

/// <summary>
/// A bounty hunter antag. Shortly after spawn they are briefed with every wanted
/// record currently on the colony's books.
/// </summary>
[RegisterComponent]
public sealed partial class BountyHunterComponent : Component
{
    /// <summary>
    /// Delay before the target list is sent, so other antags' records exist first.
    /// </summary>
    [DataField]
    public TimeSpan FaxDelay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Wanted colonists on the hunter's briefing; reported at round end.
    /// </summary>
    public int TargetCount;

    public TimeSpan NextFax;
    public bool Faxed;
}
