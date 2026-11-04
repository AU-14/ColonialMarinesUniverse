/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Positive;

public sealed partial class Neutralizing : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";
    private static readonly ProtoId<DamageTypePrototype> CausticType = "Caustic";

    /// <summary>Reactive reagents and plasmas this property scrubs from the bloodstream.</summary>
    public static readonly ProtoId<ReagentPrototype>[] Neutralized =
    [
        "RMCPhoron", "RMCSulphuricAcid", "PolytrinicAcid", "RMCCLF3", "CMUXenoBlood",
    ];

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Removes [color=green]{PotencyPerSecond * 2}[/color] units of plasma and acids from the bloodstream, but deals [color=red]{PotencyPerSecond * 0.5f}[/color] toxin damage itself.\n" +
           $"Overdoses deal [color=red]{PotencyPerSecond * 2}[/color] toxin damage.\n" +
           $"Critical overdoses deal [color=red]{PotencyPerSecond * 2}[/color] caustic damage.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        foreach (var reagent in Neutralized)
            system.RMCBloodstream.RemoveBloodstreamChemical(args.TargetEntity, reagent, potency * 2f);
        var damage = new DamageSpecifier();
        damage.DamageDict[PoisonType] = potency * 0.5f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[PoisonType] = potency * 2f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[CausticType] = potency * 2f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }
}
