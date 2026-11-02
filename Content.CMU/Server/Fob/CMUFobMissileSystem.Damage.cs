using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Shields;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Heatshield;
using Content.Shared._RMC14.Xenonids.Hedgehog;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;

namespace Content.Server.CMU14.Fob;

public sealed partial class CMUFobMissileSystem
{
    [Dependency] private MobThresholdSystem _thresholds = default!;

    private void InitializeDamage()
    {
        SubscribeLocalEvent<CMUFobMissileComponent, BeforeProjectileHitEvent>(OnBeforeHit);
        // Limit the warhead before active shields absorb it, so a shield remains useful against the large payload.
        SubscribeLocalEvent<CMUFobRocketDamageCapComponent, DamageModifyAfterResistEvent>(OnLimitDamage,
            before: [typeof(XenoShieldSystem), typeof(CrusherShieldSystem), typeof(VanguardShieldSystem),
                typeof(KingShieldSystem), typeof(XenoSpikeShieldSystem), typeof(XenoHeatshieldSystem)]);
    }

    private void OnBeforeHit(Entity<CMUFobMissileComponent> missile, ref BeforeProjectileHitEvent args)
    {
        if (!_reserve.Enabled || !TryComp<XenoComponent>(args.Target, out var xeno) || xeno.Tier != 3
            || !_thresholds.TryGetThresholdForState(args.Target, MobState.Critical, out var critical)
            || !_thresholds.TryGetThresholdForState(args.Target, MobState.Dead, out var dead))
            return;

        var cap = EnsureComp<CMUFobRocketDamageCapComponent>(args.Target);
        // The allowance is damage per rocket, not a health floor: an already wounded T3 can still die.
        cap.Remaining.TryAdd(missile, FixedPoint2.Min(critical.Value + missile.Comp.T3CriticalMargin, dead.Value - 1));
    }

    private void OnLimitDamage(Entity<CMUFobRocketDamageCapComponent> target, ref DamageModifyAfterResistEvent args)
    {
        if (!_reserve.Enabled)
            return;
        // Direct hits identify their projectile as Tool; explosions keep its UID as Origin, even after deletion.
        var source = args.Tool is { } tool && target.Comp.Remaining.ContainsKey(tool) ? tool : args.Origin;
        if (source is not { } rocket || !target.Comp.Remaining.TryGetValue(rocket, out var remaining))
            return;
        var total = args.Damage.GetTotal();
        if (total <= FixedPoint2.Zero)
            return;
        if (remaining <= FixedPoint2.Zero)
            args.Damage = new DamageSpecifier();
        else if (total > remaining)
            args.Damage *= (float) (remaining.Double() / total.Double());
        target.Comp.Remaining[rocket] = FixedPoint2.Max(FixedPoint2.Zero, remaining - args.Damage.GetTotal());
    }
}
