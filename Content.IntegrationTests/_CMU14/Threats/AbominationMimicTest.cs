using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._CMU14.Threats.Mobs.Abomination;
using Content.Server._RMC14.Speech.Components;
using Content.Server.Humanoid;
using Content.Server.Mind;
using Content.Server.Polymorph.Components;
using Content.Shared._CMU14.Threats.Mobs.Abomination;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Radio.Components;
using Content.Shared.Wagging;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Threats;

[TestFixture]
[TestOf(typeof(AbominationMimicSystem))]
public sealed class AbominationMimicTest : GameTest
{
    [SidedDependency(Side.Server)] private HumanoidOrganAppearanceSystem _organAppearance = default!;

    [Test]
    public async Task NonHumanHumanoidCanBeInfectedButSynthCannot()
    {
        await Server.WaitAssertion(() =>
        {
            var infection = Server.System<AbominationInfectionSystem>();
            var assimilate = Server.System<AbominationAssimilateSystem>();
            var vulpkanin = SEntMan.Spawn("RMCMobVulpkanin");
            var synth = SEntMan.Spawn("CMMobHuman");

            try
            {
                SEntMan.EnsureComponent<SynthComponent>(synth);

                Assert.Multiple(() =>
                {
                    Assert.That(infection.TryInfect(vulpkanin), Is.True);
                    Assert.That(SEntMan.HasComponent<AbominationInfectionComponent>(vulpkanin), Is.True);
                    Assert.That(infection.TryInfect(synth), Is.False);
                    Assert.That(SEntMan.HasComponent<AbominationInfectionComponent>(synth), Is.False);
                });

                var profile = assimilate.BuildProfile(vulpkanin);
                Assert.Multiple(() =>
                {
                    Assert.That(profile.SourceProtoId, Is.Null);
                    Assert.That(profile.Appearance, Is.Not.Null);
                    Assert.That(profile.Appearance!.Species.Id, Is.EqualTo("Vulpkanin"));
                });
            }
            finally
            {
                SEntMan.DeleteEntity(vulpkanin);
                SEntMan.DeleteEntity(synth);
            }
        });
    }

    [Test]
    public async Task HumanoidDisguiseUsesFixedChassisAndRevertsWithMind()
    {
        var map = await Pair.CreateTestMap();
        EntityUid mimic = default;
        EntityUid donor = default;
        EntityUid disguised = default;
        EntityUid mind = default;
        AbominationAssimilationProfile? profile = null;

        await Server.WaitAssertion(() =>
        {
            var assimilate = Server.System<AbominationAssimilateSystem>();
            var mimicSystem = Server.System<AbominationMimicSystem>();
            var mindSystem = Server.System<MindSystem>();
            var mobState = Server.System<MobStateSystem>();

            mimic = SEntMan.SpawnEntity("AU14AbominationMimic", map.GridCoords);
            donor = SEntMan.SpawnEntity("RMCMobVulpkanin", map.GridCoords);
            SEntMan.EnsureComponent<LoadoutComponent>(donor);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<WaggingComponent>(donor), Is.True);
                Assert.That(SEntMan.HasComponent<VulpkaninAccentComponent>(donor), Is.True);
                Assert.That(SEntMan.HasComponent<LoadoutComponent>(donor), Is.True);
            });

            profile = assimilate.BuildProfile(donor);
            Assert.That(profile.Appearance, Is.Not.Null);
            Assert.That(profile.Appearance!.OrganMarkings, Is.Not.Empty);

            var mimicComponent = SEntMan.GetComponent<AbominationMimicComponent>(mimic);
            mimicComponent.AssimilatedPool = [profile];

            mind = mindSystem.CreateMind(null).Owner;
            mindSystem.TransferTo(mind, mimic);

            disguised = mimicSystem.StartDisguise((mimic, mimicComponent), profile, TimeSpan.FromMinutes(1))
                ?? throw new AssertionException("Mimic failed to enter a humanoid disguise.");

            AssertHumanoidDisguise(disguised, profile);
            Assert.That(SEntMan.GetComponent<Content.Shared.Mind.MindComponent>(mind).CurrentEntity,
                Is.EqualTo(disguised));

            mobState.ChangeMobState(disguised, MobState.Critical);
            var reverting = SEntMan.GetComponent<AbominationMimicRevertingComponent>(disguised);
            reverting.RevertAt = TimeSpan.Zero;
            mimicSystem.Update(0f);
        });

        await Pair.RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.EntityExists(disguised), Is.False);
                Assert.That(SEntMan.EntityExists(mimic), Is.True);
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(mimic).EntityPrototype?.ID,
                    Is.EqualTo("AU14AbominationMimic"));
                Assert.That(SEntMan.GetComponent<Content.Shared.Mind.MindComponent>(mind).CurrentEntity,
                    Is.EqualTo(mimic));
            });

            AssertMimicRadio(mimic, transformed: false);
            var restored = SEntMan.GetComponent<AbominationMimicComponent>(mimic);
            Assert.That(restored.AssimilatedPool, Has.Count.EqualTo(1));
            Assert.That(restored.AssimilatedPool[0].Appearance?.Species,
                Is.EqualTo(profile!.Appearance!.Species));

            SEntMan.DeleteEntity(donor);
            SEntMan.DeleteEntity(mimic);
            SEntMan.DeleteEntity(mind);
        });
    }

    [Test]
    public async Task AnimalProfileUsesSourcePrototypeDirectly()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var assimilate = Server.System<AbominationAssimilateSystem>();
            var infection = Server.System<AbominationInfectionSystem>();
            var mimicSystem = Server.System<AbominationMimicSystem>();
            var mimic = SEntMan.SpawnEntity("AU14AbominationMimic", map.GridCoords);
            var mouse = SEntMan.SpawnEntity("MobMouse", map.GridCoords);

            try
            {
                Assert.That(SEntMan.HasComponent<AbominationInfectableComponent>(mouse), Is.True);
                Assert.That(infection.TryInfect(mouse), Is.True);
                Assert.That(SEntMan.HasComponent<AbominationInfectionComponent>(mouse), Is.True);

                var profile = assimilate.BuildProfile(mouse);
                Assert.Multiple(() =>
                {
                    Assert.That(profile.SourceProtoId, Is.EqualTo("MobMouse"));
                    Assert.That(profile.Appearance, Is.Null);
                });

                var mimicComponent = SEntMan.GetComponent<AbominationMimicComponent>(mimic);
                var disguised = mimicSystem.StartDisguise((mimic, mimicComponent), profile, TimeSpan.FromMinutes(1))
                    ?? throw new AssertionException("Mimic failed to enter an animal disguise.");

                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.GetComponent<MetaDataComponent>(disguised).EntityPrototype?.ID,
                        Is.EqualTo("MobMouse"));
                    Assert.That(SEntMan.HasComponent<HumanoidProfileComponent>(disguised), Is.False);
                    Assert.That(SEntMan.HasComponent<AbominationInfectableComponent>(disguised), Is.True);
                    Assert.That(SEntMan.HasComponent<AbominationMimicComponent>(disguised), Is.True);
                    Assert.That(SEntMan.HasComponent<AbominationMimicTransformedComponent>(disguised), Is.True);
                    Assert.That(SEntMan.HasComponent<PolymorphedEntityComponent>(disguised), Is.True);
                });
                AssertMimicRadio(disguised, transformed: true);

                SEntMan.DeleteEntity(disguised);
            }
            finally
            {
                if (SEntMan.EntityExists(mouse))
                    SEntMan.DeleteEntity(mouse);
                if (SEntMan.EntityExists(mimic))
                    SEntMan.DeleteEntity(mimic);
            }
        });
    }

    private void AssertHumanoidDisguise(EntityUid disguised, AbominationAssimilationProfile profile)
    {
        var snapshot = profile.Appearance!;
        var humanoid = SEntMan.GetComponent<HumanoidProfileComponent>(disguised);

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(disguised).EntityPrototype?.ID,
                Is.EqualTo("CMMobHuman"));
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(disguised).EntityName, Is.EqualTo(profile.Name));
            Assert.That(humanoid.Species, Is.EqualTo(snapshot.Species));
            Assert.That(humanoid.Sex, Is.EqualTo(snapshot.Sex));
            Assert.That(humanoid.Gender, Is.EqualTo(snapshot.Gender));
            Assert.That(humanoid.Age, Is.EqualTo(snapshot.Age));
            Assert.That(humanoid.Voice, Is.EqualTo(snapshot.Voice));
            Assert.That(SEntMan.HasComponent<WaggingComponent>(disguised), Is.False);
            Assert.That(SEntMan.HasComponent<VulpkaninAccentComponent>(disguised), Is.False);
            Assert.That(SEntMan.HasComponent<LoadoutComponent>(disguised), Is.False);
            Assert.That(SEntMan.HasComponent<AbominationMimicComponent>(disguised), Is.True);
            Assert.That(SEntMan.HasComponent<AbominationMimicTransformedComponent>(disguised), Is.True);
            Assert.That(SEntMan.HasComponent<PolymorphedEntityComponent>(disguised), Is.True);
        });

        Assert.That(_organAppearance.TryGetAppearance(disguised, out var skin, out var eyes, out var markings),
            Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(skin, Is.EqualTo(snapshot.SkinColor));
            Assert.That(eyes, Is.EqualTo(snapshot.EyeColor));
        });
        AssertMarkingsEqual(snapshot.OrganMarkings, markings);
        AssertMimicRadio(disguised, transformed: true);
    }

    private void AssertMimicRadio(EntityUid uid, bool transformed)
    {
        var transmitter = SEntMan.GetComponent<IntrinsicRadioTransmitterComponent>(uid);
        var active = SEntMan.GetComponent<ActiveRadioComponent>(uid);
        var activeChannels = new[] { "Abomination", "AbominationMimic" };
        var transmitterChannels = transformed
            ? new[] { "MarineCommon", "Abomination", "AbominationMimic" }
            : activeChannels;

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.HasComponent<IntrinsicRadioReceiverComponent>(uid), Is.True);
            Assert.That(transmitter.Channels.Select(channel => channel.Id), Is.EquivalentTo(transmitterChannels));
            Assert.That(active.Channels.Select(channel => channel.Id), Is.EquivalentTo(activeChannels));
        });
    }

    private static void AssertMarkingsEqual(
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> expected,
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> actual)
    {
        Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys));
        foreach (var (organ, expectedLayers) in expected)
        {
            Assert.That(actual, Does.ContainKey(organ));
            Assert.That(actual[organ].Keys, Is.EquivalentTo(expectedLayers.Keys));
            foreach (var (layer, expectedMarkings) in expectedLayers)
            {
                Assert.That(actual[organ], Does.ContainKey(layer));
                Assert.That(actual[organ][layer], Is.EqualTo(expectedMarkings));
            }
        }
    }
}
