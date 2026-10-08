using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    [DataField] public float WeaponRecoveryRange = 6;
    [DataField] public float MeleeStandoffRange = 6;
    [DataField] public float RushLookAhead = 0.65f;
    public EntityUid? Rifle;
    public TimeSpan NextWeaponRecovery;
    public int WeaponsRecovered;
    public string WeaponRecoveryDecision = "armed";
    public EntityUid? RushTarget;
    public bool LastContactWasMelee;
    public List<(EntityCoordinates Position, Vector2 Velocity)> MeleeThreats = new();
    public TimeSpan SpacingUntil;
    public EntityCoordinates? SpacingDestination;
    public TimeSpan SpacingMoveUntil;
    public TimeSpan NextSpacingSearch;
    public string SpacingDecision = "idle";
    public TimeSpan NextDispersion;
    public TimeSpan LastShotAt;
    public int RejectedCover;
}
