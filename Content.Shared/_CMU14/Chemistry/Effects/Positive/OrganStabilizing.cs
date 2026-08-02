/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public sealed partial class Organstabilizing : RMCChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, Content.Shared.FixedPoint.FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        // OrganStasisComponent is intentionally not used here: it denotes a
        // detached organ in the CMU anatomy model. Organ stabilization is a
        // transient treatment flag handled by the organ system's damage gate.
        var index = args.EntityManager.System<CMUMedicalBodyIndexSystem>();
        var expiresAt = IoCManager.Resolve<IGameTiming>().CurTime + TimeSpan.FromSeconds(2);
        foreach (var organ in index.GetOrgans(args.TargetEntity))
        {
            var stabilized = args.EntityManager.EnsureComponent<CMUOrganStabilizedComponent>(organ.Owner);
            stabilized.ExpiresAt = expiresAt;
            args.EntityManager.Dirty(organ.Owner, stabilized);
        }
    }

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Stabilizes internal organ damage symptoms.";
    }
}
