/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Neutral;

/// <summary>
/// Follicle damage. The humanoid appearance layer here has no runtime hair-removal API, so this is modelled
/// as minor cellular damage that only matters at overdose.
/// </summary>
public sealed partial class Atrichogenic : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> CellularType = "Cellular";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Damages hair follicles. Harmless below the overdose threshold.\n" +
           $"Overdoses deal [color=red]{PotencyPerSecond * 0.5f}[/color] cellular damage.\n" +
           $"Critical overdoses deal [color=red]{PotencyPerSecond * 2}[/color] cellular damage.";

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[CellularType] = potency * 0.5f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[CellularType] = potency * 2f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }
}
