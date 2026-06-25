using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Damage.ObstacleSlamming;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Threats.Mobs.Abomination.Abilities;

public sealed partial class AbominationLeapSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private RMCObstacleSlammingSystem _obstacleSlamming = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

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

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        TimeSpan now = _timing.CurTime;
        EntityQueryEnumerator<AbominationLeapingComponent> query = EntityQueryEnumerator<AbominationLeapingComponent>();
        while (query.MoveNext(out EntityUid uid, out AbominationLeapingComponent? leaping))
        {
            if (leaping.EndsAt > now)
                continue;

            StopLeap((uid, leaping));
        }
    }

    private void OnLeapAction(Entity<AbominationLeapComponent> ent, ref AbominationLeapActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(ent, out PhysicsComponent? physics))
            return;

        if (HasComp<AbominationLeapingComponent>(ent))
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        MapCoordinates origin = _transform.GetMapCoordinates(ent);
        var target = _transform.ToMapCoordinates(args.Target);
        if (origin.MapId != target.MapId)
            return;

        Vector2 direction = target.Position - origin.Position;
        if (direction == Vector2.Zero)
            return;

        float length = direction.Length();
        float distance = Math.Clamp(length, 0.1f, ent.Comp.Range);
        direction *= distance / length;
        Vector2 impulse = Vector2.Normalize(direction) * ent.Comp.Strength * physics.Mass;

        var leaping = EnsureComp<AbominationLeapingComponent>(ent);
        leaping.EndsAt = _timing.CurTime + ent.Comp.FlightDuration;
        leaping.KnockdownTime = ent.Comp.KnockdownTime;
        leaping.Damage = ent.Comp.Damage;
        Dirty(ent, leaping);

        _obstacleSlamming.MakeImmune(ent, (float)ent.Comp.FlightDuration.TotalSeconds + 0.5f);
        _physics.SetLinearVelocity(ent, Vector2.Zero, body: physics);
        _physics.ApplyLinearImpulse(ent, impulse, body: physics);
        _physics.SetBodyStatus(ent, physics, BodyStatus.InAir);

        // Phase through barricades / mid-impassable while flying.
        if (TryComp(ent, out FixturesComponent? fixtures) && fixtures.Fixtures.Count > 0)
        {
            KeyValuePair<string, Fixture> fixture = fixtures.Fixtures.First();
            _physics.SetCollisionMask(ent, fixture.Key, fixture.Value, fixture.Value.CollisionMask & ~(int)PhaseGroup);
        }

        if (ent.Comp.LeapSound != null)
            _audio.PlayPvs(ent.Comp.LeapSound, ent);

        // Handle the case where the target is already touching us when we leap.
        foreach (EntityUid contact in _physics.GetContactingEntities(ent.Owner, physics))
        {
            if (ApplyHit((ent.Owner, leaping), contact))
                return;
        }
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

    private void OnLeapingRemove(Entity<AbominationLeapingComponent> ent, ref ComponentRemove args)
    {
        if (TryComp(ent, out PhysicsComponent? physics))
        {
            _physics.SetLinearVelocity(ent, Vector2.Zero, body: physics);
            _physics.SetBodyStatus(ent, physics, BodyStatus.OnGround);
        }

        if (TryComp(ent, out FixturesComponent? fixtures) && fixtures.Fixtures.Count > 0)
        {
            KeyValuePair<string, Fixture> fixture = fixtures.Fixtures.First();
            _physics.SetCollisionMask(ent, fixture.Key, fixture.Value, fixture.Value.CollisionMask | (int)PhaseGroup);
        }
    }

    private void StopLeap(Entity<AbominationLeapingComponent> ent)
    {
        RemCompDeferred<AbominationLeapingComponent>(ent);
    }
}
