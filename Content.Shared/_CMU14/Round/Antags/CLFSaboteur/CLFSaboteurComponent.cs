namespace Content.Shared._CMU14.Round.Antags.CLFSaboteur;

/// <summary>
/// A CLF saboteur embedded in the colony. Colony infrastructure (APCs, comms consoles)
/// destroyed while they are active counts toward their sabotage goal.
/// </summary>
[RegisterComponent]
public sealed partial class CLFSaboteurComponent : Component
{
    /// <summary>
    /// Infrastructure destroyed before the sabotage operation is complete.
    /// </summary>
    [DataField]
    public int SabotageGoal = 10;

    public int Count;
    public bool BountyPosted;
    public bool AnnouncedComplete;
}
