using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Heal;

public abstract partial class SharedXenoHealSystem
{
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedCMUWoundsSystem _wounds = default!;

    private static readonly ProtoId<DamageTypePrototype> SalvePoison = "Poison";

    private bool TryPaySalveCost(EntityUid healer, XenoApplySalveActionEvent args, FixedPoint2 amount,
        FixedPoint2 damageModifier, out FixedPoint2 damageTaken)
    {
        damageTaken = amount * damageModifier;
        if (!_xenoPlasma.TryRemovePlasmaPopup(healer, amount * args.PlasmaCostModifier))
            return false;

        if (TryComp<DamageableComponent>(healer, out var damage))
            _damageable.AddDamage(healer, damage, new DamageSpecifier { DamageDict = { [BluntGroup] = damageTaken } });
        return true;
    }

    // Wound ledgers are server-owned. A recognized human target consumes this branch even on a rejected cast.
    private bool TryApplyHumanSalve(EntityUid healer, XenoApplySalveActionEvent args)
    {
        var target = args.Target;
        if (!HasComp<CMUHumanMedicalComponent>(target) || HasComp<SynthComponent>(target))
            return false;

        if (_net.IsClient)
            return true;

        LocId? failure = null;
        if (_mobState.IsDead(target))
            failure = "rmc-xeno-apply-salve-target-dead-failure";
        else if (_flammable.IsOnFire(target))
            failure = "rmc-xeno-apply-salve-target-on-fire-failure";
        else if (!_interact.InRangeUnobstructed(healer, target, args.Range))
            failure = "rmc-xeno-apply-salve-target-too-far-away-failure";
        else if (_hive.GetHive(healer) is not { } hive || !_hive.IsAllyOfHive(target, hive.Owner))
            failure = "rmc-xeno-apply-salve-target-hostile-failure";

        if (failure != null)
        {
            _popup.PopupEntity(Loc.GetString(failure.Value, ("target_xeno", target)), healer, healer);
            return true;
        }

        if (!TryComp<DamageableComponent>(target, out var damage))
            return true;

        var treatment = _rmcDamageable.DistributeHealingCached((target, damage), BruteGroup, args.HumanBruteHeal);
        _rmcDamageable.DistributeHealingCached((target, damage), BurnGroup, args.HumanBurnHeal, treatment);
        if (treatment.Empty && !HasSurfaceBleeding(target))
        {
            _popup.PopupEntity(Loc.GetString("cmu-xeno-healer-human-no-wounds"), healer, healer);
            return true;
        }

        if (!TryPaySalveCost(healer, args, args.StandardHealAmount, args.DamageTakenModifier, out _))
            return true;

        args.Handled = true;
        treatment.DamageDict[SalvePoison] = args.HumanPoison;
        _damageable.TryChangeDamage(target, treatment, ignoreResistances: true, interruptsDoAfters: false, origin: healer);
        foreach (var (part, _) in _medicalIndex.GetBodyParts(target))
            _wounds.StopSurfaceBleedingOnPart(part);

        SpawnAttachedTo(args.HealEffect, target.ToCoordinates());
        _jitter.DoJitter(target, TimeSpan.FromSeconds(1), true, 80, 8, true);
        _audio.PlayPvs(args.HealSound, target);
        _popup.PopupEntity(Loc.GetString("cmu-xeno-healer-human-heal-self", ("target", target)), healer, healer, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("cmu-xeno-healer-human-heal-target", ("healer", healer)), target, target, PopupType.SmallCaution);
        return true;
    }

    private bool HasSurfaceBleeding(EntityUid target)
    {
        foreach (var (part, _) in _medicalIndex.GetBodyParts(target))
        {
            if (_wounds.HasActiveSurfaceBleeding(part))
                return true;
        }

        return false;
    }
}
