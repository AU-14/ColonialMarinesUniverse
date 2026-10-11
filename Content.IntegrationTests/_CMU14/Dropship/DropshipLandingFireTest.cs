using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Dropship;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

// BUG-780: landing on a burning lz left ground fires showing up inside the lexington
[TestFixture]
public sealed class DropshipLandingFireTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase(0)]
    [TestCase(90)]
    public async Task LandingPutsOutGroundFiresUnderTheWholeHull(int degrees)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var transforms = Server.System<SharedTransformSystem>();
            for (var x = -25; x <= 25; x++)
            {
                for (var y = -25; y <= 25; y++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            Assert.That(Server.System<MapLoaderSystem>().TryLoadMap(
                new ResPath("/Maps/_RMC14/Shuttles/dynamic_gunship.yml"), out var shipMap, out var loaded,
                DeserializationOptions.Default with { InitializeMaps = true }), Is.True);
            var ship = loaded!.Single();
            var rotation = Angle.FromDegrees(degrees);
            // integer origin keeps ship tile centres on ground tile centres at both angles
            var origin = new Vector2(1, -1);

            // the nose row is the part rmc's centred box used to miss
            var tiles = maps.GetAllTiles(ship.Owner, ship.Comp).Select(t => t.GridIndices).ToList();
            var nose = tiles.OrderByDescending(t => t.Y).First();
            var tail = tiles.OrderBy(t => t.Y).First();
            Vector2 Ground(Vector2i tile) => origin + rotation.RotateVec((Vector2) tile + new Vector2(0.5f, 0.5f));

            var underNose = SEntMan.SpawnEntity("RMCTileFire", new EntityCoordinates(map.Grid, Ground(nose)));
            var underTail = SEntMan.SpawnEntity("RMCTileFire", new EntityCoordinates(map.Grid, Ground(tail)));
            var outside = SEntMan.SpawnEntity("RMCTileFire", new EntityCoordinates(map.Grid, new Vector2(20.5f, 20.5f)));

            transforms.SetCoordinates(ship.Owner, new EntityCoordinates(map.MapUid, origin));
            transforms.SetWorldRotation(ship.Owner, rotation);
            var onDeck = SEntMan.SpawnEntity("RMCTileFire",
                new EntityCoordinates(ship.Owner, (Vector2) nose + new Vector2(0.5f, 0.5f)));

            Server.System<CMUDropshipLandingFireSystem>().ExtinguishUnderHull(
                (ship.Owner, ship.Comp, SEntMan.GetComponent<TransformComponent>(ship.Owner)));

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.IsQueuedForDeletion(underNose), Is.True,
                    "ground fire under the nose must go out when the ship lands on it");
                Assert.That(SEntMan.IsQueuedForDeletion(underTail), Is.True,
                    "ground fire under the tail must go out when the ship lands on it");
                Assert.That(SEntMan.IsQueuedForDeletion(outside), Is.False,
                    "fires away from the hull are none of the landing's business");
                Assert.That(SEntMan.IsQueuedForDeletion(onDeck), Is.False,
                    "fires burning on the ship's own deck stay lit");
            });

            SEntMan.DeleteEntity(ship.Owner);
            maps.DeleteMap(shipMap!.Value.Comp.MapId);
        });
    }
}
