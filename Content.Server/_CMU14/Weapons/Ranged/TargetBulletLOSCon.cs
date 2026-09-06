using Content.Server.NPC.Queries.Considerations;

namespace Content.Server._CMU14.Weapons.Ranged;

/// <summary>
/// Returns (1f) only when the physics path a bullet would take to the target is clear
/// (BulletImpassable mask). Stationary guns use this instead of the sight-occluder
/// LOS consideration so they never acquire or keep targets they cannot shoot.
/// </summary>
public sealed partial class TargetBulletLOSCon : UtilityConsideration;
