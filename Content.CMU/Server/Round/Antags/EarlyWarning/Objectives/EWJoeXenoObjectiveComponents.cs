using Content.Shared.Whitelist;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// An Early Warning Joe objective that needs xenofauna (non-humanoid threat life, see
/// <c>EWJoeTargetRules.IsXenofauna</c>) to exist. Refused on assignment unless a living xenofauna body is on
/// the owner's planet z-network (RB-37: a selected threat flagged <c>xenofauna</c> that has not spawned does not
/// count). Humanoid-threat rounds, Insurgency, FoF and Colony Fall before its threat arrives therefore refuse
/// it, and the ladder's pool walk simply moves on.
/// ARCH-02: once living xenofauna has been seen on the planet for this directive, the directive is withdrawn
/// when none is left (a gather directive also keeps going while enough victims lie on the planet).
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeRequiresXenofaunaComponent : Component
{
    /// <summary>Living xenofauna stood on the planet at assignment or at a later check.</summary>
    [DataField]
    public bool SeenLiving;
}

/// <summary>
/// "Maim N Xenofauna": a counter objective (pair with CounterCondition and NumberObjective) raised each
/// time xenofauna the Joe brought down goes critical, or dies outright. Each body counts once.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeXenoCounterComponent : Component
{
    [DataField]
    public HashSet<EntityUid> Counted = new();
}

/// <summary>
/// "Gather N Xenofauna victims in an area": on assignment pins a powered area (one with an APC) on the
/// owner's planet; complete while N xenofauna victims lie in it at once (burst or infected hosts, biomorph
/// or dormant-spore carriers, or organics xenofauna killed).
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeDeliverVictimsConditionComponent : Component
{
    /// <summary>The area prototype the victims must be brought to. Set on assignment.</summary>
    [DataField]
    public string? AreaId;

    [DataField]
    public string AreaName = string.Empty;

    [DataField]
    public int Count;

    [DataField]
    public int Min = 1;

    [DataField]
    public int Max = 2;

    /// <summary>Entities whose areas can be picked; component names only.</summary>
    [DataField(required: true)]
    public EntityWhitelist AreaSource = new();

    [DataField(required: true)]
    public LocId Title;

    [DataField(required: true)]
    public LocId Description;
}

/// <summary>
/// The Early Warning Joe that last hurt this xenofauna body, and when. Xenos fall into critical through a
/// grace period whose end re-checks thresholds with no origin, so the maim count falls back to this when the
/// state change itself does not name the Joe. Server bookkeeping.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeXenoHitComponent : Component
{
    [DataField]
    public EntityUid Joe;

    [DataField]
    public TimeSpan At;
}

/// <summary>
/// Stamped on an organic whose critical or killing blow resolved to xenofauna. While the body is dead it
/// counts as a xenofauna victim for "Gather N Xenofauna victims", which gives threats with no infection
/// mechanic (apes, wendigo) a victim source. Removed if the body is revived. Server bookkeeping.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeXenofaunaKilledComponent : Component
{
    /// <summary>The xenofauna the blow resolved to.</summary>
    [DataField]
    public EntityUid Killer;

    [DataField]
    public TimeSpan At;
}
