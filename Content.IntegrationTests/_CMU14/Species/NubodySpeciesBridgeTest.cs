#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Humanoid;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.NewPlayer;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared._RMC14.Visor;
using Content.Shared._RMC14.Webbing;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Species;

[TestFixture]
[TestOf(typeof(SharedVisualBodySystem))]
public sealed class NubodySpeciesBridgeTest : GameTest
{
    private static readonly string[] ExternalCategories =
    [
        "Torso",
        "Head",
        "ArmLeft",
        "ArmRight",
        "HandLeft",
        "HandRight",
        "LegLeft",
        "LegRight",
        "FootLeft",
        "FootRight",
    ];

    private static readonly object[][] StandardSpecies =
    [
        ["Human", "CMMobHuman", "AppearanceCMUHuman"], // CMU medical organ graph
        ["Avali", "CMMobAvali", "AppearanceAvali"],
        ["Arachnid", "CMMobArachnid", "AppearanceArachnid"],
        ["Diona", "CMMobDiona", "AppearanceDiona"],
        ["Dwarf", "CMMobDwarf", "AppearanceDwarf"],
        ["Felinid", "CMMobFelinid", "AppearanceFelinid"],
        ["Feroxi", "RMCMobFeroxi", "AppearanceFeroxi"],
        ["Moth", "CMMobMoth", "AppearanceMoth"],
        ["Reptilian", "CMMobReptilian", "AppearanceReptilian"],
        ["Rodentia", "CMMobRodentia", "AppearanceRodentia"],
        ["Skeleton", "CMMobSkeletonPerson", "AppearanceSkeletonPerson"],
        ["Skrell", "RMCMobSkrell", "AppearanceSkrell"],
        ["SlimePerson", "CMMobSlimePerson", "AppearanceSlimePerson"],
        ["Vox", "CMMobVox", "AppearanceVox"],
        ["Vulpkanin", "RMCMobVulpkanin", "AppearanceVulpkanin"],
        ["WorkingJoe", "AU14MobWorkingJoeColony", "AppearanceWorkingJoe"],
        ["DroneAndroid", "CMUDroneAndroid", "AppearanceDroneAndroid"],
        ["Human", "RMCTrainingDummy", "AppearanceCMUHuman"],
        ["Human", "cultistJob", "AppearanceCMUHuman"],
        ["Human", "CMTestDummy", "AppearanceCMUHuman"],
    ];

    private static readonly object[][] RmcHideLayerContracts =
    [
        ["CMMobHuman", new[] { HumanoidVisualLayers.Hair }],
        ["CMMobArachnid", new[] { HumanoidVisualLayers.Hair }],
        ["CMMobAvali", new[] { HumanoidVisualLayers.HeadTop, HumanoidVisualLayers.HeadSide }],
        ["CMMobDiona", new[] { HumanoidVisualLayers.HeadTop }],
        ["CMMobFelinid", new[] { HumanoidVisualLayers.Hair, HumanoidVisualLayers.HeadTop }],
        ["RMCMobFeroxi", new[] { HumanoidVisualLayers.Snout, HumanoidVisualLayers.HeadTop, HumanoidVisualLayers.HeadSide }],
        ["CMMobMoth", new[] { HumanoidVisualLayers.HeadTop }],
        ["CMMobReptilian", new[] { HumanoidVisualLayers.Snout, HumanoidVisualLayers.HeadTop, HumanoidVisualLayers.HeadSide }],
        ["CMMobRodentia", new[] { HumanoidVisualLayers.Hair, HumanoidVisualLayers.HeadTop, HumanoidVisualLayers.HeadSide, HumanoidVisualLayers.Snout }],
        ["RMCMobSkrell", new[] { HumanoidVisualLayers.Hair }],
        ["CMMobSlimePerson", new[] { HumanoidVisualLayers.Hair }],
        ["RMCMobVulpkanin", new[] { HumanoidVisualLayers.Snout, HumanoidVisualLayers.HeadTop, HumanoidVisualLayers.HeadSide, HumanoidVisualLayers.Hair }],
    ];

    [SidedDependency(Side.Server)] private BodySystem _body = default!;
    [SidedDependency(Side.Server)] private SharedContainerSystem _containers = default!;
    [SidedDependency(Side.Server)] private HumanoidOrganAppearanceSystem _organAppearance = default!;
    [SidedDependency(Side.Server)] private SharedVisualBodySystem _visualBody = default!;
    [SidedDependency(Side.Client)] private SpriteSystem _sprites = default!;

    [Test]
    [TestCaseSource(nameof(StandardSpecies))]
    public async Task StandardSpeciesUseNubodyGraph(
        string species,
        string mobPrototype,
        string appearancePrototype)
    {
        await Server.WaitIdleAsync();

        await Server.WaitAssertion(() =>
        {
            var mob = SEntMan.Spawn(mobPrototype);
            var appearance = SEntMan.Spawn(appearancePrototype);

            try
            {
                AssertNubodyEntity(mob, species, mobPrototype);
                AssertNubodyEntity(appearance, species, appearancePrototype);
            }
            finally
            {
                SEntMan.DeleteEntity(mob);
                SEntMan.DeleteEntity(appearance);
            }
        });

        await Client.WaitAssertion(() =>
        {
            var mob = CEntMan.Spawn(mobPrototype);
            AssertRmcSpriteLayers(mob, mobPrototype);

            // GameTest cleanup owns this directly client-spawned body graph. Recursive deletion here
            // detaches its organs and attempts to play BodyFall on an already terminating entity.
        });
    }

    [Test]
    [TestCaseSource(nameof(RmcHideLayerContracts))]
    public async Task RmcMobHideEligibilityLivesOnItsOrgans(
        string mobPrototype,
        HumanoidVisualLayers[] expectedHeadLayers)
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var mob = SEntMan.Spawn(mobPrototype);
            try
            {
                var body = SEntMan.GetComponent<BodyComponent>(mob);
                Assert.That(body.Organs, Is.Not.Null);

                var organs = body.Organs!.ContainedEntities.ToDictionary(
                    organ => SEntMan.GetComponent<OrganComponent>(organ).Category!.Value.Id,
                    organ => organ);
                var torso = SEntMan.GetComponent<VisualOrganMarkingsComponent>(organs["Torso"]);
                var head = SEntMan.GetComponent<VisualOrganMarkingsComponent>(organs["Head"]);

                Assert.Multiple(() =>
                {
                    Assert.That(torso.HideableLayers,
                        Is.EquivalentTo(new[] { HumanoidVisualLayers.UndergarmentTop }),
                        $"{mobPrototype} torso hide eligibility");
                    Assert.That(head.HideableLayers,
                        Is.EquivalentTo(expectedHeadLayers),
                        $"{mobPrototype} head hide eligibility");
                });
            }
            finally
            {
                SEntMan.DeleteEntity(mob);
            }
        });
    }

    [Test]
    [TestCase("RMCTrainingDummy")]
    [TestCase("cultistJob")]
    [TestCase("CMTestDummy")]
    public async Task RmcHumanDerivedMobsUseTheCanonicalCmuHumanBody(string prototype)
    {
        var expectedOrgans = new Dictionary<string, string>
        {
            ["Torso"] = "CMUPartHumanTorso",
            ["Head"] = "CMUPartHumanHead",
            ["ArmLeft"] = "CMUPartHumanLeftArm",
            ["ArmRight"] = "CMUPartHumanRightArm",
            ["HandLeft"] = "CMUPartHumanLeftHand",
            ["HandRight"] = "CMUPartHumanRightHand",
            ["LegLeft"] = "CMUPartHumanLeftLeg",
            ["LegRight"] = "CMUPartHumanRightLeg",
            ["FootLeft"] = "CMUPartHumanLeftFoot",
            ["FootRight"] = "CMUPartHumanRightFoot",
            ["Brain"] = "CMUOrganHumanBrain",
            ["Eyes"] = "CMUOrganHumanEyes",
            ["Lungs"] = "CMUOrganHumanLungs",
            ["Heart"] = "CMUOrganHumanHeart",
            ["Stomach"] = "CMUOrganHumanStomach",
            ["Liver"] = "CMUOrganHumanLiver",
            ["Kidneys"] = "CMUOrganHumanKidneys",
        };

        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var dummy = SEntMan.Spawn(prototype);
            try
            {
                var initial = SEntMan.GetComponent<InitialBodyComponent>(dummy);
                var actual = GetOrgansByCategory(dummy);

                Assert.That(initial.Organs.ToDictionary(pair => pair.Key.Id, pair => pair.Value.Id),
                    Is.EquivalentTo(expectedOrgans),
                    $"{prototype} must inherit the concrete CMU human InitialBody graph");
                Assert.That(actual.Keys, Is.EquivalentTo(expectedOrgans.Keys));

                foreach (var (category, expectedPrototype) in expectedOrgans)
                {
                    Assert.That(SEntMan.GetComponent<MetaDataComponent>(actual[category]).EntityPrototype?.ID,
                        Is.EqualTo(expectedPrototype),
                        category);
                }
            }
            finally
            {
                SEntMan.DeleteEntity(dummy);
            }
        });
    }

    [Test]
    public async Task HumanoidOrganMarkingReadsAreDefensiveAndWritesUseVisualBody()
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.Spawn("CMMobHuman");
            try
            {
                _organAppearance.SetMarkings(body, "Head", HumanoidVisualLayers.Hair,
                [
                    new Marking("HumanHairAfro", 1).WithColor(Color.Red),
                ]);

                Assert.That(_organAppearance.TryGetMarkings(
                        body,
                        HumanoidVisualLayers.Hair,
                        out var category,
                        out var markingData,
                        out var returned),
                    Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(category.Id, Is.EqualTo("Head"));
                    Assert.That(markingData.Layers, Does.Contain(HumanoidVisualLayers.Hair));
                    Assert.That(returned, Has.Count.EqualTo(1));
                });

                returned.Add(new Marking("HumanHairBob", 1).WithColor(Color.Green));
                ((List<Color>) returned[0].MarkingColors)[0] = Color.Blue;

                var head = GetOrgansByCategory(body)["Head"];
                var live = SEntMan.GetComponent<VisualOrganMarkingsComponent>(head)
                    .Markings[HumanoidVisualLayers.Hair];
                Assert.Multiple(() =>
                {
                    Assert.That(live, Has.Count.EqualTo(1),
                        "mutating the returned list must not mutate authoritative organ markings");
                    Assert.That(live.Single().MarkingId.Id, Is.EqualTo("HumanHairAfro"));
                    Assert.That(live.Single().MarkingColors, Is.EqualTo(new[] { Color.Red }),
                        "returned marking color lists must also be defensive copies");
                });

                Assert.Multiple(() =>
                {
                    Assert.That(_organAppearance.TryAddMarking(body, "MissingMarkingPrototype", Color.White),
                        Is.False);
                    Assert.That(_organAppearance.TryAddMarking(body, "MothWingsDefault", Color.White),
                        Is.False,
                        "a valid marking whose layer is not owned by this body must fail unchanged");
                    Assert.That(live, Has.Count.EqualTo(1));
                });

                Assert.That(_organAppearance.TryAddMarking(body, "HumanHairBob", Color.Green), Is.True);
                live = SEntMan.GetComponent<VisualOrganMarkingsComponent>(head)
                    .Markings[HumanoidVisualLayers.Hair];
                Assert.Multiple(() =>
                {
                    Assert.That(live, Has.Count.EqualTo(2));
                    Assert.That(live.Last().MarkingId.Id, Is.EqualTo("HumanHairBob"));
                    Assert.That(live.Last().MarkingColors, Is.EqualTo(new[] { Color.Green }));
                });
            }
            finally
            {
                SEntMan.DeleteEntity(body);
            }
        });
    }

    [Test]
    public async Task HumanoidAppearanceColorsPreferHeadThenTorsoThenStableCategory()
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.Spawn("CMMobHuman");
            var bodyComponent = SEntMan.GetComponent<BodyComponent>(body);
            var organs = GetOrgansByCategory(body);
            var head = organs["Head"];
            var torso = organs["Torso"];
            var leftArm = organs["ArmLeft"];
            var rightArm = organs["ArmRight"];

            try
            {
                _visualBody.ApplyProfiles(body, new()
                {
                    ["Head"] = Profile(Color.Red, Color.Blue),
                    ["Torso"] = Profile(Color.Green, Color.Yellow),
                    ["ArmLeft"] = Profile(Color.Orange, Color.Purple),
                    ["ArmRight"] = Profile(Color.Cyan, Color.Brown),
                });

                AssertAppearanceColors(body, Color.Red, Color.Blue, "Head must be the canonical source");

                Assert.That(_containers.Remove(head, bodyComponent.Organs!), Is.True);
                AssertAppearanceColors(body, Color.Green, Color.Yellow,
                    "Torso must be the canonical source when Head is absent");

                Assert.That(_containers.Remove(torso, bodyComponent.Organs!), Is.True);
                Assert.That(_containers.Remove(leftArm, bodyComponent.Organs!), Is.True);
                Assert.That(_containers.Remove(rightArm, bodyComponent.Organs!), Is.True);

                Assert.That(_containers.Insert(rightArm, bodyComponent.Organs!, force: true), Is.True);
                Assert.That(_containers.Insert(leftArm, bodyComponent.Organs!, force: true), Is.True);
                AssertAppearanceColors(body, Color.Orange, Color.Purple,
                    "ordinal category fallback must not depend on right-before-left insertion order");

                Assert.That(_containers.Remove(leftArm, bodyComponent.Organs!), Is.True);
                Assert.That(_containers.Remove(rightArm, bodyComponent.Organs!), Is.True);
                Assert.That(_containers.Insert(leftArm, bodyComponent.Organs!, force: true), Is.True);
                Assert.That(_containers.Insert(rightArm, bodyComponent.Organs!, force: true), Is.True);
                AssertAppearanceColors(body, Color.Orange, Color.Purple,
                    "ordinal category fallback must remain stable under the opposite insertion order");
            }
            finally
            {
                if (!bodyComponent.Organs!.Contains(head))
                    _containers.Insert(head, bodyComponent.Organs, force: true);
                if (!bodyComponent.Organs.Contains(torso))
                    _containers.Insert(torso, bodyComponent.Organs, force: true);
                SEntMan.DeleteEntity(body);
            }
        });
    }

    private Dictionary<string, EntityUid> GetOrgansByCategory(EntityUid body)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities.ToDictionary(
            organ => SEntMan.GetComponent<OrganComponent>(organ).Category!.Value.Id,
            organ => organ);
    }

    private static OrganProfileData Profile(Color skin, Color eyes)
    {
        return new OrganProfileData
        {
            Sex = Sex.Male,
            SkinColor = skin,
            EyeColor = eyes,
        };
    }

    private void AssertAppearanceColors(EntityUid body, Color skin, Color eyes, string message)
    {
        Assert.That(_organAppearance.TryGetAppearance(body, out var actualSkin, out var actualEyes, out _),
            Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(actualSkin, Is.EqualTo(skin), message);
            Assert.That(actualEyes, Is.EqualTo(eyes), message);
        });
    }

    private void AssertNubodyEntity(EntityUid uid, ProtoId<SpeciesPrototype> species, string prototype)
    {
        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.HasComponent<BodyComponent>(uid), Is.True, prototype);
            Assert.That(SEntMan.HasComponent<InitialBodyComponent>(uid), Is.True, prototype);
            Assert.That(SEntMan.HasComponent<VisualBodyComponent>(uid), Is.True, prototype);
            Assert.That(SEntMan.HasComponent<HumanoidProfileComponent>(uid), Is.True, prototype);
            Assert.That(SEntMan.HasComponent<HideableHumanoidLayersComponent>(uid), Is.True, prototype);
        });

        var profile = SEntMan.GetComponent<HumanoidProfileComponent>(uid);
        Assert.That(profile.Species, Is.EqualTo(species), prototype);

        var initial = SEntMan.GetComponent<InitialBodyComponent>(uid);
        var body = SEntMan.GetComponent<BodyComponent>(uid);
        Assert.That(body.Organs, Is.Not.Null, prototype);

        var actualOrgans = body.Organs!.ContainedEntities.ToDictionary(
            organ => SEntMan.GetComponent<OrganComponent>(organ).Category!.Value,
            organ => organ);
        Assert.That(actualOrgans.Keys, Is.EquivalentTo(initial.Organs.Keys),
            $"{prototype} must spawn every category declared by its Doll InitialBody graph");

        foreach (var (category, expectedPrototype) in initial.Organs)
        {
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(actualOrgans[category]).EntityPrototype?.ID,
                Is.EqualTo(expectedPrototype.Id),
                $"{prototype} category {category.Id}");
        }

        var markingGroup = species.Id switch
        {
            "Dwarf" => "Human",
            "SlimePerson" => "Slime",
            _ => species.Id,
        };
        foreach (var category in ExternalCategories)
        {
            var markingComponent = SEntMan.GetComponent<VisualOrganMarkingsComponent>(actualOrgans[category]);
            Assert.Multiple(() =>
            {
                Assert.That(markingComponent.MarkingData.Group.Id, Is.EqualTo(markingGroup),
                    $"{prototype} category {category} markings group");
                Assert.That(markingComponent.MarkingData.Layers, Is.Not.Empty,
                    $"{prototype} category {category} marking ownership");
            });
        }

        Assert.That(_body.TryGetOrgansWithComponent<VisualOrganComponent>(uid, out var visualOrgans),
            Is.True,
            prototype);
        Assert.That(visualOrgans, Is.Not.Empty, prototype);

        Assert.That(_organAppearance.TryGetAppearance(uid, out _, out _, out var markings),
            Is.True,
            prototype);
        Assert.That(markings, Is.Not.Empty, prototype);
    }

    private void AssertRmcSpriteLayers(EntityUid uid, string prototype)
    {
        var sprite = CEntMan.GetComponent<SpriteComponent>(uid);
        object[] orderedLayers =
        [
            HumanoidVisualLayers.LHand,
            HumanoidVisualLayers.RHand,
            HumanoidVisualLayers.Overlay,
            "gloves",
            SquadArmorLayers.Gloves,
            "shoes",
            "id",
            "ears",
            "eyes",
            "outerClothing",
            "belt",
            SquadArmorLayers.Armor,
            WebbingVisualLayers.Base,
            UniformAccessoryLayer.Base,
            MedalVisualLayers.Base,
            MedalVisualLayers.Base1,
            "back",
            "neck",
            "suitstorage",
            HumanoidVisualLayers.SnoutCover,
            HumanoidVisualLayers.Tail,
            HumanoidVisualLayers.TailOverlay,
            "mask",
            "head",
            SquadArmorLayers.Helmet,
            VisorVisualLayers.Base,
            "pocket1",
            "pocket2",
            HumanoidVisualLayers.Handcuffs,
            "acided",
            "hooked",
            NewPlayerLayers.Layer,
        ];

        var previous = -1;
        foreach (var layer in orderedLayers)
        {
            var found = layer switch
            {
                Enum enumKey => _sprites.LayerMapTryGet((uid, sprite), enumKey, out _, false),
                string stringKey => _sprites.LayerMapTryGet((uid, sprite), stringKey, out _, false),
                _ => false,
            };
            var index = layer switch
            {
                Enum enumKey when _sprites.LayerMapTryGet((uid, sprite), enumKey, out var enumIndex, false) => enumIndex,
                string stringKey when _sprites.LayerMapTryGet((uid, sprite), stringKey, out var stringIndex, false) => stringIndex,
                _ => -1,
            };
            Assert.That(found,
                Is.True,
                $"{prototype} is missing sprite layer {layer}");
            Assert.That(index, Is.GreaterThan(previous),
                $"{prototype} sprite layer {layer} is out of order");
            previous = index;
        }
    }
}

#pragma warning restore RA0002
