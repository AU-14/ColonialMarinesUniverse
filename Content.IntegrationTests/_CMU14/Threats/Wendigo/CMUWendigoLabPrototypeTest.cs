using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Metabolism;
using Content.Shared._RMC14.Xenonids.Headbite;
using Content.Shared._RMC14.Xenonids.Punch;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Threats.Wendigo;

[TestFixture]
public sealed class CMUWendigoLabPrototypeTest : GameTest
{
    private const string Full = "AU14Wendigo";
    private const string Lab = "CMUWendigoLab";
    private const string Lesser = "CMUWendigoLesser";

    [SidedDependency(Side.Server)] private readonly IComponentFactory _compFactory = default!;

    [Test]
    [RunOnSide(Side.Server)]
    public void ReactionsHaveExpectedReactantsAndProducts()
    {
        AssertReaction("CMUStabilizedMutagen", "CMUUnstableMutagen", "CMUStabilizedMutagen", 15,
            ("CMDylovene", 10), ("RMCUranium", 5), ("RMCGold", 1), ("CMUUnstableMutagen", 5));
        AssertReaction("CMUStabilizedMutagenUpstream", "UnstableMutagen", "CMUStabilizedMutagen", 15,
            ("CMDylovene", 10), ("RMCUranium", 5), ("RMCGold", 1), ("UnstableMutagen", 5));
        AssertReaction("CMUMH33", "CMUMH32", "CMUMH33", 15,
            ("CMUMH32", 15), ("RMCGold", 15), ("RMCPlatinum", 10), ("CMUBlackSludge", 32));
    }

    private void AssertReaction(string id, string _, string product, int productAmount,
        params (string Reagent, int Amount)[] reactants)
    {
        var reaction = SProtoMan.Index<ReactionPrototype>(id);
        Assert.That(reaction.Quantized, Is.True, id);
        Assert.That(reaction.Reactants, Has.Count.EqualTo(reactants.Length), id);
        foreach (var (reagent, amount) in reactants)
        {
            Assert.That(reaction.Reactants.TryGetValue(reagent, out var info), Is.True, $"{id} missing {reagent}");
            Assert.That(info.Amount, Is.EqualTo(FixedPoint2.New(amount)), $"{id} {reagent}");
            Assert.That(info.Catalyst, Is.False, $"{id} {reagent}");
        }

        Assert.That(reaction.Products, Has.Count.EqualTo(1), id);
        Assert.That(reaction.Products[product], Is.EqualTo(FixedPoint2.New(productAmount)), id);
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void LesserWendigoIsWeakerAndLacksScreech()
    {
        var full = SProtoMan.Index<Robust.Shared.Prototypes.EntityPrototype>(Full);
        var lesser = SProtoMan.Index<Robust.Shared.Prototypes.EntityPrototype>(Lesser);

        Assert.That(full.TryGetComponent<MobThresholdsComponent>(out var fullThresholds, _compFactory));
        Assert.That(lesser.TryGetComponent<MobThresholdsComponent>(out var lesserThresholds, _compFactory));
        Assert.That(DeadThreshold(fullThresholds!), Is.EqualTo(FixedPoint2.New(1500)));
        Assert.That(DeadThreshold(lesserThresholds!), Is.EqualTo(FixedPoint2.New(1200)));

        Assert.That(full.TryGetComponent<XenoPunchComponent>(out var fullPunch, _compFactory));
        Assert.That(lesser.TryGetComponent<XenoPunchComponent>(out var lesserPunch, _compFactory));
        AssertScaled(fullPunch!.Damage, lesserPunch!.Damage, "punch");

        Assert.That(full.TryGetComponent<MeleeWeaponComponent>(out var fullMelee, _compFactory));
        Assert.That(lesser.TryGetComponent<MeleeWeaponComponent>(out var lesserMelee, _compFactory));
        AssertScaled(fullMelee!.Damage, lesserMelee!.Damage, "melee");

        Assert.That(full.TryGetComponent<XenoHeadbiteComponent>(out var fullBite, _compFactory));
        Assert.That(lesser.TryGetComponent<XenoHeadbiteComponent>(out var lesserBite, _compFactory));
        AssertScaled(fullBite!.Damage, lesserBite!.Damage, "headbite");

        Assert.That(full.TryGetComponent<XenoComponent>(out var fullXeno, _compFactory));
        Assert.That(lesser.TryGetComponent<XenoComponent>(out var lesserXeno, _compFactory));
        Assert.That(fullXeno!.ActionIds, Does.Contain("ActionWendigoDoom"));
        Assert.That(lesserXeno!.ActionIds, Does.Not.Contain("ActionWendigoDoom"));
        Assert.That(lesserXeno.ActionIds, Does.Contain("ActionXenoPunch"));
        Assert.That(lesserXeno.ActionIds, Does.Contain("ActionXenoHeadbite"));
    }

    private static FixedPoint2 DeadThreshold(MobThresholdsComponent thresholds)
    {
        foreach (var (threshold, state) in thresholds.Thresholds)
        {
            if (state == MobState.Dead)
                return threshold;
        }

        Assert.Fail("no Dead threshold");
        return FixedPoint2.Zero;
    }

    private static void AssertScaled(DamageSpecifier full, DamageSpecifier lesser, string what)
    {
        Assert.That(lesser.DamageDict, Has.Count.EqualTo(full.DamageDict.Count), what);
        foreach (var (type, amount) in full.DamageDict)
        {
            Assert.That(lesser.DamageDict.TryGetValue(type, out var value), Is.True, $"{what} {type}");
            Assert.That(value.Float(), Is.EqualTo(amount.Float() * 0.8f).Within(0.01f), $"{what} {type}");
        }
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void Mh32SyringeAndCrateAreStocked()
    {
        var syringe = SProtoMan.Index<Robust.Shared.Prototypes.EntityPrototype>("CMUSyringeMH32");
        Assert.That(syringe.TryGetComponent<SolutionComponent>(out var solution, _compFactory));
        Assert.That(solution!.Solution.GetTotalPrototypeQuantity("CMUMH32"), Is.EqualTo(FixedPoint2.New(15)));
        Assert.That(solution.Solution.Volume, Is.EqualTo(FixedPoint2.New(15)));

        var crate = SProtoMan.Index<Robust.Shared.Prototypes.EntityPrototype>("CMUCrateMH32");
        Assert.That(crate.TryGetComponent<StorageFillComponent>(out var fill, _compFactory));
        var total = 0;
        foreach (var entry in fill!.Contents)
        {
            Assert.That(entry.PrototypeId?.Id, Is.EqualTo("CMUSyringeMH32"));
            total += entry.Amount;
        }

        Assert.That(total, Is.EqualTo(2));
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void LabWendigosAreTreatableButNaturalOnesAreNot()
    {
        foreach (var id in new[] { Lab, Lesser })
        {
            var proto = SProtoMan.Index<Robust.Shared.Prototypes.EntityPrototype>(id);
            Assert.That(proto.TryGetComponent<WoundableComponent>(out _, _compFactory), Is.True,
                $"{id} must take wound kits and sutures.");
            Assert.That(proto.TryGetComponent<InjectableSolutionComponent>(out _, _compFactory), Is.True,
                $"{id} must accept syringes and hyposprays.");
            Assert.That(proto.TryGetComponent<BloodstreamComponent>(out _, _compFactory), Is.True, id);
            Assert.That(proto.TryGetComponent<InitialBodyComponent>(out var body, _compFactory), Is.True, id);
            Assert.That(body!.Organs.Values.Select(organ => organ.Id), Does.Contain("CMUOrganWendigoHeart"),
                $"{id} needs a metabolising heart so medicine works.");
        }

        var heart = SProtoMan.Index<Robust.Shared.Prototypes.EntityPrototype>("CMUOrganWendigoHeart");
        Assert.That(heart.TryGetComponent<MetabolizerComponent>(out _, _compFactory), Is.True);

        var natural = SProtoMan.Index<Robust.Shared.Prototypes.EntityPrototype>(Full);
        Assert.That(natural.TryGetComponent<WoundableComponent>(out _, _compFactory), Is.False,
            "Natural Wendigos stay untreatable.");
        Assert.That(natural.TryGetComponent<InjectableSolutionComponent>(out _, _compFactory), Is.False);
    }
}
