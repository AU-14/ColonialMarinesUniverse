namespace Content.Shared.CMU14.Combat.Drown;

/// <summary>
/// A victim that was drowned (balance A-5). While it lasts the victim counts as dead for objectives (the Early Warning
/// Joe's kill-person objective credits during the window); when the forced sleep ends the flag is lifted again so other
/// antags' objectives and antag selection are not poisoned for the rest of the round.
/// </summary>
[RegisterComponent]
public sealed partial class CMUDrownedComponent : Component
{
    [DataField]
    public TimeSpan Until;

    /// <summary>The drowning added IsDeadIC (it was not already there), so the expiry removes it.</summary>
    [DataField]
    public bool AddedDeadIc;
}
