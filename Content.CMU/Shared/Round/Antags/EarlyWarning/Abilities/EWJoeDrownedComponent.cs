namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// A victim the Early Warning Joe drowned (phase E4, balance A-5). While it lasts the victim counts as
/// dead for objectives (the Joe's kill-person objective credits during the window); when the forced
/// sleep ends the flag is lifted again so other antags' objectives and antag selection are not
/// poisoned for the rest of the round.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeDrownedComponent : Component
{
    [DataField]
    public TimeSpan Until;

    /// <summary>The drowning added IsDeadIC (it was not already there), so the expiry removes it.</summary>
    [DataField]
    public bool AddedDeadIc;
}

/// <summary>
/// Text pass A: a victim the Joe is drowning right now (the 8 s hold). The combat system tells them what
/// is happening every second beat and lets the signal speak; removed when the hold ends either way.
/// Server bookkeeping.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeDrowningComponent : Component
{
    [DataField]
    public EntityUid Joe;

    /// <summary>Seconds of the hold so far.</summary>
    [DataField]
    public int Beat;

    /// <summary>A hold that outlives this was dropped without its do-after event; the marker goes.</summary>
    [DataField]
    public TimeSpan Until;
}
