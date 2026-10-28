using Robust.Shared.Map;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Stun;

public sealed partial class RMCSizeStunSystem
{
    // same idea as RMCProjectileSystem.SetShotOrigin, the point blank stun range measures from here
    public void SetShotOrigin(EntityUid projectile, MapCoordinates origin)
    {
        if (!TryComp(projectile, out RMCStunOnHitComponent? stun))
            return;

        stun.ShotFrom = origin;
        Dirty(projectile, stun);
    }
}
