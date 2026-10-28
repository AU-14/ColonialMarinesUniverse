/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Neutral;

/// <summary>
/// Accelerated cell division in hair follicles. Modelled as a nutrition drain; the hydroponics yield bonus
/// is delivered through <see cref="RMCChemicalEffectSystem.RaiseHydroTick{T}"/>.
/// </summary>
public sealed partial class Trichogenic : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Accelerates hair growth, consuming [color=yellow]{PotencyPerSecond}[/color] nutrition per tick. Increases plant yield.\n" +
           $"Overdoses consume [color=red]{PotencyPerSecond * 4}[/color] nutrition.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        if (system.TryGetSatiation(args.TargetEntity, out var satiation))
            system.Satiation.ModifyValue((args.TargetEntity, satiation), SatiationSystem.Hunger, -(float)potency);
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        if (system.TryGetSatiation(args.TargetEntity, out var satiation))
            system.Satiation.ModifyValue((args.TargetEntity, satiation), SatiationSystem.Hunger, (float)potency * -4f);
    }

    protected override void TickHydroTray(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.RaiseHydroTick<Trichogenic>(args.TargetEntity, potency, args.Context.Quantity);
}
