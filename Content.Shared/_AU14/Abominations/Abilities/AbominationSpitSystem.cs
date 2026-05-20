using System.Numerics;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Spawners;

namespace Content.Shared._AU14.Abominations.Abilities;

public sealed partial class AbominationSpitSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationSpitComponent, AbominationSpitActionEvent>(OnSpitAction);
    }

    private void OnSpitAction(Entity<AbominationSpitComponent> ent, ref AbominationSpitActionEvent args)
    {
        if (args.Handled)
            return;

        var origin = _transform.GetMapCoordinates(ent);
        var target = _transform.ToMapCoordinates(args.Target);
        if (origin.MapId != target.MapId || origin.Position == target.Position)
            return;

        args.Handled = true;

        _audio.PlayPredicted(ent.Comp.Sound, ent, ent);

        // Spawn + launch is server-authoritative; client predicts the sound only.
        if (_net.IsClient)
            return;

        var direction = target.Position - origin.Position;
        var velocity = direction.Normalized() * ent.Comp.Speed;

        var projectile = Spawn(ent.Comp.Projectile, origin);

        // ShootProjectile sets up the ProjectileComponent.Shooter, body type and
        // initial velocity correctly. Doing this manually is what made the
        // previous version's networking go sideways.
        _gun.ShootProjectile(projectile, velocity, Vector2.Zero, ent.Owner, ent.Owner, ent.Comp.Speed);

        // Hard despawn so missed projectiles don't litter the map.
        var lifetime = ent.Comp.Range / ent.Comp.Speed;
        var despawn = EnsureComp<TimedDespawnComponent>(projectile);
        despawn.Lifetime = lifetime;
    }
}
