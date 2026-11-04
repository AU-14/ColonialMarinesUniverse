using Robust.Shared.Map;
using Content.Shared.CMU14.Expeditions;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    [DataField] public bool CoordinateSquads = true;
    public bool HoldPosition;
    public bool AutoPatrol;
    public EntityCoordinates? AutoPatrolAnchor;
    public TimeSpan NextAutoPatrol;
    public Dictionary<int, CMUPatrolMemory> PatrolMemory = new();
    public EntityCoordinates? PatrolMemoryAnchor;
    public EntityCoordinates? AutoPatrolDestination;
    public int AutoPatrolSector = -1;
    public bool AutoPatrolVisited;
    public TimeSpan AutoPatrolLegStarted;
    public TimeSpan? AutoPatrolCohesionSince;
    public int PatrolVisits;
    public int PatrolSearches;
    public int PatrolFailures;
    public string PatrolDecision = "disabled";
    public EntityUid? SupportSquadRoot;
    public EntityCoordinates? SupportOrigin;
    public EntityUid? SupportTarget;
    public EntityCoordinates? SupportContact;
    public int SupportFlankSide;
    public float SupportBestDistance = float.MaxValue;
    public TimeSpan SupportProgressAt;
    public TimeSpan NextAssistance;
    public int AssistanceAccepted;
    public int AssistanceDeclined;
    public string AssistanceDecision = "none";
    public TimeSpan SupportUntil;
    public string OperationsDecision = "local-orders";
}
