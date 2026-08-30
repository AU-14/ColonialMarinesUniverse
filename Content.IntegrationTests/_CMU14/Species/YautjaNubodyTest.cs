using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Humanoid;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Speech.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Species;

[TestFixture]
[TestOf(typeof(SharedVisualBodySystem))]
public sealed class YautjaNubodyTest : GameTest
{
    private static readonly Dictionary<string, string> ExternalOrgans = new()
    {
        ["Torso"] = "OrganYautjaTorso",
        ["Head"] = "OrganYautjaHead",
        ["ArmLeft"] = "OrganYautjaArmLeft",
        ["ArmRight"] = "OrganYautjaArmRight",
        ["HandLeft"] = "OrganYautjaHandLeft",
        ["HandRight"] = "OrganYautjaHandRight",
        ["LegLeft"] = "OrganYautjaLegLeft",
        ["LegRight"] = "OrganYautjaLegRight",
        ["FootLeft"] = "OrganYautjaFootLeft",
        ["FootRight"] = "OrganYautjaFootRight",
    };

    private static readonly Dictionary<string, string> InternalOrgans = new()
    {
        ["Brain"] = "OrganHumanBrain",
        ["Eyes"] = "OrganHumanEyes",
        ["Lungs"] = "OrganHumanLungs",
        ["Heart"] = "OrganHumanHeart",
        ["Stomach"] = "OrganHumanStomach",
        ["Liver"] = "OrganHumanLiver",
        ["Kidneys"] = "OrganHumanKidneys",
    };

    [SidedDependency(Side.Server)] private HumanoidOrganAppearanceSystem _organAppearance = default!;

    [Test]
    public async Task ConservativeGraphPreservesVisualsWithoutAddingMedicalOrgans()
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.Spawn("CMUMobYautja");
            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<InitialBodyComponent>(uid), Is.True);
                    Assert.That(SEntMan.HasComponent<VisualBodyComponent>(uid), Is.True);
                    Assert.That(SEntMan.HasComponent<HumanoidProfileComponent>(uid), Is.True);
                });

                var profile = SEntMan.GetComponent<HumanoidProfileComponent>(uid);
                var vocal = SEntMan.GetComponent<VocalComponent>(uid);
                Assert.Multiple(() =>
                {
                    Assert.That(profile.Species.Id, Is.EqualTo("Yautja"));
                    Assert.That(profile.Sex, Is.EqualTo(Sex.Male));
                    Assert.That(profile.Voice.Id, Is.EqualTo("CMUMaleYautja"));
                    Assert.That(vocal.EmoteSounds, Is.EqualTo(profile.Voice));
                });

                var body = SEntMan.GetComponent<BodyComponent>(uid);
                Assert.That(body.Organs, Is.Not.Null);
                var organs = body.Organs!.ContainedEntities;
                Assert.That(organs, Has.Count.EqualTo(17));

                var found = new Dictionary<string, EntityUid>();
                foreach (var organ in organs)
                {
                    var organComponent = SEntMan.GetComponent<OrganComponent>(organ);
                    Assert.That(organComponent.Category, Is.Not.Null);
                    found.Add(organComponent.Category!.Value.Id, organ);

                    Assert.Multiple(() =>
                    {
                        Assert.That(SEntMan.HasComponent<BodyPartHealthComponent>(organ), Is.False,
                            "the conservative bridge must not grant legacy part-health behavior");
                        Assert.That(SEntMan.HasComponent<OrganHealthComponent>(organ), Is.False,
                            "stock Human internals must not silently become CMU medical organs");
                    });
                }

                Assert.That(found.Keys, Is.EquivalentTo(ExternalOrgans.Keys.Concat(InternalOrgans.Keys)));
                AssertPrototypeMap(found, ExternalOrgans);
                AssertPrototypeMap(found, InternalOrgans);

                foreach (var category in ExternalOrgans.Keys)
                {
                    var visual = SEntMan.GetComponent<VisualOrganComponent>(found[category]);
                    Assert.Multiple(() =>
                    {
                        Assert.That(visual.Profile.Sex, Is.EqualTo(Sex.Male), category);
                        Assert.That(visual.Profile.SkinColor, Is.EqualTo(Color.White), category);
                    });
                }

                var head = SEntMan.GetComponent<VisualOrganComponent>(found["Head"]);
                var torso = SEntMan.GetComponent<VisualOrganComponent>(found["Torso"]);
                Assert.Multiple(() =>
                {
                    Assert.That(head.SexStateOverrides, Does.ContainKey(Sex.Male));
                    Assert.That(head.SexStateOverrides, Does.ContainKey(Sex.Female));
                    Assert.That(head.SexStateOverrides![Sex.Male], Is.EqualTo("head_m"));
                    Assert.That(head.SexStateOverrides[Sex.Female], Is.EqualTo("head_f"));
                    Assert.That(torso.SexStateOverrides![Sex.Male], Is.EqualTo("torso_m"));
                    Assert.That(torso.SexStateOverrides[Sex.Female], Is.EqualTo("torso_f"));
                });

                Assert.That(_organAppearance.TryGetMarkings(
                    uid,
                    HumanoidVisualLayers.Hair,
                    out _,
                    out _,
                    out var hair),
                    Is.True);
                Assert.That(hair, Has.Count.EqualTo(1));
                Assert.That(hair[0].MarkingId, Is.EqualTo("CMUYautjaDreadlocksStandard"));

                AssertNoAppliedMarkings(uid, HumanoidVisualLayers.UndergarmentTop);
                AssertNoAppliedMarkings(uid, HumanoidVisualLayers.UndergarmentBottom);
            }
            finally
            {
                SEntMan.DeleteEntity(uid);
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

    private void AssertNoAppliedMarkings(EntityUid uid, HumanoidVisualLayers layer)
    {
        if (!_organAppearance.TryGetMarkings(uid, layer, out _, out _, out var applied))
            return;

        Assert.That(applied, Is.Empty, layer.ToString());
    }
}
