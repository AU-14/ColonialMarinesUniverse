using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Evacuation;
using Content.Shared.Maps;
using Robust.Shared.Map;

namespace Content.IntegrationTests.RMC14.Evacuation;

[TestFixture]
public sealed class EvacuationProgressRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: RMCIncompleteEvacuationProgressTest
          components:
          - type: EvacuationProgress
            progress: 25

        - type: entity
          id: RMCCompleteEvacuationProgressTest
          components:
          - type: EvacuationProgress
            progress: 100
        """;

    [Test]
    public async Task ProgressIsScopedToTheLaunchingShipsMap()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var incompleteMap = maps.CreateMap(out var incompleteMapId);
            var completeMap = maps.CreateMap(out var completeMapId);

            try
            {
                var incomplete = SEntMan.SpawnEntity(
                    "RMCIncompleteEvacuationProgressTest",
                    new MapCoordinates(Vector2.Zero, incompleteMapId));
                var complete = SEntMan.SpawnEntity(
                    "RMCCompleteEvacuationProgressTest",
                    new MapCoordinates(Vector2.Zero, completeMapId));
                var evacuation = Server.System<EvacuationSystem>();

                Assert.Multiple(() =>
                {
                    Assert.That(evacuation.GetEvacuationProgress(incomplete), Is.EqualTo(25));
                    Assert.That(evacuation.IsEvacuationComplete(incomplete), Is.False);
                    Assert.That(evacuation.GetEvacuationProgress(complete), Is.EqualTo(100));
                    Assert.That(evacuation.IsEvacuationComplete(complete), Is.True);
                });
            }
            finally
            {
                maps.DeleteMap(completeMapId);
                maps.DeleteMap(incompleteMapId);
            }
        });
    }
}
