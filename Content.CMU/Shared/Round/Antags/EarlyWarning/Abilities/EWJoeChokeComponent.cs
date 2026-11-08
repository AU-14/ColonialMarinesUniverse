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
    /// RB-17: the choke has knocked <see cref="Target"/> out (RMC stamina below its knockout tier) once in this hold.
    /// From then on a choke tick never drains stamina below <see cref="StaminaFloorAfterKnockout"/>. Reset per hold:
    /// a new grab starts clean, and a choke released and restarted inside the same hold keeps it.
    /// The struggle restart is shared by every source and tracked by <c>EWJoeMachineGripSystem</c> (RB-18).
    /// </summary>
    [DataField]
    public bool KnockedOut;

    /// <summary>RB-17: after the one knockout, choke ticks stop at this much stamina (RMC knocks out below 10).</summary>
    [DataField]
    public double StaminaFloorAfterKnockout = 10;
}
