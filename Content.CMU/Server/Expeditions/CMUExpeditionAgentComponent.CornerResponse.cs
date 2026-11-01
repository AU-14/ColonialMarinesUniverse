using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    public bool CornerHolding;
    public EntityCoordinates? CornerDangerPoint;
    public TimeSpan CornerUntil;
    public EntityCoordinates? CornerDestination;
    public TimeSpan CornerMoveUntil;
    public bool CornerFlanking;
    public int CornerFlankCandidate;
    public TimeSpan CornerProbeUntil;
    public TimeSpan NextCornerSearch;
    public string CornerDecision = "clear";
    public int CornerFlanks;
    public int CornerStagingMoves;
}
