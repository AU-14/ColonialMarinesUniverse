namespace Content.Server.CMU14.Yautja.BadBlood;

public enum BadBloodUplinkSiteKind : byte
{
    Sensor,
    Comms,
    Relay,
}

/// <summary>
/// A place marines can run the uplink from: a sensor tower, a comms tower, or a field uplink relay.
/// Added at rule start to every planet sensor and comms tower; the field relay prototype carries it.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodUplinkSiteComponent : Component
{
    [DataField]
    public BadBloodUplinkSiteKind Kind;

    /// <summary>
    /// The uplink do-after at this site.
    /// </summary>
    [DataField]
    public TimeSpan UplinkTime = TimeSpan.FromSeconds(90);

    /// <summary>
    /// The container a looted Bad Blood bracer is put into for the bracer signature.
    /// </summary>
    [DataField]
    public string BracerSlot = "bad_blood_uplink_bracer";

    /// <summary>
    /// The bracer tinker step: 0 nothing inserted, 1 inserted, 2 cut, 3 done.
    /// </summary>
    [ViewVariables]
    public int BracerStage;
}

/// <summary>
/// A marine objective site Bad Bloods can sabotage: uplink sites and colony reactors. Added at rule start.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodSabotageTargetComponent : Component
{
    /// <summary>
    /// Bad Bloods cannot sabotage this target before this time. Set after a repair, or when the Vessel Strike lock is
    /// re-routed to it. The single source of truth for sabotage immunity.
    /// </summary>
    [ViewVariables]
    public TimeSpan ImmuneUntil;

    // ---- FLOW-509 (BadBloodSabotageSystem) ----

    /// <summary>
    /// The Bad Blood sabotage do-after.
    /// </summary>
    [DataField]
    public TimeSpan SabotageTime = TimeSpan.FromSeconds(25);

    /// <summary>
    /// How many damage stages a sabotage knocks a colony reactor down (Working → Wrench → Wire → Weld).
    /// </summary>
    [DataField]
    public int SabotageStages = 2;

    /// <summary>
    /// The damage a sabotage deals to a comms tower, at least its Breakage threshold.
    /// </summary>
    [DataField]
    public float CommsDamage = 450;

    /// <summary>
    /// The target was seen broken and is waiting for its repair; the repair starts <see cref="ImmuneUntil"/>.
    /// </summary>
    [ViewVariables]
    public bool AwaitingRepair;
}
