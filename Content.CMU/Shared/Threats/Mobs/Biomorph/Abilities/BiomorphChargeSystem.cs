using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Threats.Mobs.Biomorph.Abilities;

/// <summary>
///     Crusher charge: long ranged lunge that damages mobs AND structures it
///     ploughs through. Sibling of AbominationLeapSystem but heavier.
/// </summary>
public sealed partial class BiomorphChargeSystem : EntitySystem
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
        SubscribeLocalEvent<BiomorphChargeComponent, BiomorphChargeActionEvent>(OnChargeAction);
        SubscribeLocalEvent<BiomorphChargingComponent, StartCollideEvent>(OnChargingCollide);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        TimeSpan now = _timing.CurTime;
        EntityQueryEnumerator<BiomorphChargingComponent> query
            = EntityQueryEnumerator<BiomorphChargingComponent>();
        while (query.MoveNext(out EntityUid uid, out BiomorphChargingComponent? charging))
        {
            if (charging.EndsAt > now)
                continue;

            if (TryComp(uid, out PhysicsComponent? physics))
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);

            RemCompDeferred<BiomorphChargingComponent>(uid);
        }
    }

    private void OnChargeAction(Entity<BiomorphChargeComponent> ent, ref BiomorphChargeActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(ent, out PhysicsComponent? physics))
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

        Math.Clamp(direction.Length(), 0.1f, ent.Comp.Range);
        Vector2 velocity = Vector2.Normalize(direction) * ent.Comp.Strength;

        _physics.SetLinearVelocity(ent, Vector2.Zero, body: physics);
        _physics.ApplyLinearImpulse(ent, velocity * physics.Mass, body: physics);

        var charging = EnsureComp<BiomorphChargingComponent>(ent);
        charging.EndsAt = _timing.CurTime + ent.Comp.FlightDuration;
        charging.KnockdownTime = ent.Comp.KnockdownTime;
        charging.MobDamage = ent.Comp.MobDamage;
        charging.StructureDamage = ent.Comp.StructureDamage;
        Dirty(ent, charging);

        if (ent.Comp.ChargeSound != null)
            _audio.PlayPvs(ent.Comp.ChargeSound, ent);
    }

    private void OnChargingCollide(Entity<BiomorphChargingComponent> ent, ref StartCollideEvent args)
    {
        EntityUid target = args.OtherEntity;
        if (target == ent.Owner || HasComp<BiomorphComponent>(target))
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
}
