using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaPlasmaProjectileSystem : EntitySystem
{
    private const string CasterKnockdownStatus = "KnockedDown";
    private const string CasterStunStatus = "Stun";
    private const string YautjaInterferenceStatus = "YautjaInterference";

    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private StatusEffectQuerySystem _status = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaCasterImmobilizerProjectileComponent, ProjectileFixedDistanceStopEvent>(OnCasterImmobilizerStop);
        SubscribeLocalEvent<YautjaCasterImmobilizerProjectileComponent, ProjectileHitEvent>(OnCasterImmobilizerHit);
        SubscribeLocalEvent<YautjaCasterStunProjectileComponent, ProjectileHitEvent>(OnCasterStunHit);
        SubscribeLocalEvent<YautjaCasterSingleLethalProjectileComponent, BeforeTriggerEvent>(OnCasterSingleLethalBeforeTrigger);
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

    private static float GetDamageStackBonus(DamageSpecifier damage, YautjaIncendiaryPlasmaProjectileComponent component)
    {
        if (component.XenoDamageStackDivisor <= 0)
            return 0f;

        var total = MathF.Max(damage.GetTotal().Float(), 0f);
        return MathF.Floor(total / component.XenoDamageStackDivisor);
    }
}
