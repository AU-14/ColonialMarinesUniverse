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
}
