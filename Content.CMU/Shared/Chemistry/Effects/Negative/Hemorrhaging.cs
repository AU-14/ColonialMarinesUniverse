/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.

using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;


namespace Content.Shared._CMU14.Chemistry.Effects.Negative;

public sealed partial class Hemorrhaging : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"DESCRIPTION IS NOT IMPLEMENTED";
    }

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var entman = args.EntityManager;
        var bodSys = entman.System<SharedBodySystem>();
        var woundSys = entman.System<SharedCMUWoundsSystem>();
        var targ = args.TargetEntity;
        List<EntityUid> bparts = [];
        // evil foreach from hell
        foreach (var item in bodSys.GetBodyChildren(targ))
        {
            bparts.Add(item.Id);
        }
        var random = IoCManager.Resolve<IRobustRandom>();
        var part = random.Pick(bparts);
        //TODO if (entman.TryComp<LimbComponent>(part, out var limb) && (limb.Robot | limb.Synth)) return;
        if (random.Prob(((float)potency * 5f) / 100f))
        {

        }
        //TODO: coughing up blood
    }
}
