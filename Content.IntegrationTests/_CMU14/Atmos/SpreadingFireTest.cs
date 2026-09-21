using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Atmos;
using Content.Shared._RMC14.Atmos;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Atmos;

[TestFixture]
public sealed class SpreadingFireTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task CompetingParentsSpreadOnceWithoutInvalidatingTheFireQuery()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            // Only three floor tiles: both parents must compete for the middle one.
            for (var x = -1; x <= 1; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);

            var parents = new[]
            {
                SEntMan.SpawnEntity("RMCTileFire", new EntityCoordinates(map.Grid, -0.5f, 0.5f)),
                SEntMan.SpawnEntity("RMCTileFire", new EntityCoordinates(map.Grid, 1.5f, 0.5f)),
            };
            foreach (var parent in parents)
            {
                var spread = SEntMan.EnsureComponent<CMUSpreadingFireComponent>(parent);
                spread.Depth = 3;
                spread.NextSpread = TimeSpan.Zero;
            }

            var system = Server.System<CMUSpreadingFireSystem>();
            Assert.DoesNotThrow(() => system.Update(0));
            var fires = SEntMan.EntityQuery<TileFireComponent>().ToArray();
            Assert.That(fires, Has.Length.EqualTo(3), "The destination may only ignite once.");
            var child = fires.Single(f => !parents.Contains(f.Owner));
            var childSpread = SEntMan.GetComponent<CMUSpreadingFireComponent>(child.Owner);
            Assert.That(childSpread.Depth, Is.EqualTo(2));
            Assert.That(childSpread.NextSpread, Is.EqualTo(SGameTiming.CurTime + childSpread.SpreadEvery));
            system.Update(0);
            Assert.That(SEntMan.EntityQuery<TileFireComponent>().Count(), Is.EqualTo(3),
                "New fires must wait for their spread interval.");
        });
    }
}
