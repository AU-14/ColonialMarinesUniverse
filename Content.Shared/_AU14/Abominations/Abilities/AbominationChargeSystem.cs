using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._AU14.Abominations.Abilities;

/// <summary>
/// Crusher charge: long ranged lunge that damages mobs AND structures it
/// ploughs through. Sibling of AbominationLeapSystem but heavier.
/// </summary>
public sealed partial class AbominationChargeSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbominationChargeComponent, AbominationChargeActionEvent>(OnChargeAction);
        SubscribeLocalEvent<AbominationChargingComponent, StartCollideEvent>(OnChargingCollide);
    }

    private void OnChargeAction(Entity<AbominationChargeComponent> ent, ref AbominationChargeActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<PhysicsComponent>(ent, out var physics))
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

        Math.Clamp(direction.Length(), 0.1f, ent.Comp.Range);
        var velocity = Vector2.Normalize(direction) * ent.Comp.Strength;

        _physics.SetLinearVelocity(ent, Vector2.Zero, body: physics);
        _physics.ApplyLinearImpulse(ent, velocity * physics.Mass, body: physics);

        var charging = EnsureComp<AbominationChargingComponent>(ent);
        charging.EndsAt = _timing.CurTime + ent.Comp.FlightDuration;
        charging.KnockdownTime = ent.Comp.KnockdownTime;
        charging.MobDamage = ent.Comp.MobDamage;
        charging.StructureDamage = ent.Comp.StructureDamage;
        Dirty(ent, charging);

        if (ent.Comp.ChargeSound != null)
            _audio.PlayPvs(ent.Comp.ChargeSound, ent);
    }

    private void OnChargingCollide(Entity<AbominationChargingComponent> ent, ref StartCollideEvent args)
    {
        var target = args.OtherEntity;
        if (target == ent.Owner || HasComp<AbominationComponent>(target))
            return;

        if (HasComp<MobStateComponent>(target))
        {
            if (_mobState.IsDead(target))
                return;
            if (_net.IsServer)
            {
                _stun.TryParalyze(target, ent.Comp.KnockdownTime, true);
                _damageable.TryChangeDamage(target, ent.Comp.MobDamage, origin: ent.Owner);
            }
            return;
        }

        // Structure / wall / anything else damageable in the way.
        if (_net.IsServer && HasComp<DamageableComponent>(target))
            _damageable.TryChangeDamage(target, ent.Comp.StructureDamage, origin: ent.Owner);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AbominationChargingComponent>();
        while (query.MoveNext(out var uid, out var charging))
        {
            if (charging.EndsAt > now)
                continue;

            if (TryComp<PhysicsComponent>(uid, out var physics))
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);

            RemCompDeferred<AbominationChargingComponent>(uid);
        }
    }
}
