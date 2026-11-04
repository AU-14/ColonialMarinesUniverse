/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Special;

/// <summary>
/// Marker property. The reagent generator omits overdose thresholds from any reagent carrying it
/// (see <c>SharedReagentGeneratorSystem.CreateReagent</c>), so no overdose tick can ever fire.
/// </summary>
public sealed partial class Regulating : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => "This chemical regulates its own metabolism and [color=green]cannot overdose[/color].";
}
