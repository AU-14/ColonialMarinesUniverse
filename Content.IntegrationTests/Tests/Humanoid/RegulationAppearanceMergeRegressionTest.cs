using System.Reflection;
using Content.Client.Humanoid;
using Content.Client.Lobby.UI;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Humanoid;

[TestFixture]
[TestOf(typeof(RegulationMarkingPicker))]
[TestOf(typeof(HumanoidProfileEditor))]
public sealed class RegulationAppearanceMergeRegressionTest : GameTest
{
    private static readonly ProtoId<OrganCategoryPrototype> Head = "Head";
    private static readonly ProtoId<OrganCategoryPrototype> Torso = "Torso";

    [Test]
    public async Task PickerUsesOrganLayerGroupWhitelistDefaultsAndNamedColors()
    {
        await Client.WaitAssertion(() =>
        {
            var picker = new RegulationMarkingPicker
            {
                Layer = HumanoidVisualLayers.Hair,
                DefaultMarkingId = HairStyles.DefaultHairStyle,
                MarkingWhitelist = ["RMCHumanHairBob", "VulpHairAdhara"],
                DropdownColors = [("Copper", Color.Orange)],
            };

            string? selectedMarking = null;
            Color? selectedColor = null;
            picker.OnMarkingChanged += marking => selectedMarking = marking;
            picker.OnColorChanged += color => selectedColor = color;

            picker.UpdateData(HairStyles.DefaultHairStyle, Color.Black, "Human", Sex.Male);

            var markingList = GetField<ItemList>(picker, "_markingList");
            var ids = markingList.Select(item => (string) item.Metadata!).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(picker.Visible, Is.True);
                Assert.That(ids, Is.EquivalentTo(new[]
                {
                    HairStyles.DefaultHairStyle.Id,
                    "RMCHumanHairBob",
                }));
                Assert.That(ids, Does.Not.Contain("VulpHairAdhara"),
                    "a Hair marking from a different organ marking group must be filtered out");
                Assert.That(markingList.Single(item =>
                    Equals(item.Metadata, HairStyles.DefaultHairStyle.Id)).Selected, Is.True);
            });

            markingList.Single(item => Equals(item.Metadata, "RMCHumanHairBob")).Selected = true;
            var colorList = GetField<ItemList>(picker, "_colorList");
            Assert.Multiple(() =>
            {
                Assert.That(selectedMarking, Is.EqualTo("RMCHumanHairBob"));
                Assert.That(colorList.Visible, Is.True);
                Assert.That(colorList, Has.Count.EqualTo(1));
                Assert.That(colorList[0].Metadata, Is.EqualTo(Color.Orange));
            });

            colorList[0].Selected = true;
            Assert.That(selectedColor, Is.EqualTo(Color.Orange),
                "selecting a named color must update only the regulation color field callback");

            markingList.Single(item =>
                Equals(item.Metadata, HairStyles.DefaultHairStyle.Id)).Selected = true;
            Assert.Multiple(() =>
            {
                Assert.That(selectedMarking, Is.EqualTo(HairStyles.DefaultHairStyle.Id));
                Assert.That(colorList.Visible, Is.False,
                    "the default marking ID represents an empty regulation layer and has no color choice");
            });
        });
    }

    [Test]
    public async Task RegulationPreviewReplacesOnlyHairAndFacialHairWithDeepClones()
    {
        await Client.WaitAssertion(() =>
        {
            var markingManager = Client.ResolveDependency<MarkingManager>();
            var hair = new Marking("HumanHairAfro", 1).WithColor(Color.Red);
            var facial = new Marking("HumanFacialHairChin", 1).WithColor(Color.Blue);
            var tattoo = new Marking("TattooHiveChest", 1).WithColor(Color.Green);
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human", Sex.Male)
                .WithCharacterAppearance(new HumanoidCharacterAppearance(
                    Color.Azure,
                    Color.Bisque,
                    new()
                    {
                        [Head] = new()
                        {
                            [HumanoidVisualLayers.Hair] = [hair],
                            [HumanoidVisualLayers.FacialHair] = [facial],
                        },
                        [Torso] = new()
                        {
                            [HumanoidVisualLayers.Chest] = [tattoo],
                        },
                    },
                    "RMCHumanHairBob",
                    Color.Orange,
                    "HumanFacialHairSmallstache",
                    Color.Brown));

            var preview = HumanoidProfileEditor.GetRegulationPreviewProfile(profile, markingManager);
            Assert.Multiple(() =>
            {
                Assert.That(preview.Appearance.Markings[Head][HumanoidVisualLayers.Hair].Single().MarkingId.Id,
                    Is.EqualTo("RMCHumanHairBob"));
                Assert.That(preview.Appearance.Markings[Head][HumanoidVisualLayers.Hair].Single().MarkingColors,
                    Is.EqualTo(new[] { Color.Orange }));
                Assert.That(preview.Appearance.Markings[Head][HumanoidVisualLayers.FacialHair].Single().MarkingId.Id,
                    Is.EqualTo("HumanFacialHairSmallstache"));
                Assert.That(preview.Appearance.Markings[Head][HumanoidVisualLayers.FacialHair].Single().MarkingColors,
                    Is.EqualTo(new[] { Color.Brown }));
                Assert.That(preview.Appearance.Markings[Torso][HumanoidVisualLayers.Chest].Single().MarkingId.Id,
                    Is.EqualTo("TattooHiveChest"));
                Assert.That(preview.Appearance.Markings[Torso][HumanoidVisualLayers.Chest].Single().MarkingColors,
                    Is.EqualTo(new[] { Color.Green }));
            });

            var previewChest = preview.Appearance.Markings[Torso][HumanoidVisualLayers.Chest][0];
            preview.Appearance.Markings[Torso][HumanoidVisualLayers.Chest][0] =
                previewChest.WithColorAt(0, Color.Purple);
            Assert.Multiple(() =>
            {
                Assert.That(profile.Appearance.Markings[Torso][HumanoidVisualLayers.Chest][0].MarkingColors[0],
                    Is.EqualTo(Color.Green), "preview marking colors must be deep-cloned");
                Assert.That(profile.Appearance.Markings[Head][HumanoidVisualLayers.Hair][0].MarkingId.Id,
                    Is.EqualTo("HumanHairAfro"), "the stored profile must not be regulation-substituted");
                Assert.That(profile.Appearance.RegulationHairColor, Is.EqualTo(Color.Orange),
                    "preview generation must not mutate the separate regulation fields");
            });

            var defaults = profile.WithCharacterAppearance(
                profile.Appearance
                    .WithRegulationHairStyleName(HairStyles.DefaultHairStyle)
                    .WithRegulationFacialHairStyleName(HairStyles.DefaultFacialHairStyle));
            var defaultPreview = HumanoidProfileEditor.GetRegulationPreviewProfile(defaults, markingManager);
            Assert.Multiple(() =>
            {
                Assert.That(defaultPreview.Appearance.Markings[Head],
                    Does.Not.ContainKey(HumanoidVisualLayers.Hair));
                Assert.That(defaultPreview.Appearance.Markings[Head],
                    Does.Not.ContainKey(HumanoidVisualLayers.FacialHair));
                Assert.That(defaultPreview.Appearance.Markings[Torso][HumanoidVisualLayers.Chest].Single().MarkingId.Id,
                    Is.EqualTo("TattooHiveChest"));
                Assert.That(defaults.Appearance.Markings[Head], Does.ContainKey(HumanoidVisualLayers.Hair));
                Assert.That(defaults.Appearance.Markings[Head], Does.ContainKey(HumanoidVisualLayers.FacialHair));
            });
        });
    }

    private static T GetField<T>(object instance, string name)
        where T : class
    {
        return (T) instance.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;
    }
}
