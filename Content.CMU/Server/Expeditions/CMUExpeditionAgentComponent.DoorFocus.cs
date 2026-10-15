using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    public TimeSpan NextContactDoor;
    public TimeSpan NextDoorClearance;
    public EntityCoordinates? DoorClearanceDestination;
    public TimeSpan DoorClearanceUntil;
}
