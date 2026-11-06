/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared._RMC14.Chemistry.Effects;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Reaction;

/// <summary>
/// Guidebook-only marker. The generator gives an Explosive chemical an <c>{ID}Ignition</c> reaction that
/// detonates it once heated (see <see cref="SharedReagentGeneratorSystem.ComputeExplosion"/>).
/// </summary>
public sealed partial class Explosive : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var level = (int) Math.Round(Potency);
        SharedReagentGeneratorSystem.ComputeExplosion(level, out var slope, out var max, out _, out _);
        return $"Detonates when its solution is heated to [color=yellow]{SharedReagentGeneratorSystem.IgnitionTemperature(level)}[/color] K or more " +
               $"(lower if another combustion property is stronger).\n" +
               $"The blast reaches about [color=red]{max / slope:0.#}[/color] tiles with a peak intensity of [color=red]{max:0.#}[/color]; " +
               $"[color=yellow]{SharedReagentGeneratorSystem.ExplosionUnitsToCap}[/color] units give the full blast. " +
               $"Radius and peak grow with level up to a strong IED at level {SharedReagentGeneratorSystem.ExplosiveLevelCap}.";
    }
}
