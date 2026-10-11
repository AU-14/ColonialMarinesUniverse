using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Atmos;
using Content.Shared.CMU14.Fire;
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
                SEntMan.SpawnEntity("AU14TileFire", new EntityCoordinates(map.Grid, -0.5f, 0.5f)),
                SEntMan.SpawnEntity("AU14TileFire", new EntityCoordinates(map.Grid, 1.5f, 0.5f)),
            };
            foreach (var parent in parents)
            {
                var spread = SEntMan.EnsureComponent<CMUSpreadingFireComponent>(parent);
                spread.Depth = 3;
                spread.NextSpread = TimeSpan.Zero;
            }

            var system = Server.System<CMUSpreadingFireSystem>();
            Assert.DoesNotThrow(() => system.Update(0));
            var fires = SEntMan.QueryEntities<TileFireComponent>().ToArray();
            Assert.That(fires, Has.Length.EqualTo(3), "The destination may only ignite once.");
            var child = fires.Single(f => !parents.Contains(f.Owner));
            var childSpread = SEntMan.GetComponent<CMUSpreadingFireComponent>(child.Owner);
            Assert.That(childSpread.Depth, Is.EqualTo(2));
            Assert.That(childSpread.NextSpread, Is.EqualTo(SGameTiming.CurTime + childSpread.SpreadEvery));
            system.Update(0);
            Assert.That(SEntMan.QueryEntities<TileFireComponent>().Count(), Is.EqualTo(3),
                "New fires must wait for their spread interval.");
        });
    }

    [TestCase("RMCTileFire")]
    [TestCase("RMCHijackPipeFire")]
    [TestCase("CMUTileFirePhoronArsonist")]
    public async Task NonAu14FiresNeverCreepEvenWithAnExplicitSpreadDepth(string prototype)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);

            var fire = SEntMan.SpawnEntity(prototype, map.GridCoords);
            var system = Server.System<CMUSpreadingFireSystem>();
            system.Update(0);
            Assert.That(SEntMan.HasComponent<CMUSpreadingFireComponent>(fire), Is.False,
                "Ordinary fire must not opt into the default spread depth.");

            var spread = SEntMan.EnsureComponent<CMUSpreadingFireComponent>(fire);
            spread.Depth = 3;
            spread.NextSpread = TimeSpan.Zero;
            system.Update(0);
            Assert.That(SEntMan.QueryEntities<TileFireComponent>().Count(), Is.EqualTo(1),
                "An explicit depth must not bypass the AU14-only spread restriction.");
        });
    }

    [TestCase("RMCTileFire", false)]
    [TestCase("RMCHijackPipeFire", false)]
    [TestCase("CMUTileFirePhoronArsonist", false)]
    [TestCase("AU14TileFire", true)]
    [TestCase("AU14SpreadTileFire", true)]
    public async Task OnlyAu14TileFiresIgniteNearbyFlammableObjects(string prototype, bool shouldIgnite)
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        await Server.WaitAssertion(() =>
        {
            Server.System<SharedMapSystem>().SetTile(map.Grid, new Vector2i(1, 0), map.Tile.Tile);
            var fire = SEntMan.SpawnEntity(prototype, map.GridCoords);
#pragma warning disable RA0002 // Keep every tested fire alive through the same propagation interval.
            SEntMan.GetComponent<TileFireComponent>(fire).Duration = TimeSpan.FromMinutes(2);
#pragma warning restore RA0002
            // Isolate the independent object-ignition path from tile creep.
            SEntMan.EnsureComponent<CMUSpreadingFireComponent>(fire).Depth = 0;
            target = SEntMan.SpawnEntity(null, new EntityCoordinates(map.Grid, 1.5f, 0.5f));
            var flammable = SEntMan.EnsureComponent<FlamabilityComponent>(target);
            flammable.Chance = 2; // Deterministic ignition after the system's 0.5 multiplier.
        });
        await Pair.RunSeconds(71);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<FlamabilityComponent>(target).OnFire, Is.EqualTo(shouldIgnite)));
    }
}
