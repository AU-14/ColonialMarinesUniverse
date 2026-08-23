using System.Numerics;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stun;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaShieldBashSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RMCDazedSystem _dazed = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaShieldBashComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<YautjaShieldBashComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit ||
            args.HitEntities.Count == 0 ||
            !HasComp<YautjaComponent>(args.User) ||
            ent.Comp.NextBashAt > _timing.CurTime)
        {
            return;
        }

        foreach (var target in args.HitEntities)
        {
            if (target == args.User || Deleted(target))
                continue;

            if (!TryBash(ent, args.User, target))
                continue;

            ent.Comp.NextBashAt = _timing.CurTime + ent.Comp.Cooldown;
            break;
        }
    }

    private bool TryBash(Entity<YautjaShieldBashComponent> ent, EntityUid user, EntityUid target)
    {
        var origin = _transform.GetMapCoordinates(user);
        var destination = _transform.GetMapCoordinates(target);
        if (origin.MapId != destination.MapId)
            return false;

        var direction = destination.Position - origin.Position;
        if (direction == Vector2.Zero)
            return false;

        direction = Vector2.Normalize(direction) * ent.Comp.ThrowDistance;
        _throwing.TryThrow(
            target,
            direction,
            ent.Comp.ThrowSpeed,
            user,
            compensateFriction: true,
            doSpin: false);
        _dazed.TryDaze(target, ent.Comp.DazeDuration, true);
        _slow.TrySlowdown(target, ent.Comp.SlowDuration, ignoreDurationModifier: true);
        return true;
    }
}
