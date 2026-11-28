using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.CMU14.Weapons;

/// <summary>
/// FLOW-02: swaps the heading of a spread shot's first projectile (the cartridge's own, fired at the fan's left edge)
/// with the middle one's, so the original flies straight at the target. Runs before <see cref="CMGunSystem"/>'s
/// fixed-point handler, which keeps each projectile's own heading for multi-projectile shots.
/// </summary>
public sealed partial class CMUCenteredProjectileSpreadSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUCenteredProjectileSpreadComponent, AmmoShotEvent>(OnAmmoShot, before: [typeof(CMGunSystem)]);
    }

    private void OnAmmoShot(Entity<CMUCenteredProjectileSpreadComponent> ent, ref AmmoShotEvent args)
    {
        CenterOriginal(args.FiredProjectiles);
    }

    /// <summary>
    /// For each cartridge's group of <see cref="ProjectileSpreadComponent.Count"/> projectiles (the original first,
    /// then its spread copies), gives the original the middle projectile's velocity and rotation and the middle one
    /// the original's. Shots that are not a whole number of odd-sized spread groups are left alone.
    /// </summary>
    public void CenterOriginal(IReadOnlyList<EntityUid> projectiles)
    {
        if (projectiles.Count == 0 ||
            !TryComp(projectiles[0], out ProjectileSpreadComponent? spread) ||
            spread.Count < 3 ||
            spread.Count % 2 == 0 ||
            projectiles.Count % spread.Count != 0)
        {
            return;
        }

        for (var start = 0; start < projectiles.Count; start += spread.Count)
            Swap(projectiles[start], projectiles[start + spread.Count / 2]);
    }

    private void Swap(EntityUid first, EntityUid middle)
    {
        if (!TryComp(first, out PhysicsComponent? firstPhysics) ||
            !TryComp(middle, out PhysicsComponent? middlePhysics))
        {
            return;
        }

        var firstVelocity = firstPhysics.LinearVelocity;
        var middleVelocity = middlePhysics.LinearVelocity;
        _physics.SetLinearVelocity(first, middleVelocity, body: firstPhysics);
        _physics.SetLinearVelocity(middle, firstVelocity, body: middlePhysics);

        var firstRotation = _transform.GetWorldRotation(first);
        var middleRotation = _transform.GetWorldRotation(middle);
        _transform.SetWorldRotation(first, middleRotation);
        _transform.SetWorldRotation(middle, firstRotation);
    }
}
