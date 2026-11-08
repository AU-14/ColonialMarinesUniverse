using Content.Shared.Whitelist;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// An Early Warning Joe objective that only exists in a Distress Signal round (an admin-made Joe there:
/// the antag is blacklisted from that preset's roll). Refused on assignment anywhere else, so the ladder's
/// pool walk simply moves on.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeDistressSignalOnlyComponent : Component;

/// <summary>
/// "Maim N Xenofauna": a counter objective (pair with CounterCondition and NumberObjective) raised each
/// time a xeno the Joe brought down goes critical, or dies outright. Each xeno counts once.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeXenoCounterComponent : Component
{
    [DataField]
    public HashSet<EntityUid> Counted = new();
}

/// <summary>
/// "Gather N Xenofauna victims in an area": on assignment pins a powered area (one with an APC) on the
/// owner's planet; complete while N burst corpses or still-infected bodies lie in it at once.
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
/// The Early Warning Joe that last hurt this xeno, and when. Xenos fall into critical through a grace
/// period whose end re-checks thresholds with no origin, so the maim count falls back to this when the
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
