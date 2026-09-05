#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Humanoid;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.NewPlayer;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared._RMC14.Visor;
using Content.Shared._RMC14.Webbing;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Throwing;
using Robust.Shared;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Species;

[TestFixture]
[TestOf(typeof(SharedVisualBodySystem))]
public sealed class NubodySpeciesBridgeTest : GameTest
{
    private static readonly ResPath HumanPartsRsi = new("/Textures/Mobs/Species/Human/parts.rsi");

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
    [SidedDependency(Side.Server)] private StationSpawningSystem _stationSpawning = default!;
    [SidedDependency(Side.Server)] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [SidedDependency(Side.Server)] private SharedBodyPartHealthSystem _partHealth = default!;
    [SidedDependency(Side.Server)] private SharedVisualBodySystem _visualBody = default!;
    private SpriteSystem Sprites => Client.System<SpriteSystem>();

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
    [TestCase(null)]
    [TestCase("RMCJobSynthetic")]
    public async Task SpawnedRmcPlayerHasBodyOrgansAndVisibleCmuParts(string? job)
    {
        var map = await Pair.CreateTestMap();
        NetEntity bodyNet = default;

        await Server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human", Sex.Male);
            var body = _stationSpawning.SpawnPlayerMob(map.GridCoords, job, profile, station: null);
            bodyNet = SEntMan.GetNetEntity(body);

            var organs = GetOrgansByCategory(body);
            Assert.That(organs.Keys, Is.EquivalentTo(new[]
            {
                "Torso", "Head", "ArmLeft", "ArmRight", "HandLeft", "HandRight",
                "LegLeft", "LegRight", "FootLeft", "FootRight", "Brain", "Eyes",
                "Lungs", "Heart", "Stomach", "Liver", "Kidneys",
            }), "the authoritative body graph must contain every health-analyzer body part and organ");
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var body = CEntMan.GetEntity(bodyNet);
            var sprite = CEntMan.GetComponent<SpriteComponent>(body);
            AssertBodyPart(sprite, body, HumanoidVisualLayers.Chest, "torso_m");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.Head, "head_m");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.LArm, "l_arm");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.RArm, "r_arm");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.LHand, "l_hand");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.RHand, "r_hand");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.LLeg, "l_leg");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.RLeg, "r_leg");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.LFoot, "l_foot");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.RFoot, "r_foot");
        });
    }

    [Test]
    public async Task ConsoleSpawnedCmuHumanHasVisibleBodyAfterNetworkSync()
    {
        var map = await Pair.CreateTestMap();
        NetEntity bodyNet = default;

        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            bodyNet = SEntMan.GetNetEntity(body);

            Assert.That(GetOrgansByCategory(body).Keys, Is.EquivalentTo(new[]
            {
                "Torso", "Head", "ArmLeft", "ArmRight", "HandLeft", "HandRight",
                "LegLeft", "LegRight", "FootLeft", "FootRight", "Brain", "Eyes",
                "Lungs", "Heart", "Stomach", "Liver", "Kidneys",
            }));
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var body = CEntMan.GetEntity(bodyNet);
            var sprite = CEntMan.GetComponent<SpriteComponent>(body);
            AssertBodyPart(sprite, body, HumanoidVisualLayers.Chest, "torso_m");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.Head, "head_m");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.LArm, "l_arm");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.RArm, "r_arm");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.LHand, "l_hand");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.RHand, "r_hand");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.LLeg, "l_leg");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.RLeg, "r_leg");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.LFoot, "l_foot");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.RFoot, "r_foot");
        });
    }

    [Test]
    [PairConfig(nameof(PsDisconnected))]
    public async Task ExistingCmuHumanHasVisibleBodyForLateConnectingClient()
    {
        await Server.WaitPost(() => Server.CfgMan.SetCVar(CVars.NetPVS, true));
        var map = await Pair.CreateTestMap();
        NetEntity bodyNet = default;
        EntityUid serverBody = default;
        EntityUid observer = default;

        await Server.WaitAssertion(() =>
        {
            serverBody = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            observer = SEntMan.SpawnEntity("MobObserver", map.GridCoords);
            bodyNet = SEntMan.GetNetEntity(serverBody);
        });

        await Server.WaitPost(() => Server.CfgMan.SetCVar(RMCCVars.HidePlayerIdentities, true));
        Client.SetConnectTarget(Server);
        var clientNet = Client.ResolveDependency<IClientNetManager>();
        await Client.WaitPost(() => clientNet.ClientConnect(null!, 0, null!));
        await Pair.RunTicksSync(5);
        await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(Server.PlayerMan.Sessions.Single(), observer));
        await Pair.RunTicksSync(300);

        await Client.WaitAssertion(() =>
        {
            var body = CEntMan.GetEntity(bodyNet);
            Assert.That(CEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities, Has.Count.EqualTo(17));
            var sprite = CEntMan.GetComponent<SpriteComponent>(body);
            AssertBodyPart(sprite, body, HumanoidVisualLayers.Chest, "torso_m");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.Head, "head_m");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.LArm, "l_arm");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.RArm, "r_arm");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.LHand, "l_hand");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.RHand, "r_hand");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.LLeg, "l_leg");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.RLeg, "r_leg");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.LFoot, "l_foot");
            AssertBodyPart(sprite, body, HumanoidVisualLayers.RFoot, "r_foot");
        });
    }

    [Test]
    public async Task SeveredCmuArmIsVisibleOnThrownCarrierClientside()
    {
        var map = await Pair.CreateTestMap();
        NetEntity carrierNet = default;
        EntityUid human = default;
        EntityUid gloves = default;
        EntityUid legHuman = default;
        EntityUid shoes = default;
        EntityUid headHuman = default;
        var headEquipment = new Dictionary<string, EntityUid>();

        await Server.WaitAssertion(() =>
        {
            human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            gloves = SEntMan.SpawnEntity("ClothingHandsGlovesCombat", map.GridCoords);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(human, gloves, "gloves"), Is.True);
            Assert.That(_medicalIndex.TryGetBodyPart(
                human,
                new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left),
                out var arm),
                Is.True);

            var damage = new Content.Shared.Damage.DamageSpecifier();
            damage.DamageDict["Slash"] = 1000;
            Assert.That(_partHealth.TryApplyPartDamage(human, arm, damage), Is.True);

            var carrier = SEntMan.EntityQuery<MetaDataComponent>()
                .Select(meta => meta.Owner)
                .Single(uid =>
                    SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "DetachedBody" &&
                    SEntMan.System<Content.Shared.Body.Systems.SharedBodySystem>()
                        .GetRootPartOrNull(uid)?.Entity == arm);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(carrier).EntityName, Is.EqualTo("left arm"));
                Assert.That(SEntMan.HasComponent<PhysicsComponent>(carrier), Is.True,
                    "detached body carriers need item physics or TryThrow rejects them");
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(carrier), Is.True,
                    "severed limb carrier should immediately enter the throwing state");
            });
            carrierNet = SEntMan.GetNetEntity(carrier);

            legHuman = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            shoes = SEntMan.SpawnEntity("ClothingShoesBootsCombat", map.GridCoords);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(legHuman, shoes, "shoes"), Is.True);
            Assert.That(_medicalIndex.TryGetBodyPart(
                legHuman,
                new CMUMedicalBodyPartKey(BodyPartType.Leg, BodyPartSymmetry.Left),
                out var leg),
                Is.True);
            var legDamage = new Content.Shared.Damage.DamageSpecifier();
            legDamage.DamageDict["Slash"] = 1000;
            Assert.That(_partHealth.TryApplyPartDamage(legHuman, leg, legDamage), Is.True);

            headHuman = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            foreach (var (slot, prototype) in new[]
                     {
                         ("ears", "ClothingHeadsetGrey"),
                         ("eyes", "ClothingEyesGlassesSunglasses"),
                         ("mask", "ClothingMaskGas"),
                         ("head", "ClothingHeadHelmetBasic"),
                     })
            {
                var item = SEntMan.SpawnEntity(prototype, map.GridCoords);
                Assert.That(SEntMan.System<InventorySystem>().TryEquip(headHuman, item, slot), Is.True, slot);
                headEquipment.Add(slot, item);
            }

            Assert.That(_medicalIndex.TryGetBodyPart(
                headHuman,
                new CMUMedicalBodyPartKey(BodyPartType.Head, BodyPartSymmetry.None),
                out var head),
                Is.True);
            var headDamage = new Content.Shared.Damage.DamageSpecifier();
            headDamage.DamageDict["Slash"] = 1000;
            Assert.That(_partHealth.TryApplyPartDamage(headHuman, head, headDamage), Is.True);
        });
        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            Assert.Multiple(() =>
            {
                Assert.That(inventory.TryGetSlotEntity(human, "gloves", out _), Is.False);
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(gloves), Is.True,
                    "gloves should be unequipped and flung with a severed arm");
                Assert.That(inventory.TryGetSlotEntity(legHuman, "shoes", out _), Is.False);
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(shoes), Is.True,
                    "shoes should be unequipped and flung with a severed leg");
            });

            foreach (var (slot, item) in headEquipment)
            {
                Assert.That(inventory.TryGetSlotEntity(headHuman, slot, out _), Is.False, slot);
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(item), Is.True,
                    $"{slot} equipment should be unequipped and flung with a severed head");
            }
        });

        await Pair.RunTicksSync(29);

        await Client.WaitAssertion(() =>
        {
            var carrier = CEntMan.GetEntity(carrierNet);
            Assert.That(CEntMan.GetComponent<MetaDataComponent>(carrier).EntityName, Is.EqualTo("left arm"));
            var body = CEntMan.GetComponent<BodyComponent>(carrier);
            Assert.That(body.Organs!.ContainedEntities, Has.Count.EqualTo(2));

            var sprite = CEntMan.GetComponent<SpriteComponent>(carrier);
            AssertBodyPart(sprite, carrier, HumanoidVisualLayers.LArm, "l_arm");
            AssertBodyPart(sprite, carrier, HumanoidVisualLayers.LHand, "l_hand");
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
                Enum enumKey => Sprites.LayerMapTryGet((uid, sprite), enumKey, out _, false),
                string stringKey => Sprites.LayerMapTryGet((uid, sprite), stringKey, out _, false),
                _ => false,
            };
            var index = layer switch
            {
                Enum enumKey when Sprites.LayerMapTryGet((uid, sprite), enumKey, out var enumIndex, false) => enumIndex,
                string stringKey when Sprites.LayerMapTryGet((uid, sprite), stringKey, out var stringIndex, false) => stringIndex,
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

    private void AssertBodyPart(
        SpriteComponent sprite,
        EntityUid body,
        HumanoidVisualLayers layerKey,
        string expectedState)
    {
        Assert.That(Sprites.LayerMapTryGet((body, sprite), layerKey, out var layer, false),
            Is.True,
            $"CMMobHuman is missing its {layerKey} sprite layer");

        var state = Sprites.LayerGetRsiState((body, sprite), layer);
        Assert.Multiple(() =>
        {
            Assert.That(state.Name, Is.EqualTo(expectedState), $"CMMobHuman {layerKey} is not being drawn");
            Assert.That(sprite[layer].ActualRsi?.Path, Is.EqualTo(HumanPartsRsi),
                $"CMMobHuman {layerKey} must use the CMU human body sprites");
            Assert.That(sprite[layer].Visible, Is.True, $"CMMobHuman {layerKey} is hidden");
            Assert.That(sprite[layer].Color.A, Is.GreaterThan(0f), $"CMMobHuman {layerKey} is transparent");
        });
    }

}

#pragma warning restore RA0002
