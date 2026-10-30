/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Neutral;

/// <summary>
/// Passive above 170 K; below it, boosts every sibling property like <c>Boosting</c> does.
/// </summary>
public sealed partial class Cryometabolizing : RMCChemicalEffect
{
    public const float ActivationKelvin = 170f;

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"No effect above [color=yellow]{ActivationKelvin}[/color] K. Below that body temperature, boosts every other property in this chemical by [color=yellow]{Potency * 0.5f}[/color].";

    protected override void ReagentBoost(RMCChemicalEffectSystem system, RMCReagentEffectArgs args, ref float boost)
    {
        if (system.Temperature.GetTemperature(args.TargetEntity) < ActivationKelvin)
            boost += Potency * 0.5f;
    }
}
