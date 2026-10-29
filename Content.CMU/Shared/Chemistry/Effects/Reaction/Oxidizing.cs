/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared._RMC14.Chemistry.Effects;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Reaction;

public sealed partial class Oxidizing : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var level = (int) Math.Round(Potency);
        return $"Makes chemical fires hotter but shorter; at level {SharedReagentGeneratorSystem.BlueFireOxidizingLevel}+ and above its Fueling level it burns blue.\n" +
               $"Ignites when its solution is heated to [color=yellow]{SharedReagentGeneratorSystem.IgnitionTemperature(level)}[/color] K or more " +
               $"(lower if another combustion property is stronger), setting the floor around the container on fire.";
    }
}
