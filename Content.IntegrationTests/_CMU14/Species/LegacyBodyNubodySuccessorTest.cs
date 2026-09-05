using System.Collections.Generic;
using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Body.Components;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Metabolism;
using Content.Shared.Movement.Components;
using Content.Shared.Stunnable;
using Content.Shared.Temperature.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Species;

[TestFixture]
[TestOf(typeof(InitialBodySystem))]
public sealed class LegacyBodyNubodySuccessorTest : GameTest
{
    private static readonly IReadOnlyDictionary<string, string> AnimalOrgans = new Dictionary<string, string>
    {
        ["Torso"] = "TorsoAnimal",
        ["Legs"] = "LegsAnimal",
        ["Feet"] = "FeetAnimal",
        ["Lungs"] = "OrganAnimalLungs",
        ["Stomach"] = "OrganAnimalStomach",
        ["Liver"] = "OrganAnimalLiver",
        ["Heart"] = "OrganAnimalHeart",
        ["Kidneys"] = "OrganAnimalKidneys",
    };

    private static readonly IReadOnlyDictionary<string, string> MouseOrgans =
        WithReplacements(AnimalOrgans, ("Stomach", "OrganMouseStomach"));

    private static readonly IReadOnlyDictionary<string, string> PrimateOrgans =
        WithReplacements(
            AnimalOrgans,
            ("Torso", "TorsoPrimate"),
            ("Hands", "HandsAnimal"));

    private static readonly IReadOnlyDictionary<string, string> ApeOrgans = WithReplacements(
        AnimalOrgans,
        ("Stomach", "OrganApeStomach"),
        ("Liver", "OrganApeLiver"),
        ("Heart", "OrganApeHeart"));

    private static readonly IReadOnlyDictionary<string, string> BloodsuckerOrgans = WithReplacements(
        AnimalOrgans,
        ("Stomach", "OrganBloodsuckerStomach"),
        ("Liver", "OrganBloodsuckerLiver"),
        ("Heart", "OrganBloodsuckerHeart"));

    [Test]
    public async Task LegacyAnimalGraphsUseRelatedNubodyOrgans()
    {
        await AssertAnimalGraph("RMCMobCat", 0, AnimalOrgans, hasCombinedHands: false);
        await AssertAnimalGraph("CMMobMouse", 0, MouseOrgans, hasCombinedHands: false);
        await AssertAnimalGraph("CMMobSmallHostMonkey", 1, PrimateOrgans, hasCombinedHands: true);
        await AssertAnimalGraph("CMUMobApe", 1, ApeOrgans, hasCombinedHands: false);
        await AssertAnimalGraph("CMUMobSmallHostCarp", 0, BloodsuckerOrgans, hasCombinedHands: false);
    }

    [Test]
    public async Task XenoHeadAndHeartFormARealNubodyGraph()
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var xeno = SEntMan.Spawn("CMXenoDrone");
            try
            {
                var body = SEntMan.GetComponent<BodyComponent>(xeno);
                Assert.That(body.RequiredLegs, Is.Zero);
                var organs = GetOrgans(body);
                Assert.That(organs.Keys, Is.EquivalentTo(new[] { "Head", "Heart" }));
                AssertPrototype(organs["Head"], "RMCHeadXeno");
                AssertPrototype(organs["Heart"], "RMCOrganXenoHeart");

                var head = SEntMan.GetComponent<BodyPartComponent>(organs["Head"]);
                Assert.Multiple(() =>
                {
                    Assert.That(head.PartType, Is.EqualTo(BodyPartType.Head));
                    Assert.That(head.IsVital, Is.True);
                    Assert.That(head.Organs.Keys, Is.EquivalentTo(new[] { "heart" }));
                    Assert.That(head.Organs["heart"].Id, Is.EqualTo("heart"));
                    Assert.That(SharedBodySystem.GetCanonicalSlotId("Head"), Is.EqualTo("head"));
                    Assert.That(SharedBodySystem.GetCanonicalSlotId("Heart"), Is.EqualTo("heart"));
                });

                AssertParent(organs["Heart"], organs["Head"]);
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<GibbableOrganComponent>(organs["Heart"]), Is.False);
                    Assert.That(SEntMan.HasComponent<MetabolizerComponent>(organs["Heart"]), Is.False);
                });
                var bodySystem = SEntMan.System<SharedBodySystem>();
                Assert.That(bodySystem.GetRootPartOrNull(xeno)?.Entity, Is.EqualTo(organs["Head"]));
                Assert.That(bodySystem.GetPartOrgans(organs["Head"]).Select(entry => entry.Id),
                    Is.EquivalentTo(new[] { organs["Heart"] }));
            }
            finally
            {
                SEntMan.DeleteEntity(xeno);
            }
        });
    }

    [Test]
    public async Task CombinedAnimalHandsDoNotCreateDuplicateIntrinsicHands()
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            foreach (var prototype in new[] { "CMMobSmallHostMonkey", "CMUMobApe" })
            {
                var mob = SEntMan.Spawn(prototype);
                try
                {
                    var hands = SEntMan.GetComponent<HandsComponent>(mob);
                    Assert.Multiple(() =>
                    {
                        Assert.That(hands.Hands.Keys, Is.EquivalentTo(new[] { "hand_right" }), prototype);
                        Assert.That(hands.SortedHands, Is.EqualTo(new[] { "hand_right" }), prototype);
                        Assert.That(hands.ActiveHandId, Is.EqualTo("hand_right"), prototype);
                        Assert.That(hands.Hands.Keys.Any(id => id.StartsWith("body_part_slot_", StringComparison.Ordinal)),
                            Is.False,
                            prototype);
                    });
                }
                finally
                {
                    SEntMan.DeleteEntity(mob);
                }
            }
        });
    }

    [Test]
    public async Task RmcHumanBasePreservesNonOrganicForkContract()
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.Spawn("CMMobHuman");
            try
            {
                var slowdown = SEntMan.GetComponent<SlowOnDamageComponent>(human);
                var temperature = SEntMan.GetComponent<TemperatureComponent>(human);
                Assert.Multiple(() =>
                {
                    Assert.That(slowdown.SpeedModifierThresholds[60], Is.EqualTo(0.85f));
                    Assert.That(slowdown.SpeedModifierThresholds[80], Is.EqualTo(0.75f));
                    Assert.That(temperature.SpecificHeat, Is.EqualTo(42f));
                    Assert.That(SEntMan.HasComponent<RespiratorComponent>(human), Is.False);
                    Assert.That(SEntMan.HasComponent<BarotraumaComponent>(human), Is.False);
                    Assert.That(SEntMan.HasComponent<PassiveDamageComponent>(human), Is.False);
                    Assert.That(SEntMan.HasComponent<PerishableComponent>(human), Is.False);
                    Assert.That(SEntMan.HasComponent<TemperatureDamageComponent>(human), Is.False);
                    Assert.That(SEntMan.HasComponent<StunVisualsComponent>(human), Is.False);
                });
            }
            finally
            {
                SEntMan.DeleteEntity(human);
            }
        });
    }

    private async Task AssertAnimalGraph(
        string prototype,
        int requiredLegs,
        IReadOnlyDictionary<string, string> expected,
        bool hasCombinedHands)
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var mob = SEntMan.Spawn(prototype);
            try
            {
                var body = SEntMan.GetComponent<BodyComponent>(mob);
                var organs = GetOrgans(body);
                Assert.Multiple(() =>
                {
                    Assert.That(body.RequiredLegs, Is.EqualTo(requiredLegs), prototype);
                    Assert.That(organs.Keys, Is.EquivalentTo(expected.Keys), prototype);
                });

                foreach (var (category, organPrototype) in expected)
                    AssertPrototype(organs[category], organPrototype);

                var torso = SEntMan.GetComponent<BodyPartComponent>(organs["Torso"]);
                var legs = SEntMan.GetComponent<BodyPartComponent>(organs["Legs"]);
                var feet = SEntMan.GetComponent<BodyPartComponent>(organs["Feet"]);
                Assert.Multiple(() =>
                {
                    Assert.That(torso.PartType, Is.EqualTo(BodyPartType.Torso), prototype);
                    Assert.That(torso.Children.Keys,
                        Is.EquivalentTo(hasCombinedHands ? new[] { "hands", "legs" } : new[] { "legs" }),
                        prototype);
                    Assert.That(torso.Organs.Keys,
                        Is.EquivalentTo(new[] { "lungs", "stomach", "liver", "heart", "kidneys" }),
                        prototype);
                    Assert.That(legs.PartType, Is.EqualTo(BodyPartType.Leg), prototype);
                    Assert.That(legs.Symmetry, Is.EqualTo(BodyPartSymmetry.None), prototype);
                    Assert.That(legs.Children.Keys, Is.EquivalentTo(new[] { "feet" }), prototype);
                    Assert.That(feet.PartType, Is.EqualTo(BodyPartType.Foot), prototype);
                    Assert.That(feet.Symmetry, Is.EqualTo(BodyPartSymmetry.None), prototype);
                    Assert.That(SEntMan.HasComponent<MovementBodyPartComponent>(organs["Legs"]), Is.True, prototype);
                    Assert.That(SEntMan.HasComponent<MovementBodyPartComponent>(organs["Feet"]), Is.False, prototype);
                    Assert.That(SharedBodySystem.GetCanonicalSlotId("Legs"), Is.EqualTo("legs"));
                    Assert.That(SharedBodySystem.GetCanonicalSlotId("Feet"), Is.EqualTo("feet"));
                    Assert.That(SharedBodySystem.GetCanonicalSlotId("Hands"), Is.EqualTo("hands"));
                    Assert.That(body.LegEntities, Is.EquivalentTo(new[] { organs["Legs"] }), prototype);
                });

                AssertParent(organs["Legs"], organs["Torso"]);
                AssertParent(organs["Feet"], organs["Legs"]);
                foreach (var category in new[] { "Lungs", "Stomach", "Liver", "Heart", "Kidneys" })
                    AssertParent(organs[category], organs["Torso"]);

                if (hasCombinedHands)
                {
                    var hands = SEntMan.GetComponent<BodyPartComponent>(organs["Hands"]);
                    Assert.Multiple(() =>
                    {
                        Assert.That(hands.PartType, Is.EqualTo(BodyPartType.Hand), prototype);
                        Assert.That(hands.Symmetry, Is.EqualTo(BodyPartSymmetry.Left), prototype);
                        Assert.That(SEntMan.HasComponent<HandOrganComponent>(organs["Hands"]), Is.False, prototype);
                    });
                    AssertParent(organs["Hands"], organs["Torso"]);
                }

                var bodySystem = SEntMan.System<SharedBodySystem>();
                Assert.That(bodySystem.GetRootPartOrNull(mob)?.Entity, Is.EqualTo(organs["Torso"]), prototype);

                if (requiredLegs == 1)
                {
                    var movementPart = SEntMan.GetComponent<MovementBodyPartComponent>(organs["Legs"]);
                    // Pre-Nubody SharedBodySystem also recomputed the entity base speed from
                    // attached MovementBodyPart values divided by RequiredLegs.
                    bodySystem.UpdateMovementSpeed(mob, body);
                    var movement = SEntMan.GetComponent<MovementSpeedModifierComponent>(mob);
                    Assert.Multiple(() =>
                    {
                        Assert.That(movement.BaseWalkSpeed, Is.EqualTo(movementPart.WalkSpeed), prototype);
                        Assert.That(movement.BaseSprintSpeed, Is.EqualTo(movementPart.SprintSpeed), prototype);
                    });
                }
            }
            finally
            {
                SEntMan.DeleteEntity(mob);
            }
        });
    }

    private Dictionary<string, EntityUid> GetOrgans(BodyComponent body)
    {
        Assert.That(body.Organs, Is.Not.Null);
        return body.Organs!.ContainedEntities.ToDictionary(
            uid => SEntMan.GetComponent<OrganComponent>(uid).Category!.Value.Id,
            uid => uid);
    }

    private void AssertParent(EntityUid child, EntityUid expectedParent)
    {
        var relation = SEntMan.GetComponent<ChildOrganComponent>(child);
        Assert.That(relation.Parent, Is.EqualTo(expectedParent), child.ToString());
        Assert.That(SEntMan.GetComponent<ParentOrganComponent>(expectedParent).Children,
            Does.Contain(child),
            expectedParent.ToString());
    }

    private void AssertPrototype(EntityUid uid, string prototype)
    {
        Assert.That(SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID, Is.EqualTo(prototype));
    }

    private static IReadOnlyDictionary<string, string> WithReplacements(
        IReadOnlyDictionary<string, string> source,
        params (string Category, string Prototype)[] replacements)
    {
        var result = source.ToDictionary(entry => entry.Key, entry => entry.Value);
        foreach (var (category, prototype) in replacements)
            result[category] = prototype;
        return result;
    }
}
