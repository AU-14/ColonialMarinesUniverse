using System.Numerics;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Atmos.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Vehicle.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaPlasmaProjectileSystem : EntitySystem
{
    private const string CasterKnockdownStatus = "KnockedDown";
    private const string CasterStunStatus = "Stun";
    private const string YautjaInterferenceStatus = "YautjaInterference";

    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private ExplosionSystem _explosions = default!;
    [Dependency] private StatusEffectQuerySystem _status = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TriggerSystem _trigger = default!;
    [Dependency] private VehicleTopologySystem _vehicleTopology = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaCasterImmobilizerProjectileComponent, ProjectileFixedDistanceStopEvent>(OnCasterImmobilizerStop);
        SubscribeLocalEvent<YautjaCasterImmobilizerProjectileComponent, ProjectileHitEvent>(OnCasterImmobilizerHit);
        SubscribeLocalEvent<YautjaCasterStunProjectileComponent, ProjectileHitEvent>(OnCasterStunHit);
        SubscribeLocalEvent<YautjaCasterSingleLethalProjectileComponent, BeforeTriggerEvent>(OnCasterSingleLethalBeforeTrigger);
        SubscribeLocalEvent<YautjaCasterEradicatorProjectileComponent, ProjectileFixedDistanceStopEvent>(OnCasterEradicatorStop);
        SubscribeLocalEvent<YautjaCasterEradicatorProjectileComponent, ProjectileHitEvent>(OnCasterEradicatorHit);
        SubscribeLocalEvent<YautjaIncendiaryPlasmaProjectileComponent, ProjectileHitEvent>(OnIncendiaryPlasmaHit);
        SubscribeLocalEvent<YautjaPlasmaRifleBoltComponent, ProjectileHitEvent>(OnPlasmaRifleBoltHit);
    }

    private void OnCasterStunHit(Entity<YautjaCasterStunProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (!CanCasterStun(args.Target))
            return;

        var duration = ent.Comp.StunTime;
        if (IsHuman(args.Target))
            duration += ent.Comp.HumanBonusStunTime;

        ApplyCasterStun(args.Target, duration);
    }

    private void OnCasterImmobilizerHit(Entity<YautjaCasterImmobilizerProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        DoCasterImmobilizerAreaStun(ent);
    }

    private void OnCasterImmobilizerStop(Entity<YautjaCasterImmobilizerProjectileComponent> ent, ref ProjectileFixedDistanceStopEvent args)
    {
        DoCasterImmobilizerAreaStun(ent);
    }

    private void OnCasterSingleLethalBeforeTrigger(Entity<YautjaCasterSingleLethalProjectileComponent> ent, ref BeforeTriggerEvent args)
    {
        if (args.User is { } target && HasComp<MobStateComponent>(target))
            return;

        args.Cancelled = true;
    }

    private void OnCasterEradicatorStop(Entity<YautjaCasterEradicatorProjectileComponent> ent, ref ProjectileFixedDistanceStopEvent args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _trigger.Trigger(ent.Owner);
    }

    private void OnCasterEradicatorHit(Entity<YautjaCasterEradicatorProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (!_vehicleTopology.TryGetVehicle(args.Target, out var vehicle))
            return;

        ApplyCasterEradicatorVehicleImpact((vehicle, ent.Comp));
    }

    private void ApplyCasterEradicatorVehicleImpact(Entity<YautjaCasterEradicatorProjectileComponent> ent)
    {
        if (TryComp(ent.Owner, out GridVehicleMoverComponent? mover))
        {
            var speed = mover.CurrentSpeed;
            var until = _timing.CurTime + ent.Comp.VehicleSlowdownTime;
            mover.ImmobileUntil = TimeSpan.FromTicks(Math.Max(mover.ImmobileUntil.Ticks, until.Ticks));
            mover.CurrentSpeed = 0f;
            mover.IsCommittedToMove = false;
            mover.IsPushMove = false;
            mover.PushDirection = Vector2i.Zero;
            Dirty(ent.Owner, mover);

            if (MathF.Abs(speed) > 1f)
                ApplyCasterEradicatorInteriorCrash(ent.Owner, speed, mover.MaxSpeed, ent.Comp);
        }

        _audio.PlayPvs(ent.Comp.VehicleImpactSound, ent.Owner);
        _hardpoints.DamageVehicleHull(ent.Owner, ent.Comp.VehicleHullDamage);

        if (!TryComp(ent.Owner, out VehicleInteriorComponent? interior) ||
            interior.EntryParent == EntityUid.Invalid ||
            !Exists(interior.EntryParent))
        {
            return;
        }

        var interiorCoordinates = _transform.ToMapCoordinates(interior.Entry);
        _explosions.QueueExplosion(
            interiorCoordinates,
            "RMC",
            ent.Comp.InteriorExplosionIntensity,
            ent.Comp.InteriorExplosionSlope,
            ent.Comp.InteriorExplosionMaxTileIntensity,
            ent.Owner,
            addLog: false);
    }

    private void ApplyCasterEradicatorInteriorCrash(
        EntityUid vehicle,
        float currentSpeed,
        float maxSpeed,
        YautjaCasterEradicatorProjectileComponent component)
    {
        if (!TryComp(vehicle, out VehicleInteriorComponent? interior))
            return;

        var flingDistance = Math.Max(1, (int) MathF.Ceiling(MathF.Abs(currentSpeed) / MathF.Max(maxSpeed, 1f))) * 2;
        var direction = new Vector2(MathF.Sign(currentSpeed), 0f);
        var occupants = new HashSet<EntityUid>(interior.Passengers);
        occupants.UnionWith(interior.Xenos);

        foreach (var occupant in occupants)
        {
            if (TerminatingOrDeleted(occupant) ||
                TryComp(occupant, out BuckleComponent? buckle) && buckle.Buckled)
            {
                continue;
            }

            ApplyCasterStun(occupant, component.InteriorCrashStun);
            ApplyCasterKnockdown(occupant, component.InteriorCrashKnockdown);
            _throwing.TryThrow(occupant, direction, flingDistance, animated: false, playSound: false);
        }
    }

    private void OnIncendiaryPlasmaHit(Entity<YautjaIncendiaryPlasmaProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (!HasComp<MobStateComponent>(args.Target) ||
            !TryComp(args.Target, out FlammableComponent? flammable))
        {
            return;
        }

        var stacks = ent.Comp.FireStacks;
        if (HasComp<XenoComponent>(args.Target) &&
            TryComp(ent, out ProjectileComponent? projectile))
        {
            stacks = ent.Comp.FireStacks * ent.Comp.XenoFireStackMultiplier + GetDamageStackBonus(projectile.Damage, ent.Comp);
        }

        _flammable.AdjustFireStacks(args.Target, stacks, flammable, true);
    }

    private void OnPlasmaRifleBoltHit(Entity<YautjaPlasmaRifleBoltComponent> ent, ref ProjectileHitEvent args)
    {
        if (!HasComp<XenoComponent>(args.Target) ||
            !TryComp(ent, out ProjectileComponent? projectile) ||
            !projectile.Damage.DamageDict.TryGetValue(ent.Comp.XenoExtraDamageType, out var baseDamage) ||
            baseDamage <= FixedPoint2.Zero)
        {
            return;
        }

        var extraDamage = new DamageSpecifier
        {
            DamageDict =
            {
                [ent.Comp.XenoExtraDamageType] = baseDamage * ent.Comp.XenoExtraDamageMultiplier,
            },
        };

        _damage.TryChangeDamage(args.Target, extraDamage, true, origin: args.Shooter, tool: ent);
        _status.TryAddStatusEffect(args.Target, YautjaInterferenceStatus, ent.Comp.XenoInterferenceDuration, true);
    }

    private void DoCasterImmobilizerAreaStun(Entity<YautjaCasterImmobilizerProjectileComponent> ent)
    {
        var coordinates = _transform.GetMapCoordinates(ent);
        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(coordinates, ent.Comp.StunRange))
        {
            if (HasComp<YautjaAbominationComponent>(target))
                continue;

            var duration = ent.Comp.StunTime;
            if (HasComp<YautjaComponent>(target))
                duration -= ent.Comp.YautjaStunReduction;

            ApplyCasterStun(target, duration);
        }
    }

    private bool CanCasterStun(EntityUid target)
    {
        return HasComp<MobStateComponent>(target) &&
               !HasComp<YautjaComponent>(target) &&
               !HasComp<YautjaAbominationComponent>(target) &&
               (HasComp<HumanoidAppearanceComponent>(target) || HasComp<XenoComponent>(target));
    }

    private bool IsHuman(EntityUid target)
    {
        return TryComp(target, out HumanoidAppearanceComponent? humanoid) &&
               humanoid.Species == "Human";
    }

    private void ApplyCasterStun(EntityUid target, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            return;

        if (!TryComp(target, out StatusEffectsComponent? status))
            return;

        if (_stun.TryStun(target, duration, true, status))
            _status.TrySetTime(target, CasterStunStatus, duration, status);

        if (_stun.TryKnockdown(target, duration, true, status))
            _status.TrySetTime(target, CasterKnockdownStatus, duration, status);
    }

    private void ApplyCasterKnockdown(EntityUid target, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero || !TryComp(target, out StatusEffectsComponent? status))
            return;

        if (_stun.TryKnockdown(target, duration, true, status))
            _status.TrySetTime(target, CasterKnockdownStatus, duration, status);
    }

    private static float GetDamageStackBonus(DamageSpecifier damage, YautjaIncendiaryPlasmaProjectileComponent component)
    {
        if (component.XenoDamageStackDivisor <= 0)
            return 0f;

        var total = MathF.Max(damage.GetTotal().Float(), 0f);
        return MathF.Floor(total / component.XenoDamageStackDivisor);
    }
}
