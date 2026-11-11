/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Special;

/// <summary>Admin-only: full-body revitalisation.</summary>
public sealed partial class Omnipotent : RMCChemicalEffect
{
    private static readonly ProtoId<DamageGroupPrototype>[] Groups = ["Brute", "Burn", "Toxin", "Airloss", "Genetic"];

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Heals [color=green]{PotencyPerSecond * 5}[/color] of every damage group, [color=green]{PotencyPerSecond * 5}[/color] health on every organ, and restores [color=green]{PotencyPerSecond * 10}[/color] blood.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var target = args.TargetEntity;
        var rmc = system.RMCDamageable;
        Content.Shared.Damage.DamageSpecifier? healing = null;
        foreach (var group in Groups)
            healing = rmc.DistributeHealingCached(target, group, potency * 5f, healing);
        if (healing is not null)
            damageable.TryChangeDamage(target, healing, true, interruptsDoAfters: false);
        foreach (var organ in system.MedicalBodyIndex.GetOrgans(target))
            system.OrganHealth.HealOrgan(organ.Owner, target, potency * 5f);
        if (system.TryGetBloodstream(target, out var blood))
            system.Bloodstream.TryModifyBloodLevel((target, blood), potency * 10f);
    }
}
