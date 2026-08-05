using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;

namespace Content.Shared.Metabolism;

public sealed partial class MetabolizerSystem
{
    [Dependency] private RMCChemicalEffectSystem _rmcChemicalEffects = default!;

    private bool TryApplyRMCChemicalEffect(
        EntityUid target,
        Entity<MetabolizerComponent, OrganComponent?, SolutionManagerComponent?> organ,
        Solution source,
        FixedPoint2 quantity,
        ReagentPrototype reagent,
        EntityEffect effect,
        float scale)
    {
        if (effect is not RMCChemicalEffect rmcEffect)
            return false;

        _rmcChemicalEffects.ApplyMetabolismEffect(
            target,
            rmcEffect,
            scale,
            organ,
            source,
            quantity,
            reagent);
        return true;
    }
}
