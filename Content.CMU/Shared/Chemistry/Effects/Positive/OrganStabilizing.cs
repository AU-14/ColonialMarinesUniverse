using Content.Shared.Damage.Systems;
/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Chemistry.Effects.Positive;

public sealed partial class Organstabilizing : RMCChemicalEffect
{
    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, Content.Shared.FixedPoint.FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        // OrganStasisComponent is intentionally not used here: it denotes a
        // detached organ in the CMU anatomy model. Organ stabilization is a
        // transient treatment flag handled by the organ system's damage gate.
        system.StabilizeYautjaOrgans(args.TargetEntity);
    }

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Stabilizes internal organ damage symptoms.";
    }
}
