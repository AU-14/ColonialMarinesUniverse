using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.CharacterDescription;

[TestFixture]
public sealed class CMUCharacterBuildAppearanceTest : GameTest
{
    private static readonly ProtoId<OrganCategoryPrototype> Torso = "Torso";
    private static readonly ProtoId<MarkingPrototype> BuiltMale = "CMUHumanBuiltMale";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task SavedBuildRestoresTheMatchingOverlayAndRemovesStaleOverrides()
    {
        await Server.WaitAssertion(() =>
        {
            var profile = new HumanoidCharacterProfile()
                .WithSex(Sex.Female)
                .WithBuild(BuildType.Athletic);
            profile.Appearance.Markings[Torso][HumanoidVisualLayers.Special].Clear();

            profile.EnsureValid(ServerSession!, Server.InstanceDependencyCollection);
            Assert.That(BodyMarkings(profile), Is.EqualTo(new[] { "CMUHumanBuiltFemale" }));
            Assert.That(profile.Build, Is.EqualTo(BuildType.Athletic));

            var ordinary = profile.WithBuild(BuildType.Average);
            ordinary.Appearance.Markings[Torso][HumanoidVisualLayers.Special] = [new Marking(BuiltMale, 1)];
            ordinary.EnsureValid(ServerSession!, Server.InstanceDependencyCollection);
            Assert.That(BodyMarkings(ordinary), Is.Empty);
        });
    }

    private static string[] BodyMarkings(HumanoidCharacterProfile profile)
    {
        if (!profile.Appearance.Markings.TryGetValue(Torso, out var organ) ||
            !organ.TryGetValue(HumanoidVisualLayers.Special, out var markings))
            return [];

        return markings.Select(marking => marking.MarkingId.Id).ToArray();
    }
}
