using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    public TimeSpan NextExposedStep;
    public TimeSpan ExposedStepUntil;
    public EntityCoordinates? LastExposedStepOrigin;
    public TimeSpan AvoidExposedStepOriginUntil;
    public int ExposedSteps;
    public string ExposureMovementDecision = "settled";
}
