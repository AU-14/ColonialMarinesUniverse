using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    public EntityCoordinates? AssaultDestination;
    public bool AssaultObjectiveSecured;
    public EntityCoordinates? AssaultBoundDestination;
    public TimeSpan AssaultNextBound;
    public TimeSpan NextAssaultRoute;
    public int AssaultBoundsCompleted;
    public string AssaultDecision = "none";
}
