using Content.Shared.Physics;
using Robust.Shared.Physics;

namespace Content.IntegrationTests.Tests.Physics;

[TestFixture]
[TestOf(typeof(FixturesComponent))]
public sealed class DiagonalGrilleCollisionTest
{
    private static readonly string[] DiagonalGrilles =
    {
        "GrilleDiagonal",
        "ClockworkGrilleDiagonal",
    };

    [Test]
    public async Task DiagonalGrillesUseGlassCollisionLayer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var glassLayer = (int) CollisionGroup.GlassLayer;
            var opaqueLayer = (int) CollisionGroup.Opaque;

            foreach (var prototype in DiagonalGrilles)
            {
                var grille = entMan.SpawnEntity(prototype, map.GridCoords);
                var fixture = entMan.GetComponent<FixturesComponent>(grille).Fixtures["fix1"];

                Assert.Multiple(() =>
                {
                    Assert.That(fixture.CollisionLayer, Is.EqualTo(glassLayer), prototype);
                    Assert.That(fixture.CollisionLayer & opaqueLayer, Is.EqualTo(0), prototype);
                });

                entMan.DeleteEntity(grille);
            }
        });

        await pair.CleanReturnAsync();
    }
}
