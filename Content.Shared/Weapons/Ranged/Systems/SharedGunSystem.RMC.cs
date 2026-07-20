using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    public void SetProjectileTarget(EntityUid projectile, EntityUid target)
    {
        var targeted = EnsureComp<TargetedProjectileComponent>(projectile);
        targeted.Target = target;
        Dirty(projectile, targeted);
    }
}
