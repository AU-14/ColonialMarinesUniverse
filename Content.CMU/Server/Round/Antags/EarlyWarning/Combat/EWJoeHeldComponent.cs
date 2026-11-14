namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Combat;

// CMU14
/// <summary>
/// RB-17 / RB-18 (FLOW-17): per-hold state on a body an Early Warning Joe holds. Added on first use by
/// <c>EWJoeMachineGripSystem</c> and removed when any Joe starts a new pull on the body, so every hold starts clean.
/// Server only; nothing here is predicted.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeHeldComponent : Component
{
    /// <summary>
    /// RB-18: how many times this hold has restarted the body's struggle (break-free do-after), from every source
    /// (choke ticks, machete hits). Capped at <c>EWJoeMachineGripSystem.StruggleRestartsPerHold</c>.
    /// </summary>
    [ViewVariables]
    public int StruggleRestarts;

    /// <summary>RB-17: a choke in this hold has already knocked the body out once.</summary>
    [ViewVariables]
    public bool ChokeKnockedOut;
}
