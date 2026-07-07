using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;


namespace Content.Shared._RMC14.Xenonids.Charge.CursorCharge;

public sealed partial class XenoChargerCollisionSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private XenoSystem _xeno = default!;
    [Dependency] private RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private XenoChargerMovementSystem _movement = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private VehicleSystem _vehicle = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;

    private readonly ProtoId<DamageTypePrototype> _blunt = "Blunt";
    private const float HeadOnDotThreshold = 0.707f; // cos(45°)
    private readonly HashSet<(EntityUid Charger, EntityUid Target)> _hits = new();

    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        SubscribeLocalEvent<XenoChargerComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(Entity<XenoChargerComponent> xeno, ref StartCollideEvent args)
    {
        // Only care if actively moving.
        if (!TryComp(xeno.Owner, out XenoChargerStateComponent? state))
            return;

        if (state.MoveState == XenoChargerMoveState.Idle)
            return;

        _hits.Add((xeno.Owner, args.OtherEntity));
    }

    public void ProcessHits()
    {
        if (_net.IsClient)
            return;

        try
        {
            foreach (var (charger, target) in _hits)
            {
                if (TerminatingOrDeleted(charger) || TerminatingOrDeleted(target))
                    continue;

                if (!TryComp(charger, out XenoChargerComponent? xeno))
                    continue;

                if (!TryComp(charger, out XenoChargerStateComponent? state))
                    continue;

                var now = _timing.CurTime;

                if (state.MoveState == XenoChargerMoveState.Lunging || state.MoveState == XenoChargerMoveState.Charging)
                {
                    if (state.HitEntities.TryGetValue(target, out var lastHit) && now - lastHit < xeno.HitCooldown)
                        continue;

                    state.HitEntities[target] = now;
                }

                switch (state.MoveState)
                {
                    case XenoChargerMoveState.Charging:
                        HandleChargingCollision(charger, xeno, state, target);
                        break;
                    case XenoChargerMoveState.Lunging:
                        HandleLungingCollision(charger, xeno, state, target);
                        break;
                }
            }
        }
        finally
        {
            _hits.Clear();
        }
    }

    // -------------------------------------------------------------------------
    // Charging collisions
    // -------------------------------------------------------------------------

    private void HandleChargingCollision(EntityUid charger, XenoChargerComponent xeno, XenoChargerStateComponent state,
        EntityUid target)
    {
        var stage = state.Stage;
        var atMax = stage == xeno.MaxStage;

        if (_hive.FromSameHive(charger, target))
            return;

        // --- Mobs ---
        if (TryComp(target, out MobStateComponent? mobState))
        {
            if (_mobState.IsDead(target, mobState) || !_xeno.CanAbilityAttackTarget(charger, target))
                return;

            var mult = atMax ? xeno.HumanDamageMultiplierMax : xeno.HumanDamageMultiplier;
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = stage * mult;
            _damageable.TryChangeDamage(target, damage, origin: charger);

            var knockdown = atMax
                ? TimeSpan.FromSeconds(xeno.HumanKnockdownDuration * 2)
                : TimeSpan.FromSeconds(xeno.HumanKnockdownDuration);
            _stun.TryParalyze(target, knockdown, false);

            var origin = _transform.GetMapCoordinates(charger);
            _sizeStun.KnockBack(target, origin, stage * 0.3f, stage * 0.5f, knockBackSpeed: stage);

            if (_net.IsServer)
            {
                _popup.PopupEntity(
                    Loc.GetString("rmc-xeno-charge-knockback-others", ("user", charger), ("target", target)),
                    target,
                    PopupType.MediumCaution
                );
            }

            state.Stage = Math.Max(0, state.Stage - 1);
            Dirty(charger, state);
            return;
        }

        // --- Vehicles ---
        if (HasComp<VehicleComponent>(target))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = stage * xeno.StructureDamageMultiplier;
            _damageable.TryChangeDamage(target, damage, origin: charger);

            // Vehicles are heavy — always stop the charge
            _movement.ResetToIdle(charger);
            return;
        }

        // --- Pass-through structures (e.g. handrails) ---
        if (TryComp(target, out XenoCrusherChargableComponent? chargable) && chargable.PassOnDestroy)
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = stage * xeno.StructureDamageMultiplier;
            _damageable.TryChangeDamage(target, damage);
            state.Stage = Math.Max(0, state.Stage - 1);
            Dirty(charger, state);
            return;
        }

        // --- Barricades (must precede generic damageable) ---
        if (HasComp<BarricadeComponent>(target))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = stage * xeno.BarricadeCollisionDamage;
            _damageable.TryChangeDamage(target, damage);
            _movement.ResetToIdle(charger);
            return;
        }

        // --- Structures and other damageable objects ---
        if (HasComp<DamageableComponent>(target))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = stage * xeno.StructureDamageMultiplier;
            _damageable.TryChangeDamage(target, damage);

            if (!TerminatingOrDeleted(target) &&
                !EntityManager.IsQueuedForDeletion(target) &&
                _physicsQuery.TryGetComponent(target, out var tp) &&
                tp.Hard && tp.BodyType == BodyType.Static)
            {
                var chargeDir = state.CurrentHeading.ToVec();
                var wallNormal = GetWallNormal(charger, target);
                var dot = Vector2.Dot(chargeDir, wallNormal);

                if (dot < -HeadOnDotThreshold)
                    _movement.ResetToIdle(charger);
            }
            else
            {
                state.Stage = Math.Max(0, state.Stage - 1);
                Dirty(charger, state);
            }

            return;
        }


        // --- Raw walls (non-damageable static geometry) ---
        if (_physicsQuery.TryGetComponent(target, out var wallPhysics) &&
            wallPhysics.Hard && wallPhysics.BodyType == BodyType.Static)
        {
            var chargeDir = state.CurrentHeading.ToVec();
            var wallNormal = GetWallNormal(charger, target);
            var dot = Vector2.Dot(chargeDir, wallNormal);

            if (dot < -HeadOnDotThreshold)
                _movement.ResetToIdle(charger);
        }
    }

    private void HandleLungingCollision(EntityUid charger, XenoChargerComponent xeno, XenoChargerStateComponent state,
        EntityUid target)
    {
        var stage = state.Stage;
        var isCharged = stage > 6;

        if (_hive.FromSameHive(charger, target))
            return;

        if (TryComp(target, out MobStateComponent? mobState))
        {
            if (_mobState.IsDead(target, mobState) || !_xeno.CanAbilityAttackTarget(charger, target))
                return;

            float damageAmount;
            float knockbackPower;
            TimeSpan knockdown;

            if (isCharged)
            {
                damageAmount = xeno.ChargedDamageBase + stage * xeno.ChargedDamagePerStage;
                knockbackPower = xeno.ChargedKnockback;
                knockdown = TimeSpan.FromSeconds(xeno.ChargedKnockdownDuration);
            }
            else
            {
                damageAmount = xeno.StandaloneDamage;
                knockbackPower = xeno.StandaloneKnockback;
                knockdown = TimeSpan.FromSeconds(xeno.StandaloneKnockdownDuration);
            }

            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = damageAmount;
            _damageable.TryChangeDamage(target, damage, origin: charger);
            _stun.TryParalyze(target, knockdown, false);

            var origin = _transform.GetMapCoordinates(charger);
            _sizeStun.KnockBack(target, origin, knockbackPower, knockbackPower);

            if (_net.IsServer)
            {
                _popup.PopupEntity(
                    Loc.GetString("rmc-xeno-lunge-hit-others", ("user", charger), ("target", target)),
                    target,
                    PopupType.MediumCaution
                );
            }

            return;
        }

        //Vehicles
        if (HasComp<VehicleComponent>(target))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = stage * xeno.StructureDamageMultiplier;
            _damageable.TryChangeDamage(target, damage, origin: charger);

            if (isCharged && _vehicle.TryGetOccupants(target, out var passengers, out var xenos))
            {
                foreach (var occupant in passengers.Concat(xenos))
                {
                    if (TerminatingOrDeleted(occupant) || _mobState.IsDead(occupant))
                        continue;

                    var throwDir = new Vector2(
                        _random.NextFloat(-1f, 1f),
                        _random.NextFloat(-1f, 1f)
                    );

                    if (throwDir.LengthSquared() > 0.001f)
                        throwDir = Vector2.Normalize(throwDir);

                    _stun.TryKnockdown(occupant, TimeSpan.FromSeconds(1), false);
                    _throwing.TryThrow(occupant, throwDir, 20f);
                }
            }
            _movement.ResetToIdle(charger);
            return;
        }

        // --- Pass-through structures (e.g. handrails) ---
        if (TryComp(target, out XenoCrusherChargableComponent? chargable) && chargable.PassOnDestroy)
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = stage * xeno.StructureDamageMultiplier;
            _damageable.TryChangeDamage(target, damage);
            state.Stage = Math.Max(0, state.Stage - 1);
            Dirty(charger, state);
            return;
        }

        if (HasComp<BarricadeComponent>(target))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = (stage + 1) * xeno.ChargedDamageBase;
            _damageable.TryChangeDamage(target, damage);

            if (_net.IsServer)
                _audio.PlayPvs(xeno.CadeHitSound, target);

            if (stage >= 7)
            {
                if (_net.IsServer)
                {
                    _transform.Unanchor(target);
                    _throwing.TryThrow(target, state.LungeDirection, 5f + stage * 1.5f, compensateFriction: true);
                }

                state.Stage = Math.Max(0, state.Stage - 1);
                Dirty(charger, state);
                return;
            }

            _movement.ResetToIdle(charger);
            return;
        }

        // Walls — same penetration logic as barricades.
        if (_physicsQuery.TryGetComponent(target, out var wallPhysics) &&
            wallPhysics.Hard && wallPhysics.BodyType == BodyType.Static)
        {

            if (TryComp(target, out XenoCrusherChargableComponent? crushchargable) && crushchargable.PassOnDestroy)
                return;

            if (!HasComp<DamageableComponent>(target))
            {
                _movement.ResetToIdle(charger);
                return;
            }

            var damage = new DamageSpecifier();
            damage.DamageDict[_blunt] = (stage + 1) * xeno.ChargedDamageBase;
            _damageable.TryChangeDamage(target, damage);

            if (stage >= 7)
            {
                if (_net.IsServer)
                    BreakWall(charger, target, state.LungeDirection);

                state.Stage = Math.Max(0, state.Stage - 1);
                Dirty(charger, state);

                if (_physicsQuery.TryGetComponent(charger, out var physics))
                {
                    var speed = xeno.LungeSpeed + state.Stage * xeno.LungeSpeedPerStage;
                    _physics.SetLinearVelocity(charger, state.LungeDirection * speed, body: physics);
                }

                return;
            }

            _movement.ResetToIdle(charger);
        }
    }

    private void BreakWall(EntityUid charger, EntityUid wall, Vector2 lungeDirection)
    {
        //WIP but not really that important.

        if (!_net.IsServer)
            return;

        if (TerminatingOrDeleted(charger))
            return;

        //if its not indestructible, delete.
        if (HasComp<DamageableComponent>(wall))
            Del(wall);

        //was gonna add some shrapnel throwing code here, but it had hands so im not doing it now.
    }

    private Vector2 GetWallNormal(EntityUid charger, EntityUid wall)
    {
        var chargerPos = _transform.GetMapCoordinates(charger).Position;
        var wallPos = _transform.GetMapCoordinates(wall).Position;
        var diff = chargerPos - wallPos;
        return diff.LengthSquared() > 0.001f ? Vector2.Normalize(diff) : Vector2.UnitX;
    }
}
