using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Fireman;
using Content.Shared.CMU14.PowerLoader;
using Content.Shared.CMU14.Pulling;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Pack;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.ClawSharpness;
using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
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
    private static readonly Robust.Shared.Prototypes.EntProtoId CMUSyringeMH32Prototype = "CMUSyringeMH32";
    private static readonly Robust.Shared.Prototypes.EntProtoId CMUCrateMH32Prototype = "CMUCrateMH32";

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

    /// <summary>
    /// No Wendigo is a xeno: no XenoComponent, actions granted directly. The lesser one keeps less health and no
    /// screech but full damage. All carry the ballistic hide, armor piercing, wall-breaking claws, the pack, the
    /// drag, carry and crate utility, and their emotes; the natural one matches the lab one's hide and piercing.
    /// </summary>
    [Test]
    [RunOnSide(Side.Server)]
    public void WendigosAreNotXenosAndCarryTheirKit()
    {
        var full = SProtoMan.Index<Robust.Shared.Prototypes.EntityPrototype>(Full);
        var lab = SProtoMan.Index<Robust.Shared.Prototypes.EntityPrototype>(Lab);
        var lesser = SProtoMan.Index<Robust.Shared.Prototypes.EntityPrototype>(Lesser);

        Assert.That(lab.TryComp<MobThresholdsComponent>(out var labThresholds, _compFactory));
        Assert.That(lesser.TryComp<MobThresholdsComponent>(out var lesserThresholds, _compFactory));
        Assert.That(DeadThreshold(labThresholds!), Is.EqualTo(FixedPoint2.New(1500)));
        Assert.That(DeadThreshold(lesserThresholds!), Is.EqualTo(FixedPoint2.New(1200)));

        // The lesser variant's damage is restored to the full Wendigo's.
        Assert.That(full.TryComp<XenoPunchComponent>(out var fullPunch, _compFactory));
        Assert.That(lesser.TryComp<XenoPunchComponent>(out var lesserPunch, _compFactory));
        AssertEqual(fullPunch!.Damage, lesserPunch!.Damage, "punch");
        Assert.That(full.TryComp<MeleeWeaponComponent>(out var fullMelee, _compFactory));
        Assert.That(lesser.TryComp<MeleeWeaponComponent>(out var lesserMelee, _compFactory));
        AssertEqual(fullMelee!.Damage, lesserMelee!.Damage, "melee");
        Assert.That(full.TryComp<XenoHeadbiteComponent>(out var fullBite, _compFactory));
        Assert.That(lesser.TryComp<XenoHeadbiteComponent>(out var lesserBite, _compFactory));
        AssertEqual(fullBite!.Damage, lesserBite!.Damage, "headbite");

        string[] shared =
        [
            "ActionXenoWatch", "ActionXenoPunch", "ActionCMUWendigoPunchSwing", "ActionXenoPounce",
            "ActionXenoHeadbite", "ActionCMUWendigoClawedSweep", "ActionApeLimbRip", "ActionXenoRest", "ActionCMUWendigoScent",
        ];
        foreach (var (proto, screech) in new[] { (full, true), (lab, true), (lesser, false) })
        {
            Assert.That(proto.TryComp<XenoComponent>(out _, _compFactory), Is.False, $"{proto.ID} must not be a xeno.");
            Assert.That(proto.TryComp<ActionGrantComponent>(out var grant, _compFactory), proto.ID);
            var actions = grant!.Actions.Select(a => a.Id).ToList();
            Assert.That(actions, Is.SupersetOf(shared), proto.ID);
            Assert.That(actions.Contains("ActionWendigoDoom"), Is.EqualTo(screech), proto.ID);

            Assert.That(proto.TryComp<CMArmorComponent>(out var armor, _compFactory), proto.ID);
            Assert.That(armor!.Bullet, Is.GreaterThan(armor.Melee), $"{proto.ID} hide must resist ballistics best.");
            Assert.That(armor.Melee, Is.GreaterThan(0), proto.ID);
            Assert.That(proto.TryComp<CMArmorPiercingComponent>(out var piercing, _compFactory), proto.ID);
            Assert.That(piercing!.Amount, Is.GreaterThan(0), proto.ID);
            Assert.That(proto.TryComp<XenoClawsComponent>(out var claws, _compFactory), proto.ID);
            Assert.That(claws!.ClawType, Is.EqualTo(XenoClawType.VerySharp), $"{proto.ID} must break reinforced walls.");

            foreach (var utility in new[]
                     {
                         typeof(CMUWendigoPackComponent), typeof(CMUNoPullSlowdownComponent),
                         typeof(CanFiremanCarryComponent), typeof(CMULargeObjectGrabberComponent),
                     })
            {
                Assert.That(proto.Components.ContainsKey(_compFactory.GetComponentName(utility)),
                    $"{proto.ID} must have {utility.Name}.");
            }

            foreach (var emote in new[] { "Growl", "Hiss", "XenoRoar", "XenoHelp", "WendigoBreathing" })
            {
                Assert.That(PassesWhitelist(proto, SProtoMan.Index<EmotePrototype>(emote).Whitelist),
                    $"{proto.ID} must keep the {emote} emote.");
            }
        }
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void NaturalWendigoHideAndPiercingMatchLab()
    {
        var natural = SProtoMan.Index<Robust.Shared.Prototypes.EntityPrototype>(Full);
        var lab = SProtoMan.Index<Robust.Shared.Prototypes.EntityPrototype>(Lab);
        Assert.That(natural.TryComp<CMArmorComponent>(out var naturalArmor, _compFactory));
        Assert.That(lab.TryComp<CMArmorComponent>(out var labArmor, _compFactory));
        Assert.That(naturalArmor!.Melee, Is.EqualTo(labArmor!.Melee));
        Assert.That(naturalArmor.Bullet, Is.EqualTo(labArmor.Bullet));
        Assert.That(naturalArmor.Bio, Is.EqualTo(labArmor.Bio));
        Assert.That(naturalArmor.ExplosionArmor, Is.EqualTo(labArmor.ExplosionArmor));
        Assert.That(natural.TryComp<CMArmorPiercingComponent>(out var naturalPiercing, _compFactory));
        Assert.That(lab.TryComp<CMArmorPiercingComponent>(out var labPiercing, _compFactory));
        Assert.That(naturalPiercing!.Amount, Is.EqualTo(labPiercing!.Amount));
    }

    private bool PassesWhitelist(Robust.Shared.Prototypes.EntityPrototype proto, EntityWhitelist? whitelist)
    {
        if (whitelist == null)
            return true;

        if (whitelist.Components != null && whitelist.Components.Any(proto.Components.ContainsKey))
            return true;

        return proto.TryComp<TagComponent>(out var tags, _compFactory) &&
               whitelist.Tags != null &&
               whitelist.Tags.Any(tag => tags!.Tags.Any(owned => owned == tag));
    }

    private static void AssertEqual(DamageSpecifier full, DamageSpecifier lesser, string what)
    {
        Assert.That(lesser.DamageDict, Has.Count.EqualTo(full.DamageDict.Count), what);
        foreach (var (type, amount) in full.DamageDict)
        {
            Assert.That(lesser.DamageDict.TryGetValue(type, out var value), Is.True, $"{what} {type}");
            Assert.That(value, Is.EqualTo(amount), $"{what} {type}");
        }
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

    [Test]
    [RunOnSide(Side.Server)]
    public void Mh32SyringeAndCrateAreStocked()
    {
        var syringe = SProtoMan.Index<Robust.Shared.Prototypes.EntityPrototype>(CMUSyringeMH32Prototype);
        Assert.That(syringe.TryComp<SolutionComponent>(out var solution, _compFactory));
        Assert.That(solution!.Solution.GetTotalPrototypeQuantity("CMUMH32"), Is.EqualTo(FixedPoint2.New(15)));
        Assert.That(solution.Solution.Volume, Is.EqualTo(FixedPoint2.New(15)));

        var crate = SProtoMan.Index<Robust.Shared.Prototypes.EntityPrototype>(CMUCrateMH32Prototype);
        Assert.That(crate.TryComp<StorageFillComponent>(out var fill, _compFactory));
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
