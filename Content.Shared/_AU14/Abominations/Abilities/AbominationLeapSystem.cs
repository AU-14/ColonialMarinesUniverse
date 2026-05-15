using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Damage.ObstacleSlamming;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._AU14.Abominations.Abilities;

public sealed class AbominationLeapSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly RMCObstacleSlammingSystem _obstacleSlamming = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    // Collision groups we phase through while leaping so the abomination doesn't
    // get instantly snagged on barricades and mid-walls.
    private const CollisionGroup PhaseGroup = CollisionGroup.BarricadeImpassable | CollisionGroup.MidImpassable;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationLeapComponent, AbominationLeapActionEvent>(OnLeapAction);
        SubscribeLocalEvent<AbominationLeapingComponent, StartCollideEvent>(OnLeapingCollide);
        SubscribeLocalEvent<AbominationLeapingComponent, ComponentRemove>(OnLeapingRemove);
        SubscribeLocalEvent<AbominationLeapingComponent, PhysicsSleepEvent>(OnLeapingPhysicsSleep);
    }

    private void OnLeapAction(Entity<AbominationLeapComponent> ent, ref AbominationLeapActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<PhysicsComponent>(ent, out var physics))
            return;

        if (HasComp<AbominationLeapingComponent>(ent))
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        var origin = _transform.GetMapCoordinates(ent);
        var target = _transform.ToMapCoordinates(args.Target);
        if (origin.MapId != target.MapId)
            return;

        var direction = target.Position - origin.Position;
        if (direction == Vector2.Zero)
            return;

        var length = direction.Length();
        var distance = Math.Clamp(length, 0.1f, ent.Comp.Range);
        direction *= distance / length;
        var impulse = Vector2.Normalize(direction) * ent.Comp.Strength * physics.Mass;

        var leaping = EnsureComp<AbominationLeapingComponent>(ent);
        leaping.EndsAt = _timing.CurTime + ent.Comp.FlightDuration;
        leaping.KnockdownTime = ent.Comp.KnockdownTime;
        leaping.Damage = ent.Comp.Damage;
        Dirty(ent, leaping);

        _obstacleSlamming.MakeImmune(ent, (float) ent.Comp.FlightDuration.TotalSeconds + 0.5f);
        _physics.SetLinearVelocity(ent, Vector2.Zero, body: physics);
        _physics.ApplyLinearImpulse(ent, impulse, body: physics);
        _physics.SetBodyStatus(ent, physics, BodyStatus.InAir);

        // Phase through barricades / mid-impassable while flying.
        if (TryComp<FixturesComponent>(ent, out var fixtures) && fixtures.Fixtures.Count > 0)
        {
            var fixture = fixtures.Fixtures.First();
            _physics.SetCollisionMask(ent, fixture.Key, fixture.Value, fixture.Value.CollisionMask & ~(int) PhaseGroup);
        }

        if (ent.Comp.LeapSound != null)
            _audio.PlayPvs(ent.Comp.LeapSound, ent);

        // Handle the case where the target is already touching us when we leap.
        foreach (var contact in _physics.GetContactingEntities(ent.Owner, physics))
            if (ApplyHit((ent.Owner, leaping), contact))
                return;
    }

    private void OnLeapingCollide(Entity<AbominationLeapingComponent> ent, ref StartCollideEvent args)
    {
        ApplyHit(ent, args.OtherEntity);
    }

    private bool ApplyHit(Entity<AbominationLeapingComponent> ent, EntityUid target)
    {
        if (target == ent.Owner || HasComp<AbominationComponent>(target))
            return false;

        if (!HasComp<MobStateComponent>(target) || _mobState.IsDead(target))
            return false;

        if (_net.IsServer)
        {
            _stun.TryParalyze(target, ent.Comp.KnockdownTime, true);
            _damageable.TryChangeDamage(target, ent.Comp.Damage, origin: ent.Owner);
        }

        StopLeap(ent);
        return true;
    }

    private void OnLeapingPhysicsSleep(Entity<AbominationLeapingComponent> ent, ref PhysicsSleepEvent args)
    {
        StopLeap(ent);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AbominationLeapingComponent>();
        while (query.MoveNext(out var uid, out var leaping))
        {
            if (leaping.EndsAt > now)
                continue;

            StopLeap((uid, leaping));
        }
    }

    private void OnLeapingRemove(Entity<AbominationLeapingComponent> ent, ref ComponentRemove args)
    {
        if (TryComp<PhysicsComponent>(ent, out var physics))
        {
            _physics.SetLinearVelocity(ent, Vector2.Zero, body: physics);
            _physics.SetBodyStatus(ent, physics, BodyStatus.OnGround);
        }

        if (TryComp<FixturesComponent>(ent, out var fixtures) && fixtures.Fixtures.Count > 0)
        {
            var fixture = fixtures.Fixtures.First();
            _physics.SetCollisionMask(ent, fixture.Key, fixture.Value, fixture.Value.CollisionMask | (int) PhaseGroup);
        }
    }

    private void StopLeap(Entity<AbominationLeapingComponent> ent)
    {
        RemCompDeferred<AbominationLeapingComponent>(ent);
    }
}
