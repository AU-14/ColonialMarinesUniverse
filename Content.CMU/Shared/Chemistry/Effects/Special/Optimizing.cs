/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Special;

/// <summary>
/// Marker property. The reagent generator adds one unit of product per level to the reagent's recipe yield
/// (see <c>SharedReagentGeneratorSystem.CreateRecipe</c>).
/// </summary>
public sealed partial class Optimizing : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Easier to synthesise: each reaction yields [color=green]{(int)LinearLevel}[/color] additional units.";
}
