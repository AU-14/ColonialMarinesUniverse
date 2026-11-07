namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>Where a Darken a District directive stands (Objectives report §6a).</summary>
public enum EWJoeDarkenState : byte
{
    /// <summary>The pinned area is powered; the Joe has to black it out.</summary>
    Armed,

    /// <summary>The area went dark by the Joe's hand after assignment; the timer runs while it stays dark.</summary>
    Dark,

    /// <summary>It stayed dark long enough.</summary>
    Complete,
}

/// <summary>
/// ARCH-03 N7 "Darken a District": pins an area that is powered at assignment (it has an APC, is not
/// always-powered, and its lighting is on); the directive arms there. When the area goes from powered to
/// unpowered after that, and the Joe caused it (it stood in the area, or hit an APC, reactor or generator
/// feeding it, within <see cref="AttributionWindow"/>), the timer starts; <see cref="DarkDuration"/> of
/// continuous darkness completes it, and power coming back resets it. A blackout the Joe did not cause
/// re-pins to another powered area, or, with none left, lets the ladder withdraw the directive. It never
/// completes on its own. Polled once a second by <see cref="EWJoeDarkenSystem"/>.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeDarkenConditionComponent : Component
{
    /// <summary>The pinned area entity. Set on assignment.</summary>
    [DataField]
    public EntityUid? Area;

    [DataField]
    public string AreaName = string.Empty;

    /// <summary>The Joe the directive was assigned to.</summary>
    [DataField]
    public EntityUid? Owner;

    /// <summary>The mind holding the directive; its body is followed across a respawn.</summary>
    [DataField]
    public EntityUid? MindId;

    [DataField]
    public EWJoeDarkenState State = EWJoeDarkenState.Armed;

    /// <summary>When the current blackout began.</summary>
    [DataField]
    public TimeSpan DarkSince;

    /// <summary>The last time the Joe was seen inside the pinned area.</summary>
    [DataField]
    public TimeSpan? JoeInAreaAt;

    /// <summary>How long the area has to stay dark (§6a: 3 minutes).</summary>
    [DataField]
    public TimeSpan DarkDuration = TimeSpan.FromSeconds(180);

    /// <summary>How long before the blackout the Joe's presence or sabotage still counts (§6a: 60 s).</summary>
    [DataField]
    public TimeSpan AttributionWindow = TimeSpan.FromSeconds(60);

    /// <summary>No powered area was left to re-pin to: the directive cannot finish and is withdrawn.</summary>
    [DataField]
    public bool Exhausted;

    /// <summary>Title locale id; receives "area".</summary>
    [DataField(required: true)]
    public LocId Title;

    /// <summary>Description locale id; receives "area" and "minutes".</summary>
    [DataField(required: true)]
    public LocId Description;
}
