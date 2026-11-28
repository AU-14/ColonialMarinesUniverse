namespace Content.Shared.CMU14.Combat.Drown;

/// <summary>
/// Text pass A: a victim being drowned right now (the hold). The drown system tells them what is happening every
/// second beat and lets the signal speak; removed when the hold ends either way. Server bookkeeping.
/// </summary>
[RegisterComponent]
public sealed partial class CMUDrowningComponent : Component
{
    /// <summary>The body holding the victim under.</summary>
    [DataField]
    public EntityUid Joe;

    /// <summary>Seconds of the hold so far.</summary>
    [DataField]
    public int Beat;

    /// <summary>A hold that outlives this was dropped without its do-after event; the marker goes.</summary>
    [DataField]
    public TimeSpan Until;
}
