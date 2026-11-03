using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Projectiles.Reflect;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CCVar;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Magnetic Resonance, on both sides (playtest 4). Same reflection as RMC's <c>RMCReflectSystem</c> (its
/// public <see cref="RMCReflectSystem.TryReflectProjectile"/>, its reflected-bullet marker and damage
/// falloff), minus the IFF branch: a reflected IFF bullet has its IFF cleared so it can strike its own
/// side, the shooter included, instead of being zeroed.
/// </summary>
public sealed partial class EWJoeResonanceSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private GunIFFSystem _gunIFF = default!;
    [Dependency] private RMCProjectileSystem _rmcProjectile = default!;
    [Dependency] private RMCReflectSystem _reflect = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeResonanceComponent, PreventCollideEvent>(OnPreventCollide, before: [typeof(RMCProjectileSystem)]);
        SubscribeLocalEvent<EWJoeResonanceComponent, ProjectileReflectAttemptEvent>(OnReflectAttempt);
    }

    private void OnPreventCollide(Entity<EWJoeResonanceComponent> ent, ref PreventCollideEvent args)
    {
        if (!HasComp<ProjectileComponent>(args.OtherEntity) || _mobState.IsDead(ent) || !_cfg.GetCVar(CCVars.CMUEWJoeEnabled))
            return;

        _rmcProjectile.SetProjectileAccuracy(args.OtherEntity, ent.Comp.Accuracy);
    }

    private void OnReflectAttempt(Entity<EWJoeResonanceComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        // ABIL-12: a dead shell turns nothing back
        if (args.Cancelled || _mobState.IsDead(ent) || !_cfg.GetCVar(CCVars.CMUEWJoeEnabled))
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

        if (!_reflect.TryReflectProjectile(projectile, ent, ent.Comp.Angle, ent.Comp.Chance))
            return;

        // an IFF bullet would pass harmlessly back through the side that fired it
        if (HasComp<ProjectileIFFComponent>(projectile))
            _gunIFF.GiveAmmoIFF(projectile, null, false);

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
}
