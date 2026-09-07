using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Humanoid;
using Content.Server.Humanoid.Components;
using Content.Server.Humanoid.Systems;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Humanoid;

[TestFixture]
[TestOf(typeof(RandomHumanoidAppearanceSystem))]
public sealed class RandomHumanoidAppearanceOwnerTest : GameTest
{
    private const string InitialName = "owner-bundle-before-name";

    [SidedDependency(Side.Server)] private HumanoidOrganAppearanceSystem _organAppearance = default!;

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  parent: RMCMobVulpkanin
  id: RandomHumanoidAppearanceOwnerTestTarget
  name: owner-bundle-before-name
  components:
  - type: HumanoidProfile
    species: Vulpkanin
    voice: UnisexSilicon
  - type: RandomHumanoidAppearance
    hair: VulpHairShort
    randomizeName: true
  - type: RandomHumanoidAppearanceWhitelisted
    allowedHairColorsHex:
    - "#112233"
    allowedEyeColorsHex:
    - "#445566"
  - type: RandomHumanoidAppearanceOrderProbe

- type: entity
  parent: RMCMobVulpkanin
  id: RandomHumanoidAppearanceEnsureValidTestTarget
  components:
  - type: RandomHumanoidAppearance
    hair: VoxHairAfro
    randomizeName: false

- type: species
  id: RandomHumanoidAppearanceFemaleTestSpecies
  name: species-name-human
  roundStart: false
  prototype: RandomHumanoidAppearanceFemaleTestTarget
  dollPrototype: RandomHumanoidAppearanceFemaleTestTarget
  skinColoration: HumanToned
  sexes: [ Female ]
  defaultSoundsBySex:
  - RMCMaleHuman
  - RMCFemaleHuman
  - RMCMaleHuman
  voices:
  - RMCMaleHuman
  - RMCFemaleHuman

- type: entity
  parent: CMMobHuman
  id: RandomHumanoidAppearanceFemaleTestTarget
  components:
  - type: HumanoidProfile
    species: RandomHumanoidAppearanceFemaleTestSpecies
  - type: RandomHumanoidAppearance
    randomizeName: false
""";

    [Test]
    public async Task OwnerBundleAppliesVisualBodyBeforeProfileVoiceAndName()
    {
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<RandomHumanoidAppearanceOrderProbeSystem>();
            var target = SEntMan.Spawn("RandomHumanoidAppearanceOwnerTestTarget");

            try
            {
                var profile = SEntMan.GetComponent<HumanoidProfileComponent>(target);
                var metadata = SEntMan.GetComponent<MetaDataComponent>(target);
                var probe = SEntMan.GetComponent<RandomHumanoidAppearanceOrderProbeComponent>(target);
                Assert.That(_organAppearance.TryGetAppearance(target, out _, out var eyeColor, out _), Is.True);
                Assert.That(_organAppearance.TryGetMarkings(
                    target,
                    HumanoidVisualLayers.Hair,
                    out _,
                    out _,
                    out var hair), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(profile.Species.Id, Is.EqualTo("Vulpkanin"));
                    Assert.That(profile.Voice.Id, Is.AnyOf("RMCMaleVulpkanin", "RMCFemaleVulpkanin"));
                    Assert.That(eyeColor, Is.EqualTo(Color.FromHex("#445566")));
                    Assert.That(hair, Has.Count.EqualTo(1));
                    Assert.That(hair[0].MarkingId.Id, Is.EqualTo("VulpHairShort"));
                    Assert.That(hair[0].MarkingColors, Is.EqualTo(new[] { Color.FromHex("#112233") }));

                    Assert.That(probe.VoiceChanges, Is.EqualTo(1));
                    Assert.That(probe.OldVoice, Is.EqualTo("UnisexSilicon"));
                    Assert.That(probe.NewVoice, Is.EqualTo(profile.Voice.Id));
                    Assert.That(probe.SpeciesAtVoice, Is.EqualTo("Vulpkanin"));
                    Assert.That(probe.ProfileVoiceAtVoice, Is.EqualTo(profile.Voice.Id));
                    Assert.That(probe.VisualBodyReadyAtVoice, Is.True,
                        "VisualBody must receive the owner profile before HumanoidProfile raises VoiceChanged");
                    Assert.That(probe.EyeColorAtVoice, Is.EqualTo(Color.FromHex("#445566")));
                    Assert.That(probe.HairAtVoice, Has.Count.EqualTo(1));
                    Assert.That(probe.HairAtVoice[0].MarkingId.Id, Is.EqualTo("VulpHairShort"));
                    Assert.That(probe.HairAtVoice[0].MarkingColors,
                        Is.EqualTo(new[] { Color.FromHex("#112233") }));
                    Assert.That(probe.NameAtVoice, Is.EqualTo(InitialName),
                        "name randomization must happen after visual/profile/voice application");
                    Assert.That(metadata.EntityName, Is.Not.EqualTo(InitialName));
                });
            }
            finally
            {
                SEntMan.DeleteEntity(target);
            }
        });
    }

    [Test]
    public async Task EnsureValidRejectsForeignFixedHairAfterRandomization()
    {
        await Server.WaitAssertion(() =>
        {
            var target = SEntMan.Spawn("RandomHumanoidAppearanceEnsureValidTestTarget");

            try
            {
                var profile = SEntMan.GetComponent<HumanoidProfileComponent>(target);
                Assert.That(profile.Species.Id, Is.EqualTo("Vulpkanin"));
                Assert.That(_organAppearance.TryGetMarkings(
                    target,
                    HumanoidVisualLayers.Hair,
                    out _,
                    out _,
                    out var hair), Is.True);
                Assert.That(hair.Select(marking => marking.MarkingId.Id),
                    Does.Not.Contain("VoxHairAfro"),
                    "EnsureValid must remove a fixed marking that is not valid for the preserved species");
            }
            finally
            {
                SEntMan.DeleteEntity(target);
            }
        });
    }

    [Test]
    public async Task FemaleRandomAppearanceHasNoFacialHairWithoutWhitelist()
    {
        await Server.WaitAssertion(() =>
        {
            var target = SEntMan.Spawn("RandomHumanoidAppearanceFemaleTestTarget");

            try
            {
                var profile = SEntMan.GetComponent<HumanoidProfileComponent>(target);
                Assert.That(profile.Sex, Is.EqualTo(Sex.Female));
                var hasFacialHair = _organAppearance.TryGetMarkings(
                    target,
                    HumanoidVisualLayers.FacialHair,
                    out _,
                    out _,
                    out var facialHair);
                Assert.That(hasFacialHair && facialHair.Count > 0, Is.False);
            }
            finally
            {
                SEntMan.DeleteEntity(target);
            }
        });
    }
}

[RegisterComponent]
public sealed partial class RandomHumanoidAppearanceOrderProbeComponent : Component
{
    public int VoiceChanges;
    public string? OldVoice;
    public string? NewVoice;
    public string? SpeciesAtVoice;
    public string? ProfileVoiceAtVoice;
    public string? NameAtVoice;
    public bool VisualBodyReadyAtVoice;
    public Color EyeColorAtVoice;
    public List<Marking> HairAtVoice = new();
}

public sealed class RandomHumanoidAppearanceOrderProbeSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RandomHumanoidAppearanceOrderProbeComponent, VoiceChangedEvent>(OnVoiceChanged);
    }

    private void OnVoiceChanged(
        Entity<RandomHumanoidAppearanceOrderProbeComponent> ent,
        ref VoiceChangedEvent args)
    {
        if (_net.IsClient)
            return;

        var profile = Comp<HumanoidProfileComponent>(ent);
        ent.Comp.VoiceChanges++;
        ent.Comp.OldVoice = args.OldVoice?.Id;
        ent.Comp.NewVoice = args.NewVoice?.Id;
        ent.Comp.SpeciesAtVoice = profile.Species.Id;
        ent.Comp.ProfileVoiceAtVoice = profile.Voice.Id;
        ent.Comp.NameAtVoice = MetaData(ent).EntityName;

        var organAppearance = EntityManager.System<HumanoidOrganAppearanceSystem>();
        var hasAppearance = organAppearance.TryGetAppearance(ent, out _, out var eyeColor, out _);
        var hasHair = organAppearance.TryGetMarkings(
            ent,
            HumanoidVisualLayers.Hair,
            out _,
            out _,
            out var hair);
        ent.Comp.VisualBodyReadyAtVoice = hasAppearance && hasHair;

        if (!ent.Comp.VisualBodyReadyAtVoice)
            return;

        ent.Comp.EyeColorAtVoice = eyeColor;
        ent.Comp.HairAtVoice = hair.ToList();
    }
}
