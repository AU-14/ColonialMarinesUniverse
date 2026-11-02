/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Special;

public sealed partial class Firepenetrating : RMCChemicalEffect
{
    /// <summary>
    /// Marker property: the generator emits <c>firePenetrating: true</c> on the reagent and the flamer chain
    /// marks its tile fires so that <c>SharedRMCFlammableSystem.DamageFromFire</c> ignores every resistance.
    /// CMU14: from level 3 (<c>firePenetratingLevel</c>) the tile fires also carry
    /// <c>CMUStructureMeltingFireComponent</c> and melt structures on their tile.
    /// </summary>
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return "Fires fed by this chemical burn through heat-resistant materials: synthetic and Yautja fire " +
               "resistance is ignored. A flamer tank needs at least one tenth of this chemical for its blend to penetrate. " +
               // CMU14: FLOW-23 structure melting.
               "At level 3 and above, its fires also melt walls, barricades, doors and other structures they burn on over time, " +
               "faster with each level above 2. Indestructible hull walls are unaffected.";
    }
}
