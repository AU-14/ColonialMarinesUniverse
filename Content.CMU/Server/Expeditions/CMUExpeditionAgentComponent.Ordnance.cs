using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    public EntityCoordinates? GrenadeObservedContact;
    public TimeSpan GrenadeContactUntil;
    public EntityCoordinates? GrenadeThrowStance;
    public TimeSpan NextGrenadeStaging;
}
