using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.Movement.Components;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Species;

[TestFixture]
[TestOf(typeof(SharedVisualBodySystem))]
public sealed class CMUNubodyMedicalGraphTest : GameTest
{
    [TestPrototypes]
    private const string PartialRoboticPrototype = @"
- type: entity
  parent: AppearanceCMUHuman
  id: CMUNubodyPartialRoboticAppearance
  categories: [ HideSpawnMenu ]
  components:
  - type: InitialBody
    organs:
      Torso: CMUPartHumanTorso
      Head: CMUPartHumanHead
      ArmLeft: CMUPartRoboticLeftArm
      ArmRight: CMUPartHumanRightArm
      HandLeft: CMUPartRoboticLeftHand
      HandRight: CMUPartHumanRightHand
      LegLeft: CMUPartHumanLeftLeg
      LegRight: CMUPartHumanRightLeg
      FootLeft: CMUPartHumanLeftFoot
      FootRight: CMUPartHumanRightFoot
      Brain: CMUOrganHumanBrain
      Eyes: CMUOrganHumanEyes
      Lungs: CMUOrganHumanLungs
      Heart: CMUOrganHumanHeart
      Stomach: CMUOrganHumanStomach
      Liver: CMUOrganHumanLiver
      Kidneys: CMUOrganHumanKidneys
";

    private static readonly Dictionary<string, string> CmuHumanExternalOrgans = new()
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
    };

    private static readonly Dictionary<string, string> WorkingJoeExternalOrgans = new()
    {
        ["Torso"] = "CMUPartWorkingJoeTorso",
        ["Head"] = "CMUPartWorkingJoeHead",
        ["ArmLeft"] = "CMUPartWorkingJoeLeftArm",
        ["ArmRight"] = "CMUPartWorkingJoeRightArm",
        ["HandLeft"] = "CMUPartWorkingJoeLeftHand",
        ["HandRight"] = "CMUPartWorkingJoeRightHand",
        ["LegLeft"] = "CMUPartWorkingJoeLeftLeg",
        ["LegRight"] = "CMUPartWorkingJoeRightLeg",
        ["FootLeft"] = "CMUPartWorkingJoeLeftFoot",
        ["FootRight"] = "CMUPartWorkingJoeRightFoot",
    };

    private static readonly Dictionary<string, string> DroneAndroidExternalOrgans = new()
    {
        ["Torso"] = "CMUPartDroneAndroidTorso",
        ["Head"] = "CMUPartDroneAndroidHead",
        ["ArmLeft"] = "CMUPartRoboticLeftArm",
        ["ArmRight"] = "CMUPartRoboticRightArm",
        ["HandLeft"] = "CMUPartRoboticLeftHand",
        ["HandRight"] = "CMUPartRoboticRightHand",
        ["LegLeft"] = "CMUPartRoboticLeftLeg",
        ["LegRight"] = "CMUPartRoboticRightLeg",
        ["FootLeft"] = "CMUPartRoboticLeftFoot",
        ["FootRight"] = "CMUPartRoboticRightFoot",
    };

    private static readonly Dictionary<string, string> CmuInternalOrgans = new()
    {
        ["Brain"] = "CMUOrganHumanBrain",
        ["Eyes"] = "CMUOrganHumanEyes",
        ["Lungs"] = "CMUOrganHumanLungs",
        ["Heart"] = "CMUOrganHumanHeart",
        ["Stomach"] = "CMUOrganHumanStomach",
        ["Liver"] = "CMUOrganHumanLiver",
        ["Kidneys"] = "CMUOrganHumanKidneys",
    };

    private static readonly Dictionary<string, (BodyPartType Type, BodyPartSymmetry Symmetry)> BodyPartMetadata = new()
    {
        ["Torso"] = (BodyPartType.Torso, BodyPartSymmetry.None),
        ["Head"] = (BodyPartType.Head, BodyPartSymmetry.None),
        ["ArmLeft"] = (BodyPartType.Arm, BodyPartSymmetry.Left),
        ["ArmRight"] = (BodyPartType.Arm, BodyPartSymmetry.Right),
        ["HandLeft"] = (BodyPartType.Hand, BodyPartSymmetry.Left),
        ["HandRight"] = (BodyPartType.Hand, BodyPartSymmetry.Right),
        ["LegLeft"] = (BodyPartType.Leg, BodyPartSymmetry.Left),
        ["LegRight"] = (BodyPartType.Leg, BodyPartSymmetry.Right),
        ["FootLeft"] = (BodyPartType.Foot, BodyPartSymmetry.Left),
        ["FootRight"] = (BodyPartType.Foot, BodyPartSymmetry.Right),
    };

    [Test]
    public async Task CmuHumanUsesExactMedicalOrganGraph()
    {
        await AssertExactCmuGraph(
            "CMMobHuman",
            "Human",
            CmuHumanExternalOrgans,
            "Mobs/Species/Human/parts.rsi",
            allExternalPartsHaveBones: true,
            syntheticAppearance: false);
    }

    [Test]
    public async Task WorkingJoeUsesCmuMedicalGraphWithJoeVisuals()
    {
        await AssertExactCmuGraph(
            "AU14MobWorkingJoeColony",
            "WorkingJoe",
            WorkingJoeExternalOrgans,
            "Mobs/WorkingJoe/parts.rsi",
            allExternalPartsHaveBones: true,
            syntheticAppearance: true);
    }

    [Test]
    public async Task DroneAndroidUsesCmuInternalsAndRoboticLimbs()
    {
        await AssertExactCmuGraph(
            "CMUDroneAndroid",
            "DroneAndroid",
            DroneAndroidExternalOrgans,
            "Mobs/DroneAndroid/parts.rsi",
            allExternalPartsHaveBones: false,
            syntheticAppearance: true,
            roboticLimbs: true);
    }

    [Test]
    public async Task PartialRoboticGraphUsesOrganOwnedVisualsWithoutReplacingHumanParts()
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var mob = SEntMan.Spawn("CMUNubodyPartialRoboticAppearance");
            try
            {
                var organs = GetOrgansByCategory(mob);
                Assert.Multiple(() =>
                {
                    Assert.That(organs, Has.Count.EqualTo(17));
                    Assert.That(SEntMan.GetComponent<MetaDataComponent>(organs["ArmLeft"]).EntityPrototype?.ID,
                        Is.EqualTo("CMUPartRoboticLeftArm"));
                    Assert.That(SEntMan.GetComponent<MetaDataComponent>(organs["HandLeft"]).EntityPrototype?.ID,
                        Is.EqualTo("CMUPartRoboticLeftHand"));
                    Assert.That(SEntMan.GetComponent<MetaDataComponent>(organs["ArmRight"]).EntityPrototype?.ID,
                        Is.EqualTo("CMUPartHumanRightArm"));
                    Assert.That(SEntMan.GetComponent<MetaDataComponent>(organs["HandRight"]).EntityPrototype?.ID,
                        Is.EqualTo("CMUPartHumanRightHand"));
                });

                AssertRoboticVisual(organs["ArmLeft"], HumanoidVisualLayers.LArm, "l_arm");
                AssertRoboticVisual(organs["HandLeft"], HumanoidVisualLayers.LHand, "l_hand");
            }
            finally
            {
                SEntMan.DeleteEntity(mob);
            }
        });
    }

    private async Task AssertExactCmuGraph(
        string mobPrototype,
        string species,
        IReadOnlyDictionary<string, string> externalPrototypes,
        string sprite,
        bool allExternalPartsHaveBones,
        bool syntheticAppearance,
        bool roboticLimbs = false)
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var mob = SEntMan.Spawn(mobPrototype);
            try
            {
                var profile = SEntMan.GetComponent<HumanoidProfileComponent>(mob);
                Assert.That(profile.Species.Id, Is.EqualTo(species), $"{mobPrototype} Nubody profile");

                var body = SEntMan.GetComponent<BodyComponent>(mob);
                Assert.That(body.Organs, Is.Not.Null, mobPrototype);
                Assert.That(body.Organs!.ContainedEntities, Has.Count.EqualTo(17), mobPrototype);

                var organs = new Dictionary<string, EntityUid>();
                foreach (var organ in body.Organs.ContainedEntities)
                {
                    var component = SEntMan.GetComponent<OrganComponent>(organ);
                    Assert.That(component.Category, Is.Not.Null, organ.ToString());
                    organs.Add(component.Category!.Value.Id, organ);
                }

                Assert.That(organs.Keys,
                    Is.EquivalentTo(externalPrototypes.Keys.Concat(CmuInternalOrgans.Keys)),
                    mobPrototype);
                AssertPrototypeMap(organs, externalPrototypes);
                AssertPrototypeMap(organs, CmuInternalOrgans);
                AssertExactNubodyHands(mob, organs);

                foreach (var (category, expected) in BodyPartMetadata)
                {
                    var organ = organs[category];
                    var part = SEntMan.GetComponent<BodyPartComponent>(organ);
                    Assert.Multiple(() =>
                    {
                        Assert.That(part.PartType, Is.EqualTo(expected.Type), category);
                        Assert.That(part.Symmetry, Is.EqualTo(expected.Symmetry), category);
                        Assert.That(SEntMan.HasComponent<BodyPartHealthComponent>(organ), Is.True, category);
                    });

                    var shouldHaveBone = allExternalPartsHaveBones || category is "Torso" or "Head";
                    Assert.That(SEntMan.HasComponent<BoneComponent>(organ), Is.EqualTo(shouldHaveBone), category);
                }

                foreach (var category in CmuInternalOrgans.Keys)
                    Assert.That(SEntMan.HasComponent<OrganHealthComponent>(organs[category]), Is.True, category);

                AssertCanonicalSurgerySlots(organs);
                Assert.That(SEntMan.HasComponent<MovementBodyPartComponent>(organs["LegLeft"]), Is.True);
                Assert.That(SEntMan.HasComponent<MovementBodyPartComponent>(organs["LegRight"]), Is.True);

                foreach (var category in externalPrototypes.Keys)
                {
                    var visual = SEntMan.GetComponent<VisualOrganComponent>(organs[category]);
                    var markings = SEntMan.GetComponent<VisualOrganMarkingsComponent>(organs[category]);
                    Assert.Multiple(() =>
                    {
                        Assert.That(visual.Data.RsiPath, Is.EqualTo(sprite), category);
                        Assert.That(markings.MarkingData.Group.Id, Is.EqualTo(species),
                            $"{category} markings group");
                        Assert.That(markings.MarkingData.Layers, Is.Not.Empty,
                            $"{category} marking ownership");
                    });
                }

                AssertSexedVisualStates(organs);
                if (syntheticAppearance)
                    AssertSyntheticHideableLayers(organs);
                if (roboticLimbs)
                    AssertRoboticLimbMetadata(organs);
            }
            finally
            {
                SEntMan.DeleteEntity(mob);
            }
        });
    }

    private void AssertPrototypeMap(
        IReadOnlyDictionary<string, EntityUid> organs,
        IReadOnlyDictionary<string, string> expected)
    {
        foreach (var (category, prototype) in expected)
        {
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(organs[category]).EntityPrototype?.ID,
                Is.EqualTo(prototype),
                category);
        }
    }

    private Dictionary<string, EntityUid> GetOrgansByCategory(EntityUid bodyUid)
    {
        var body = SEntMan.GetComponent<BodyComponent>(bodyUid);
        Assert.That(body.Organs, Is.Not.Null);

        var organs = new Dictionary<string, EntityUid>();
        foreach (var organ in body.Organs!.ContainedEntities)
        {
            var component = SEntMan.GetComponent<OrganComponent>(organ);
            Assert.That(component.Category, Is.Not.Null, organ.ToString());
            organs.Add(component.Category!.Value.Id, organ);
        }

        return organs;
    }

    private void AssertSexedVisualStates(IReadOnlyDictionary<string, EntityUid> organs)
    {
        var head = SEntMan.GetComponent<VisualOrganComponent>(organs["Head"]);
        var torso = SEntMan.GetComponent<VisualOrganComponent>(organs["Torso"]);
        Assert.Multiple(() =>
        {
            Assert.That(head.SexStateOverrides![Sex.Male], Is.EqualTo("head_m"));
            Assert.That(head.SexStateOverrides[Sex.Female], Is.EqualTo("head_f"));
            Assert.That(torso.SexStateOverrides![Sex.Male], Is.EqualTo("torso_m"));
            Assert.That(torso.SexStateOverrides[Sex.Female], Is.EqualTo("torso_f"));
        });
    }

    private void AssertExactNubodyHands(EntityUid mob, IReadOnlyDictionary<string, EntityUid> organs)
    {
        var hands = SEntMan.GetComponent<HandsComponent>(mob);
        Assert.Multiple(() =>
        {
            Assert.That(hands.Hands.Keys, Is.EquivalentTo(new[] { "left", "right" }));
            Assert.That(hands.SortedHands, Is.EqualTo(new[] { "right", "left" }));
            Assert.That(hands.ActiveHandId, Is.Not.Null);
            Assert.That(hands.Hands.Keys.Any(id => id.StartsWith("body_part_slot_", StringComparison.Ordinal)),
                Is.False);
            Assert.That(SEntMan.GetComponent<HandOrganComponent>(organs["HandLeft"]).HandID,
                Is.EqualTo("left"));
            Assert.That(SEntMan.GetComponent<HandOrganComponent>(organs["HandRight"]).HandID,
                Is.EqualTo("right"));
        });
    }

    private void AssertCanonicalSurgerySlots(IReadOnlyDictionary<string, EntityUid> organs)
    {
        var children = new Dictionary<string, Dictionary<string, BodyPartType>>
        {
            ["Torso"] = new()
            {
                ["head"] = BodyPartType.Head,
                ["left_arm"] = BodyPartType.Arm,
                ["right_arm"] = BodyPartType.Arm,
                ["left_leg"] = BodyPartType.Leg,
                ["right_leg"] = BodyPartType.Leg,
            },
            ["Head"] = new(),
            ["ArmLeft"] = new() { ["left_hand"] = BodyPartType.Hand },
            ["ArmRight"] = new() { ["right_hand"] = BodyPartType.Hand },
            ["HandLeft"] = new(),
            ["HandRight"] = new(),
            ["LegLeft"] = new() { ["left_foot"] = BodyPartType.Foot },
            ["LegRight"] = new() { ["right_foot"] = BodyPartType.Foot },
            ["FootLeft"] = new(),
            ["FootRight"] = new(),
        };
        var organSlots = new Dictionary<string, string[]>
        {
            ["Torso"] = ["heart", "lungs", "stomach", "liver", "kidneys"],
            ["Head"] = ["brain", "eyes"],
            ["ArmLeft"] = [],
            ["ArmRight"] = [],
            ["HandLeft"] = [],
            ["HandRight"] = [],
            ["LegLeft"] = [],
            ["LegRight"] = [],
            ["FootLeft"] = [],
            ["FootRight"] = [],
        };

        foreach (var category in BodyPartMetadata.Keys)
        {
            var part = SEntMan.GetComponent<BodyPartComponent>(organs[category]);
            Assert.That(part.Children.Keys, Is.EquivalentTo(children[category].Keys), $"{category} child slots");
            foreach (var (slotId, type) in children[category])
            {
                Assert.Multiple(() =>
                {
                    Assert.That(part.Children[slotId].Id, Is.EqualTo(slotId), slotId);
                    Assert.That(part.Children[slotId].Type, Is.EqualTo(type), slotId);
                });
            }

            Assert.That(part.Organs.Keys, Is.EquivalentTo(organSlots[category]), $"{category} organ slots");
            foreach (var slotId in organSlots[category])
                Assert.That(part.Organs[slotId].Id, Is.EqualTo(slotId), slotId);
        }
    }

    private void AssertSyntheticHideableLayers(IReadOnlyDictionary<string, EntityUid> organs)
    {
        var head = SEntMan.GetComponent<VisualOrganMarkingsComponent>(organs["Head"]);
        var torso = SEntMan.GetComponent<VisualOrganMarkingsComponent>(organs["Torso"]);
        Assert.Multiple(() =>
        {
            Assert.That(head.HideableLayers,
                Is.EquivalentTo(new[] { HumanoidVisualLayers.HeadTop }));
            Assert.That(torso.HideableLayers,
                Is.EquivalentTo(new[] { HumanoidVisualLayers.UndergarmentTop }));
        });
    }

    private void AssertRoboticLimbMetadata(IReadOnlyDictionary<string, EntityUid> organs)
    {
        var expectedLayers = new Dictionary<string, (HumanoidVisualLayers Layer, string State)>
        {
            ["ArmLeft"] = (HumanoidVisualLayers.LArm, "l_arm"),
            ["ArmRight"] = (HumanoidVisualLayers.RArm, "r_arm"),
            ["HandLeft"] = (HumanoidVisualLayers.LHand, "l_hand"),
            ["HandRight"] = (HumanoidVisualLayers.RHand, "r_hand"),
            ["LegLeft"] = (HumanoidVisualLayers.LLeg, "l_leg"),
            ["LegRight"] = (HumanoidVisualLayers.RLeg, "r_leg"),
            ["FootLeft"] = (HumanoidVisualLayers.LFoot, "l_foot"),
            ["FootRight"] = (HumanoidVisualLayers.RFoot, "r_foot"),
        };

        foreach (var (category, expected) in expectedLayers)
        {
            Assert.That(SEntMan.HasComponent<CMURoboticLimbComponent>(organs[category]), Is.True, category);
            AssertRoboticVisual(organs[category], expected.Layer, expected.State);
        }
    }

    private void AssertRoboticVisual(EntityUid organ, HumanoidVisualLayers layer, string state)
    {
        var visual = SEntMan.GetComponent<VisualOrganComponent>(organ);
        Assert.Multiple(() =>
        {
            Assert.That(visual.Layer, Is.EqualTo(layer));
            Assert.That(visual.Data.RsiPath, Is.EqualTo("Mobs/DroneAndroid/parts.rsi"));
            Assert.That(visual.Data.State, Is.EqualTo(state));
        });
    }
}
