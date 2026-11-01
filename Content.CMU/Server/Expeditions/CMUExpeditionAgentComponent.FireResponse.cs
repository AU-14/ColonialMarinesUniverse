using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    public EntityUid? FireRescueTarget;
    public EntityCoordinates? FireRescueDestination;
    public TimeSpan FireRescueUntil;
    public TimeSpan FireRescueHandUntil;
    public TimeSpan NextFireAssist;
    public TimeSpan NextFireResist;
    public EntityCoordinates? FireEscapeDestination;
    public TimeSpan FireEscapeUntil;
    public TimeSpan NextFireEscape;
    public bool ResistedGroundFire;
    public string FireResponseDecision = "clear";
    public int FirePats;
    public int FireRolls;
    public int FireEscapes;
}
