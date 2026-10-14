using Content.IntegrationTests.Fixtures;
using Content.Shared.Body;
using Content.Shared.CMU14.Preferences;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Appearance;

[TestFixture]
public sealed class CMUBodyShapeTest : GameTest
{
    [Test]
    public async Task BuildPicksTheTorsoSprite()
    {
        await Server.WaitAssertion(() =>
        {
            var visualBody = SEntMan.System<SharedVisualBodySystem>();
            var body = SEntMan.System<Content.Shared.Body.BodySystem>();

            foreach (var (build, state) in new[]
                     {
                         (BuildType.LargeRipped, "torso_lrg_buff"),
                         (BuildType.ThinNoMuscle, "torso_sml_twig"),
                         (BuildType.AverageLean, "torso_avg_lean"),
                     })
            {
                foreach (var sex in new[] { Sex.Male, Sex.Female })
                {
                    var human = SEntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                    var profile = HumanoidCharacterProfile.DefaultWithSpecies().WithSex(sex).WithBuild(build);
                    visualBody.ApplyProfileTo(human, profile);

                    string? torso = null;
                    foreach (var organ in body.EnumerateOrgans<VisualOrganComponent>(human))
                    {
                        if (Equals(organ.Comp2.Layer, HumanoidVisualLayers.Chest))
                            torso = organ.Comp2.Data.State;
                    }

                    Assert.That(torso, Is.EqualTo(state), $"{sex} {build}");
                    SEntMan.DeleteEntity(human);
                }
            }
        });
    }

    [Test]
    public void OldBuildsMapToShapes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CMUBuildTypes.Parse("Heavyset"), Is.EqualTo(BuildType.LargeNoMuscle));
            Assert.That(CMUBuildTypes.Parse("Muscular"), Is.EqualTo(BuildType.AverageRipped));
            Assert.That(CMUBuildTypes.Parse("Average"), Is.EqualTo(BuildType.AverageLean));
            Assert.That(CMUBuildTypes.Parse("ThinRipped"), Is.EqualTo(BuildType.ThinRipped));
            Assert.That(CMUBuildTypes.Parse(""), Is.EqualTo(BuildType.AverageLean));
        });
    }
}
