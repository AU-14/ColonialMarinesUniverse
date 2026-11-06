/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared._RMC14.Chemistry.Effects;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Reaction;

public sealed partial class Fueling : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var level = (int) Math.Round(Potency);
        return $"Burns as a flamer fuel: longer burn time, slightly lower heat.\n" +
               $"Ignites when its solution is heated to [color=yellow]{SharedReagentGeneratorSystem.IgnitionTemperature(level)}[/color] K or more " +
               $"(lower if another combustion property is stronger), setting the floor around the container on fire. " +
               $"The fire covers one tile unless a range property widens it.";
    }
}
