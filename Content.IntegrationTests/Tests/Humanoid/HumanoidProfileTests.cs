using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Utility;
using Content.Shared.Body;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Speech.Components;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Humanoid;

[TestFixture]
[TestOf(typeof(HumanoidProfileSystem))]
public sealed class HumanoidProfileTests : GameTest
{
    private static readonly Robust.Shared.Prototypes.ProtoId<SpeciesPrototype> HumanPrototype = "Human";

    private static readonly EntProtoId BaseSpecies = "MobHuman";
    private static readonly ProtoId<SpeciesPrototype> SlimePerson = "SlimePerson";
    public static readonly ProtoId<EmoteSoundsPrototype> SlimeVoice = "FemaleSlime";

    [SidedDependency(Side.Server)] private BodySystem _bodySystem = default!;
    [SidedDependency(Side.Server)] private HumanoidProfileSystem _humanoidProfile = default!;
    [SidedDependency(Side.Server)] private MarkingManager _markingManager = default!;
    [SidedDependency(Side.Server)] private SharedVisualBodySystem _visualBody = default!;

    [Test]
    public async Task EnsureValidLoading()
    {
        var pair = Pair;
        var server = pair.Server;

        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            LoadDependencies(out var body, out var humanoidComponent);

            _humanoidProfile.ApplyProfileTo(body,
                new HumanoidCharacterProfile()
                .WithSex(Sex.Female)
                .WithAge(67)
                .WithGender(Gender.Neuter)
                .WithSpecies(SlimePerson)
                .WithVoice(SlimeVoice));

            var voiceComponent = SEntMan.GetComponent<VocalComponent>(body);

            Assert.That(humanoidComponent.Age, Is.EqualTo(67));
            Assert.That(humanoidComponent.Sex, Is.EqualTo(Sex.Female));
            Assert.That(humanoidComponent.Gender, Is.EqualTo(Gender.Neuter));
            Assert.That(humanoidComponent.Species, Is.EqualTo(SlimePerson));
            Assert.That(humanoidComponent.Voice, Is.EqualTo(SlimeVoice));

            Assert.That(voiceComponent.EmoteSounds, Is.EqualTo(SlimeVoice),
                "the selected profile voice overrides the species default sound bank");
        });
    }

    [Test]
    public async Task VersionOneExportPreservesForkProfileAndConvertsFlatMarkingOwnership()
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var tattoo = new Marking("TattooHiveChest", 1).WithColor(Color.Green);
            var export = new HumanoidProfileExportV1
            {
                ForkId = "CMU",
                Version = 1,
                Profile = new HumanoidCharacterProfileV1
                {
                    Name = "Version One",
                    FlavorText = "legacy profile",
                    Species = "Human",
                    Age = 42,
                    Sex = Sex.Female,
                    Gender = Gender.Neuter,
                    Appearance = new HumanoidCharacterAppearanceV1
                    {
                        HairStyleId = "HumanHairBob" /* cmu edit */,
                        HairColor = Color.Red,
                        FacialHairStyleId = "HumanFacialHairChin",
                        FacialHairColor = Color.Blue,
                        EyeColor = Color.Azure,
                        SkinColor = Color.FromHex("#f1d0b0"),
                        Markings = [tattoo],
                    },
                },
            };

            var converted = export.ToV2();
            var profile = converted.Profile;
            var markings = profile.Appearance.Markings;
            var expectedVoice = SProtoMan.Index<SpeciesPrototype>(HumanPrototype).DefaultSoundsBySex[(int)Sex.Female];

            Assert.Multiple(() =>
            {
                Assert.That(converted.ForkId, Is.EqualTo("CMU"));
                Assert.That(converted.Version, Is.EqualTo(2));
                Assert.That(profile.Name, Is.EqualTo("Version One"));
                Assert.That(profile.FlavorText, Is.EqualTo("legacy profile"));
                Assert.That(profile.Species.Id, Is.EqualTo("Human"));
                Assert.That(profile.Age, Is.EqualTo(42));
                Assert.That(profile.Sex, Is.EqualTo(Sex.Female));
                Assert.That(profile.Gender, Is.EqualTo(Gender.Neuter));
                Assert.That(profile.Voice, Is.EqualTo(expectedVoice));

                Assert.That(markings["Head"][HumanoidVisualLayers.Hair].Single().MarkingId,
                    Is.EqualTo("HumanHairBob" /* cmu edit */));
                Assert.That(markings["Head"][HumanoidVisualLayers.Hair].Single().MarkingColors,
                    Is.EqualTo(new[] { Color.Red }));
                Assert.That(markings["Head"][HumanoidVisualLayers.FacialHair].Single().MarkingId,
                    Is.EqualTo("HumanFacialHairChin"));
                Assert.That(markings["Head"][HumanoidVisualLayers.FacialHair].Single().MarkingColors,
                    Is.EqualTo(new[] { Color.Blue }));
                Assert.That(markings["Torso"][HumanoidVisualLayers.Chest].Single().MarkingId,
                    Is.EqualTo("TattooHiveChest"));
                Assert.That(markings["Torso"][HumanoidVisualLayers.Chest].Single().MarkingColors,
                    Is.EqualTo(new[] { Color.Green }));

                Assert.That(export.Profile.Appearance.Markings, Has.Count.EqualTo(1),
                    "V1 conversion must not append hair data into the imported flat list");
                Assert.That(export.Profile.Appearance.Markings.Single().MarkingId,
                    Is.EqualTo("TattooHiveChest"));
            });
        });
    }

    private void LoadDependencies(out EntityUid body, out HumanoidProfileComponent humanoidComponent)
    {
        body = SEntMan.Spawn(BaseSpecies);
        humanoidComponent = SEntMan.GetComponent<HumanoidProfileComponent>(body);
    }

}
