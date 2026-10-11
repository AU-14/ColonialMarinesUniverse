using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    public readonly List<CMUExpeditionDanger> KnownDanger = new();
    public readonly List<CMUExpeditionFireLane> KnownFireLanes = new();
    public TimeSpan NextFireLaneObservation;
    public TimeSpan IncomingFireUntil;
    public TimeSpan NextIncomingResponse;
    public TimeSpan NextIncomingObservation;
    public EntityCoordinates? LastSafePosition;
    public string IncomingFireDecision = "clear";
    public int IncomingFireEscapes;
}

public readonly record struct CMUExpeditionDanger(EntityCoordinates Point, TimeSpan Until, float Weight, float Radius);

public readonly record struct CMUExpeditionFireLane(EntityCoordinates Start, EntityCoordinates End,
    TimeSpan Until, float Weight, float Width);
