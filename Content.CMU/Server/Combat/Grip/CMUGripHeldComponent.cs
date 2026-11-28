namespace Content.Server.CMU14.Combat.Grip;

/// <summary>
/// RB-17 / RB-18 (FLOW-17): per-hold state on a body a Machine Grip holds. Added on first use by
/// <c>CMUMachineGripSystem</c> and removed when any Machine Grip body starts a new pull on the body, so every hold
/// starts clean. Server only; nothing here is predicted.
/// </summary>
[RegisterComponent]
public sealed partial class CMUGripHeldComponent : Component
{
    /// <summary>
    /// RB-18: how many times this hold has restarted the body's struggle (break-free do-after), from every source
    /// (choke ticks, machete hits). Capped at <c>CMUMachineGripSystem.StruggleRestartsPerHold</c>.
    /// </summary>
    [ViewVariables]
    public int StruggleRestarts;

    /// <summary>RB-17: a choke in this hold has already knocked the body out once.</summary>
    [ViewVariables]
    public bool ChokeKnockedOut;
}
