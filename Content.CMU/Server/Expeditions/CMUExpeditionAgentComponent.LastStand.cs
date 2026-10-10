using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    public EntityUid? LastStandTarget;
    public EntityCoordinates? LastStandOrigin;
    public EntityUid? PendingMeleeWeapon;
    public EntityUid? DrawnMeleeWeapon;
    public TimeSpan MeleeWeaponReadyAt;
    public TimeSpan NextMeleeWeaponChoice;
}
