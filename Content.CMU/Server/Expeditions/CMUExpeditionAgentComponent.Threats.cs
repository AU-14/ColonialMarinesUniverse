using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    public readonly Dictionary<EntityUid, TimeSpan> RecentShooters = new();
    public readonly CMUExpeditionThreat?[] ThreatSectors = new CMUExpeditionThreat?[8];
    public readonly Dictionary<EntityCoordinates, float> ExposureScores = new();
    public readonly Dictionary<EntityUid, int> TargetAssignments = new();
    public int OccupiedThreatSectors;
    public bool Crossfire;
    public TimeSpan NextCrossfireMove;
    public int CrossfireMoves;
    public TimeSpan NextFlankResponse;
    public int FlankResponses;
}

public readonly record struct CMUExpeditionThreat(EntityUid Target, EntityCoordinates Position, float Weight);
