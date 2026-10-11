using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    public EntityUid? TrafficNudgeRequester;
    public EntityCoordinates? TrafficNudgeDestination;
    public TimeSpan TrafficNudgeUntil;
    public TimeSpan NextTrafficNudge;
    public int TrafficNudges;
}
