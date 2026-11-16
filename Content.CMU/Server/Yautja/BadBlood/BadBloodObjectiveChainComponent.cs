using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// The marine signature avenue that latched the signature step.
/// </summary>
public enum BadBloodSignature : byte
{
    None,
    Bracer,
    Totem,
    Lodge,
}

/// <summary>
/// The marine objective chain against the Bad Blood clan: the infrastructure gate, the clan signature and the uplink.
/// Lives on the rule entity next to <see cref="Content.Shared.CMU14.Yautja.BadBlood.BadBloodCampRuleComponent"/>.
/// Every step latches; timers pause and never reset. docs/GraphAudit-BadBlood-Objectives.md §5.3.
/// Written only by <see cref="BadBloodObjectiveChainSystem"/>, except <see cref="LockSite"/>, which the Vessel Strike
/// moves when the lock is re-routed.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodObjectiveChainComponent : Component
{
    // ---- tunables ----

    /// <summary>
    /// How long a colony reactor must run (summed, pausing while it is down) for the power part of the gate.
    /// </summary>
    [DataField]
    public TimeSpan ReactorHold = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How many of the three infrastructure parts (power, comms, sensor) open the gate.
    /// </summary>
    [DataField]
    public int GatePartsRequired = 2;

    /// <summary>
    /// The Totem survey do-after on a desecrated Totem, for the Totem signature.
    /// </summary>
    [DataField]
    public TimeSpan TotemSurveyTime = TimeSpan.FromSeconds(75);

    /// <summary>
    /// The Lodge survey do-after for the Lodge signature.
    /// </summary>
    [DataField]
    public TimeSpan LodgeSurveyTime = TimeSpan.FromSeconds(75);

    [DataField]
    public TimeSpan Tick = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Each tool do-after of the bracer signature (cut, then wrench) at an uplink site.
    /// </summary>
    [DataField]
    public TimeSpan BracerStepTime = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The requisitions crate holding a field uplink relay kit, added to GOVFOR consoles on Bad Blood rounds.
    /// </summary>
    [DataField]
    public EntProtoId FieldRelayCrate = "CMUCrateBadBloodFieldRelay";

    [DataField]
    public int FieldRelayCost = 1500;

    /// <summary>
    /// The console category the relay crate goes into. GOVFOR consoles always have it.
    /// </summary>
    [DataField]
    public string FieldRelayCategory = "Research";

    // ---- state ----

    [ViewVariables]
    public TimeSpan NextTick;

    [ViewVariables]
    public TimeSpan ReactorHeld;

    [ViewVariables]
    public bool PowerMet;

    [ViewVariables]
    public bool CommsMet;

    [ViewVariables]
    public bool SensorMet;

    [ViewVariables]
    public bool GateMet;

    [ViewVariables]
    public BadBloodSignature Signature;

    [ViewVariables]
    public bool Uplinked;

    /// <summary>
    /// The uplink site the Vessel Strike's lock runs through. Set by the uplink; moved by a re-route.
    /// </summary>
    [ViewVariables]
    public EntityUid? LockSite;
}
