using Content.Server.NPC.Queries.Considerations;

namespace Content.Server.CMU14.Weapons.Ranged;

/// <summary>
/// Returns 1f when no solid ballistic obstruction blocks the target. Partial cover may intercept
/// individual bullets, but still permits acquisition, just as it permits shots by a player.
/// </summary>
public sealed partial class TargetBulletLOSCon : UtilityConsideration;
