using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Projectiles.Reflect;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Magnetic Resonance, on both sides (playtest 4; rebalance RB-06). Same reflection as RMC's
/// <c>RMCReflectSystem</c> (its public <see cref="RMCReflectSystem.TryReflectProjectile"/>, its reflected-bullet
/// marker and damage falloff), minus the IFF branch: RMC zeroes a reflected IFF bullet, the Joe turns it back
/// for real. RB-06: only bullets arriving inside the frontal arc can be turned back, and a reflected bullet
/// keeps its IFF, so it cannot strike the side that fired it.
/// </summary>
public sealed partial class EWJoeResonanceSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private RMCProjectileSystem _rmcProjectile = default!;
    [Dependency] private RMCReflectSystem _reflect = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeResonanceComponent, PreventCollideEvent>(OnPreventCollide, before: [typeof(RMCProjectileSystem)]);
        SubscribeLocalEvent<EWJoeResonanceComponent, ProjectileReflectAttemptEvent>(OnReflectAttempt);
    }

    private void OnPreventCollide(Entity<EWJoeResonanceComponent> ent, ref PreventCollideEvent args)
    {
        if (!HasComp<ProjectileComponent>(args.OtherEntity) || _mobState.IsDead(ent))
            return;

        _rmcProjectile.SetProjectileAccuracy(args.OtherEntity, ent.Comp.Accuracy);
    }

    private void OnReflectAttempt(Entity<EWJoeResonanceComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        // ABIL-12: a dead shell turns nothing back
        if (args.Cancelled || _mobState.IsDead(ent))
            return;

        var projectile = args.ProjUid;
        var reflectorId = GetNetEntity(ent).Id;

        // a bullet this Joe already turned back: let it pass while it is still leaving, as RMC does
        if (TryComp(projectile, out RMCReflectedProjectileComponent? reflected) && reflected.ReflectedBy.Contains(reflectorId))
        {
            if (reflectorId == reflected.LastReflectedBy)
                args.Cancelled = true;

            return;
        }

        // RB-06: flanking fire always lands; checked before the roll so it costs no chance
        if (!IsFrontal(ent, projectile))
            return;

        if (!_reflect.TryReflectProjectile(projectile, ent, ent.Comp.Angle, ent.Comp.Chance))
            return;

        // RB-06: the bullet keeps its IFF (no friendly fire on the side that fired it)
        reflected = EnsureComp<RMCReflectedProjectileComponent>(projectile);
        reflected.Accuracy = ent.Comp.Accuracy;
        reflected.ReflectionMultiplier = ent.Comp.ReflectionMultiplier;
        reflected.ReflectedBy.Add(reflectorId);
        reflected.LastReflectedBy = reflectorId;
        Dirty(projectile, reflected);

        // the reflected bullet must be able to hit its original shooter at once
        args.Component.WhenToStopIgnoringShooter = _timing.CurTime;
        Dirty(projectile, args.Component);

        _rmcProjectile.SetMaxRange(projectile, ent.Comp.Range);
        args.Cancelled = true;
    }

    /// <summary>
    /// RB-06: true when <paramref name="projectile"/> arrives from inside the Joe's frontal arc, judged by the
    /// direction it came from (the reverse of its velocity) against the Joe's facing.
    /// </summary>
    public bool IsFrontal(Entity<EWJoeResonanceComponent> joe, EntityUid projectile)
    {
        var cameFrom = -_physics.GetMapLinearVelocity(projectile);
        // a bullet at rest has no course: judge by where it sits
        if (cameFrom.LengthSquared() < 0.0001f)
            cameFrom = _transform.GetMapCoordinates(projectile).Position - _transform.GetMapCoordinates(joe).Position;

        var facing = _transform.GetWorldRotation(joe);
        return EWJoeTargetRules.InCone(cameFrom, facing, Angle.FromDegrees(joe.Comp.FrontalArc.Degrees / 2));
    }
}
