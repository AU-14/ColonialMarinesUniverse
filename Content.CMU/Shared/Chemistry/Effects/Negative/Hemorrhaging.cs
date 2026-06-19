/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.

using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;


namespace Content.Shared._CMU14.Chemistry.Effects.Negative;

public sealed partial class Hemorrhaging : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> BluntType = "Blunt";
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Has a [color=red]{PotencyPerSecond * 5}%[/color] chance to cause internal bleeding in a random limb.\n" +
               $"Overdoses cause [color=red]{PotencyPerSecond * 0.5}[/color] damage to happen to a random organ.\n" +
               $"Critical overdoses have a [color=red]{PotencyPerSecond * 10}%[/color] chance to cause internal bleeding in all limbs.";
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
            woundSys.SeedInternalBleed(part, "Chemical", 0.3f);
        }
        //TODO: coughing up blood
    }

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var entman = args.EntityManager;
        var bodSys = entman.System<SharedBodySystem>();
        var orgSys = entman.System<SharedOrganHealthSystem>();
        var targ = args.TargetEntity;
        List<EntityUid> orgs = [];
        foreach (var item in bodSys.GetBodyOrgans(targ))
        {
            orgs.Add(item.Id);
        }
        var random = IoCManager.Resolve<IRobustRandom>();
        var org = random.Pick(orgs);
        IReadOnlyList<EntityUid> organtodamage = [org];
        var damage = new DamageSpecifier();
        damage.DamageDict[BluntType] = potency * 0.5;
        //forced to do it this way, can't raise local event.
        orgSys.DistributeOrganDamage(targ, damage, organtodamage);
    }

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var entman = args.EntityManager;
        var bodSys = entman.System<SharedBodySystem>();
        var orgSys = entman.System<SharedOrganHealthSystem>();
        var woundSys = entman.System<SharedCMUWoundsSystem>();
        var targ = args.TargetEntity;
        var random = IoCManager.Resolve<IRobustRandom>();
        if (random.Prob((10f * (float)potency) / 100f))
        {
            List<EntityUid> bparts = [];
            foreach (var item in bodSys.GetBodyChildren(targ))
            {
                woundSys.SeedInternalBleed(item.Id, "Chemical", 0.3f);
            }
        }

    }
}
