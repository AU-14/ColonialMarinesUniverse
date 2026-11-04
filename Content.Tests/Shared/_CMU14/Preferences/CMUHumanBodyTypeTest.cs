using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.Tests.Shared._CMU14.Preferences;

[TestFixture]
public sealed class CMUHumanBodyTypeTest
{
    private static readonly ProtoId<OrganCategoryPrototype> Torso = "Torso";

    [Test]
    public void BuildAndSexChooseTheMatchingHumanSprite()
    {
        var average = new HumanoidCharacterProfile();
        var athletic = average.WithBuild(BuildType.Athletic);
        Assert.Multiple(() =>
        {
            Assert.That(BodyMarkings(average), Is.Empty);
            Assert.That(BodyMarkings(athletic), Is.EqualTo(new[] { "CMUHumanBuiltMale" }));
            Assert.That(athletic.Build, Is.EqualTo(BuildType.Athletic));
        });

        var female = athletic.WithSex(Sex.Female);
        Assert.That(BodyMarkings(female), Is.EqualTo(new[] { "CMUHumanBuiltFemale" }));

        var muscular = female.WithBuild(BuildType.Muscular);
        Assert.That(BodyMarkings(muscular), Is.EqualTo(new[] { "CMUHumanBuiltFemale" }));
        Assert.That(muscular.Build, Is.EqualTo(BuildType.Muscular));

        var ordinary = muscular.WithBuild(BuildType.Average);
        Assert.That(BodyMarkings(ordinary), Is.Empty);
        Assert.That(BodyMarkings(muscular), Is.EqualTo(new[] { "CMUHumanBuiltFemale" }),
            "changing Build must not mutate the earlier profile");
    }

    [Test]
    public void NonHumanSpeciesCannotKeepTheHumanBuildSprite()
    {
        var human = new HumanoidCharacterProfile().WithBuild(BuildType.Muscular);
        var other = human.WithSpecies("Yautja");

        Assert.That(BodyMarkings(other), Is.Empty);
        Assert.That(BodyMarkings(human), Is.EqualTo(new[] { "CMUHumanBuiltMale" }));
    }

    private static string[] BodyMarkings(HumanoidCharacterProfile profile)
    {
        if (!profile.Appearance.Markings.TryGetValue(Torso, out var organ) ||
            !organ.TryGetValue(HumanoidVisualLayers.Special, out var markings))
            return [];

        return markings.Select(marking => marking.MarkingId.Id).ToArray();
    }
}
