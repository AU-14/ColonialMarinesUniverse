using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    [DataField] public float SmokeWithdrawalClearance = 5;
    public EntityCoordinates? SmokeWithdrawalDestination;
    public EntityCoordinates? SmokeEscapeDestination;
    public TimeSpan SmokeEscapeUntil;
    public TimeSpan NextSmokeEscape;
}
