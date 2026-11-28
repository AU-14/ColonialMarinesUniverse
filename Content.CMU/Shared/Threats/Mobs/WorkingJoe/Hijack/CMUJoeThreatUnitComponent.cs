namespace Content.Shared.CMU14.Threats.Mobs.WorkingJoe.Hijack;

/// <summary>
/// A unit of the Working Joes shipment threat: a hijacked Joe, a threat-promoted Early Warning Joe, or a converted
/// synthetic. Counted by <c>CMUKillAllHijackedJoesRule</c>; the colony wins when every unit is dead, deleted or off
/// the planet. A cure removes it. Not networked.
/// </summary>
[RegisterComponent]
public sealed partial class CMUJoeThreatUnitComponent : Component;
