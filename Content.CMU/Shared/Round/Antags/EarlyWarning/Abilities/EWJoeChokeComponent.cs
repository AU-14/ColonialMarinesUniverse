namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// A running choke (phase E4, balance A-7): while the Joe keeps its aggressive grab on
/// <see cref="Target"/>, the target loses stamina every second. Removed when the grab breaks, the
/// target dies, or the Joe toggles the move off.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeChokeComponent : Component
{
    [DataField]
    public EntityUid Target;

    /// <summary>The Choke action, shown toggled while the choke runs.</summary>
    [DataField]
    public EntityUid? Action;

    /// <summary>
    /// Review fix (FLOW-02): the hold resets the target's struggle at most once per choke, so the victim can
    /// still break free; restarting it every tick made a choke inescapable.
    /// </summary>
    [DataField]
    public bool StruggleExtended;
}
