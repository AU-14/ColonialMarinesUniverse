using System.Numerics;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Shared._AU14.Abominations.Abilities;

/// <summary>
/// Spawns a fan of acid spit projectiles in a cone toward the target.
/// Sibling of AbominationSpitSystem but throws a whole spread.
/// </summary>
public sealed partial class AbominationAcidSpraySystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationAcidSprayComponent, AbominationAcidSprayActionEvent>(OnSprayAction);
    }

    private void OnSprayAction(Entity<AbominationAcidSprayComponent> ent, ref AbominationAcidSprayActionEvent args)
    {
        if (args.Handled)
            return;

        var origin = _transform.GetMapCoordinates(ent);
        var target = _transform.ToMapCoordinates(args.Target);
        if (origin.MapId != target.MapId || origin.Position == target.Position)
            return;

        args.Handled = true;

        _audio.PlayPredicted(ent.Comp.Sound, ent, ent);

        if (_net.IsClient)
            return;

        var aim = target.Position - origin.Position;
        var aimAngle = Math.Atan2(aim.Y, aim.X);
        var spread = MathF.PI * ent.Comp.SpreadDegrees / 180f;
        var lifetime = ent.Comp.Range / ent.Comp.Speed;

        for (var i = 0; i < ent.Comp.Shots; i++)
        {
            // Centre projectile flies straight at the target; the rest fan around it.
            var t = ent.Comp.Shots == 1 ? 0f : (float) i / (ent.Comp.Shots - 1) - 0.5f;
            var angle = (float) aimAngle + t * spread;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var velocity = dir * ent.Comp.Speed;

            var projectile = Spawn(ent.Comp.Projectile, origin);
            _gun.ShootProjectile(projectile, velocity, Vector2.Zero, ent.Owner, ent.Owner, ent.Comp.Speed);

            var despawn = EnsureComp<TimedDespawnComponent>(projectile);
            despawn.Lifetime = lifetime;
        }
    }
}
