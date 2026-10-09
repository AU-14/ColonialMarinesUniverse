using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    public TimeSpan NextLocalDetour;
    public int LocalDetours;
    public EntityCoordinates? OrderRally;
    public TimeSpan? CohesionWaitSince;
    public TimeSpan NextCohesionWait;
    public TimeSpan? OrderBlockedSince;
    public int OrderFailures;
    public EntityCoordinates? LastOrderProgressPosition;
    public EntityUid? WaitingForDoor;
    public TimeSpan DoorWaitUntil;
    public bool DoorOpenRequested;
    public EntityUid? FailedDoor;
    public TimeSpan AvoidDoorUntil;
    public string DoorDecision = "none";
    public int DoorsOpened;
    public int DoorFailures;
}
