/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Special;

/// <summary>
/// Boosts sibling properties only while the body is dead or severely hypovolemic. Keeps the reagent
/// metabolizing in corpses so that corpse-active co-properties (Neurocryogenic, Defibrillating) run.
/// </summary>
public sealed partial class Thanatometabolizing : RMCChemicalEffect
{
    public const float BloodFractionThreshold = 0.6f;

    protected override bool ProcessOnDead => true;

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"While the body is dead or below [color=yellow]{BloodFractionThreshold * 100}%[/color] blood volume, boosts every other property in this chemical by [color=yellow]{Potency * 0.5f}[/color]. Otherwise inert.";

    protected override void ReagentBoost(RMCChemicalEffectSystem system, RMCReagentEffectArgs args, ref float boost)
    {
        var target = args.TargetEntity;
        if (system.MobState.IsDead(target))
        {
            boost += Potency * 0.5f;
            return;
        }
        if (system.RMCBloodstream.TryGetBloodReadout(target, out var current, out var normal) &&
            normal > FixedPoint2.Zero && (current / normal).Float() < BloodFractionThreshold)
        {
            boost += Potency * 0.5f;
        }
    }

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args) { }
}
