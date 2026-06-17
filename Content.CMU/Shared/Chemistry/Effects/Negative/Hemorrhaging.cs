/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.

using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using HumanOrganSlot = Content.Shared._CMU14.Medical.Human.Data.OrganSlot;

namespace Content.Shared._CMU14.Chemistry.Effects.Negative;

public sealed partial class Hemorrhaging : RMCChemicalEffect
{
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
        var humanMedical = entman.System<SharedHumanMedicalSystem>();
        var targ = args.TargetEntity;
        if (!entman.TryGetComponent<HumanMedicalComponent>(targ, out var medical))
            return;

        var regions = BuildDamageableRegions(entman, bodSys, targ);
        if (regions.Count == 0)
            return;

        var random = IoCManager.Resolve<IRobustRandom>();
        var region = random.Pick(regions);
        if (random.Prob(((float) potency * 5f) / 100f))
        {
            var transaction = new MedicalTransaction(region);
            transaction.Add(MedicalEffect.AddBleedSource(
                region,
                BleedKind.Internal,
                FixedPoint2.New(0.3f)));
            humanMedical.ApplyTransaction((targ, medical), transaction);
        }
    }

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var entman = args.EntityManager;
        var humanMedical = entman.System<SharedHumanMedicalSystem>();
        var targ = args.TargetEntity;
        if (!entman.TryGetComponent<HumanMedicalComponent>(targ, out var medical))
            return;

        var organs = BuildDamageableOrgans(medical);
        if (organs.Count == 0)
            return;

        var random = IoCManager.Resolve<IRobustRandom>();
        var organ = random.Pick(organs);
        var transaction = new MedicalTransaction(BodyRegionForOrgan(organ));
        transaction.Add(MedicalEffect.AddOrganDamage(organ, potency * FixedPoint2.New(0.5f)));
        humanMedical.ApplyTransaction((targ, medical), transaction);
    }

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var entman = args.EntityManager;
        var bodSys = entman.System<SharedBodySystem>();
        var humanMedical = entman.System<SharedHumanMedicalSystem>();
        var targ = args.TargetEntity;
        if (!entman.TryGetComponent<HumanMedicalComponent>(targ, out var medical))
            return;

        var random = IoCManager.Resolve<IRobustRandom>();
        if (!random.Prob((10f * (float) potency) / 100f))
            return;

        var transaction = new MedicalTransaction(BodyRegion.Chest);
        foreach (var region in BuildDamageableRegions(entman, bodSys, targ))
        {
            transaction.Add(MedicalEffect.AddBleedSource(
                region,
                BleedKind.Internal,
                FixedPoint2.New(0.3f)));
        }

        humanMedical.ApplyTransaction((targ, medical), transaction);
    }

    private static List<BodyRegion> BuildDamageableRegions(
        IEntityManager entman,
        SharedBodySystem body,
        EntityUid target)
    {
        var regions = new List<BodyRegion>();
        foreach (var (partId, part) in body.GetBodyChildren(target))
        {
            var region = ResolveRegion(entman, partId, part.PartType, part.Symmetry);
            if (region != BodyRegion.None)
                regions.Add(region);
        }

        return regions;
    }

    private static List<HumanOrganSlot> BuildDamageableOrgans(HumanMedicalComponent medical)
    {
        var organs = new List<HumanOrganSlot>();
        foreach (var organ in medical.Organs)
        {
            if (organ.Slot != HumanOrganSlot.None && !organ.Missing)
                organs.Add(organ.Slot);
        }

        return organs;
    }

    private static BodyRegion ResolveRegion(
        IEntityManager entman,
        EntityUid part,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        if (entman.TryGetComponent<AnatomyRegionComponent>(part, out var anatomy) &&
            anatomy.Region != BodyRegion.None)
        {
            return anatomy.Region;
        }

        return type switch
        {
            BodyPartType.Head => BodyRegion.Head,
            BodyPartType.Torso => BodyRegion.Chest,
            BodyPartType.Arm => symmetry == BodyPartSymmetry.Right ? BodyRegion.RightArm : BodyRegion.LeftArm,
            BodyPartType.Hand => symmetry == BodyPartSymmetry.Right ? BodyRegion.RightHand : BodyRegion.LeftHand,
            BodyPartType.Leg => symmetry == BodyPartSymmetry.Right ? BodyRegion.RightLeg : BodyRegion.LeftLeg,
            BodyPartType.Foot => symmetry == BodyPartSymmetry.Right ? BodyRegion.RightFoot : BodyRegion.LeftFoot,
            _ => BodyRegion.None,
        };
    }

    private static BodyRegion BodyRegionForOrgan(HumanOrganSlot organ)
    {
        return organ switch
        {
            HumanOrganSlot.Brain or HumanOrganSlot.Eyes or HumanOrganSlot.Ears => BodyRegion.Head,
            HumanOrganSlot.Kidneys => BodyRegion.Groin,
            _ => BodyRegion.Chest,
        };
    }
}
