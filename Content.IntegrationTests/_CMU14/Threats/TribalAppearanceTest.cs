#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.CMU14.Threats.Mobs.Tribal;
using Content.Server.Humanoid;
using Content.Shared.CMU14.Threats.Mobs.Tribal;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Threats;

[TestFixture]
[TestOf(typeof(TribalAppearanceSystem))]
public sealed class TribalAppearanceTest : GameTest
{
    private static readonly HumanoidVisualLayers[] UnderwearLayers =
    [
        HumanoidVisualLayers.UndergarmentTop,
        HumanoidVisualLayers.UndergarmentBottom,
    ];

    [SidedDependency(Side.Server)] private BodySystem _body = default!;
    [SidedDependency(Side.Server)] private HumanoidOrganAppearanceSystem _organAppearance = default!;
    [SidedDependency(Side.Server)] private HumanoidProfileSystem _humanoidProfile = default!;
    [SidedDependency(Side.Server)] private SharedVisualBodySystem _visualBody = default!;

    [Test]
    public async Task PrototypeTribalAppliesAfterRandomAppearance()
    {
        await Server.WaitAssertion(() =>
        {
            var tribal = SEntMan.Spawn("AU14MobTribalSpear");

            try
            {
                AssertTribalAppearance(tribal);
            }
            finally
            {
                SEntMan.DeleteEntity(tribal);
            }
        });
    }

    [Test]
    public async Task DynamicTribalLifecyclePreservesOtherMarkings()
    {
        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.Spawn("CMMobHuman");

            try
            {
                Assert.That(_organAppearance.TryGetAppearance(human, out _, out _, out var before), Is.True);

                SEntMan.EnsureComponent<TribalComponent>(human);

                AssertTribalAppearance(human);
                Assert.That(_organAppearance.TryGetAppearance(human, out _, out _, out var after), Is.True);
                AssertOtherMarkingsEqual(before, after);

                ApplyValidatedAppearance(human);
                AssertUnderwearPresentButOccluded(human);

                Assert.That(SEntMan.RemoveComponent<TribalComponent>(human), Is.True);
                AssertUnderwearVisible(human);
            }
            finally
            {
                SEntMan.DeleteEntity(human);
            }
        });
    }

    [Test]
    public async Task TerminatingTribalSkipsLiveRemovalCleanup()
    {
        await Server.WaitAssertion(() =>
        {
            var tribal = SEntMan.Spawn("AU14MobTribalSpear");
            var hideable = SEntMan.GetComponent<HideableHumanoidLayersComponent>(tribal);
            var sawTerminatingRemoval = false;
            var underwearStayedPermanentlyOccluded = false;

            void OnComponentRemoved(RemovedComponentEventArgs args)
            {
                if (args.BaseArgs.Owner != tribal || args.BaseArgs.Component is not TribalComponent)
                    return;

                sawTerminatingRemoval = args.Terminating;
                underwearStayedPermanentlyOccluded =
                    hideable.PermanentlyHiddenLayers.Contains(HumanoidVisualLayers.UndergarmentTop) &&
                    hideable.PermanentlyHiddenLayers.Contains(HumanoidVisualLayers.UndergarmentBottom);
            }

            AssertTribalAppearance(tribal);
            SEntMan.ComponentRemoved += OnComponentRemoved;
            try
            {
                SEntMan.DeleteEntity(tribal);
            }
            finally
            {
                SEntMan.ComponentRemoved -= OnComponentRemoved;
            }

            Assert.Multiple(() =>
            {
                Assert.That(sawTerminatingRemoval, Is.True);
                Assert.That(underwearStayedPermanentlyOccluded, Is.True);
            });
        });
    }

    private void AssertTribalAppearance(EntityUid uid)
    {
        var profile = SEntMan.GetComponent<HumanoidProfileComponent>(uid);
        var hideable = SEntMan.GetComponent<HideableHumanoidLayersComponent>(uid);

        Assert.Multiple(() =>
        {
            Assert.That(profile.Species.Id, Is.EqualTo("Tribal"));
            Assert.That(hideable.PermanentlyHiddenLayers,
                Does.Contain(HumanoidVisualLayers.UndergarmentTop));
            Assert.That(hideable.PermanentlyHiddenLayers,
                Does.Contain(HumanoidVisualLayers.UndergarmentBottom));
        });

        Assert.That(_organAppearance.TryGetAppearance(uid, out var skinColor, out _, out var markings), Is.True);
        Assert.That(skinColor, Is.EqualTo(TribalAppearanceSystem.TribalSkin));
        Assert.That(markings, Is.Not.Empty);

        foreach (var layer in UnderwearLayers)
        {
            Assert.That(_organAppearance.TryGetMarkings(uid, layer, out _, out _, out var applied), Is.True);
            Assert.That(applied, Is.Empty);
        }

        Assert.That(_body.TryGetOrgansWithComponent<VisualOrganComponent>(uid, out var visualOrgans), Is.True);
        Assert.That(visualOrgans, Is.Not.Empty);
        foreach (var (_, organ) in visualOrgans)
            Assert.That(organ.Profile.SkinColor, Is.EqualTo(TribalAppearanceSystem.TribalSkin));
    }

    private void ApplyValidatedAppearance(EntityUid uid)
    {
        var humanoid = SEntMan.GetComponent<HumanoidProfileComponent>(uid);
        Assert.That(_organAppearance.TryGetAppearance(uid, out var skinColor, out var eyeColor, out var markings),
            Is.True);

        var appearance = HumanoidCharacterAppearance.EnsureValid(
            new HumanoidCharacterAppearance(eyeColor, skinColor, markings),
            humanoid.Species,
            humanoid.Sex);
        Assert.That(CountUnderwearMarkings(appearance.Markings, HumanoidVisualLayers.UndergarmentTop),
            Is.GreaterThan(0));
        Assert.That(CountUnderwearMarkings(appearance.Markings, HumanoidVisualLayers.UndergarmentBottom),
            Is.GreaterThan(0));

        var profile = HumanoidCharacterProfile.DefaultWithSpecies(humanoid.Species)
            .WithAge(humanoid.Age)
            .WithSex(humanoid.Sex)
            .WithGender(humanoid.Gender)
            .WithVoice(humanoid.Voice)
            .WithCharacterAppearance(appearance);

        _visualBody.ApplyProfileTo(uid, profile);
        _humanoidProfile.ApplyProfileTo(uid, profile);
    }

    private void AssertUnderwearPresentButOccluded(EntityUid uid)
    {
        Assert.That(_organAppearance.TryGetAppearance(uid, out _, out _, out var markings), Is.True);
        var hideable = SEntMan.GetComponent<HideableHumanoidLayersComponent>(uid);

        foreach (var layer in UnderwearLayers)
        {
            Assert.That(CountUnderwearMarkings(markings, layer), Is.GreaterThan(0));
            Assert.That(SharedHideableHumanoidLayersSystem.IsLayerOccluded(hideable, layer), Is.True);
        }
    }

    private void AssertUnderwearVisible(EntityUid uid)
    {
        Assert.That(_organAppearance.TryGetAppearance(uid, out _, out _, out var markings), Is.True);
        var hideable = SEntMan.GetComponent<HideableHumanoidLayersComponent>(uid);

        foreach (var layer in UnderwearLayers)
        {
            Assert.That(CountUnderwearMarkings(markings, layer), Is.GreaterThan(0));
            Assert.That(hideable.PermanentlyHiddenLayers, Does.Not.Contain(layer));
            Assert.That(SharedHideableHumanoidLayersSystem.IsLayerOccluded(hideable, layer), Is.False);
        }
    }

    private static int CountUnderwearMarkings(
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings,
        HumanoidVisualLayers layer)
    {
        var count = 0;
        foreach (var organMarkings in markings.Values)
            count += organMarkings.GetValueOrDefault(layer)?.Count ?? 0;

        return count;
    }

    private static void AssertOtherMarkingsEqual(
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> before,
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> after)
    {
        Assert.That(after.Keys, Is.EquivalentTo(before.Keys));

        foreach (var (organ, beforeLayers) in before)
        {
            var afterLayers = after[organ];
            foreach (var (layer, beforeMarkings) in beforeLayers)
            {
                if (IsUnderwearLayer(layer))
                    continue;

                Assert.That(afterLayers, Does.ContainKey(layer));
                var afterMarkings = afterLayers[layer];
                Assert.That(afterMarkings, Has.Count.EqualTo(beforeMarkings.Count));
                for (var i = 0; i < beforeMarkings.Count; i++)
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(afterMarkings[i].MarkingId, Is.EqualTo(beforeMarkings[i].MarkingId));
                        Assert.That(afterMarkings[i].Forced, Is.EqualTo(beforeMarkings[i].Forced));
                    });
                }
            }
        }
    }

    private static bool IsUnderwearLayer(HumanoidVisualLayers layer)
        => layer is HumanoidVisualLayers.UndergarmentTop or HumanoidVisualLayers.UndergarmentBottom;
}

#pragma warning restore RA0002
