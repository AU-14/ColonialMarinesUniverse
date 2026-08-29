using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server.Fluids.Components;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Coordinates;
using Content.Shared.Decals;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using ServerDecalSystem = Content.Server.Decals.DecalSystem;

namespace Content.IntegrationTests.Tests.Fluids
{
    [TestFixture]
    [TestOf(typeof(PuddleComponent))]
    public sealed class PuddleTest : GameTest
    {
        [Test]
        public async Task TilePuddleTest()
        {
            var pair = Pair;
            var server = pair.Server;

            var testMap = await pair.CreateTestMap();

            var spillSystem = server.System<PuddleSystem>();

            await server.WaitAssertion(() =>
            {
                var solution = new Solution("Water", FixedPoint2.New(20));
                var tile = testMap.Tile;
                var gridUid = tile.GridUid;
                var (x, y) = tile.GridIndices;
                var coordinates = new EntityCoordinates(gridUid, x, y);

                Assert.That(spillSystem.TrySpillAt(coordinates, solution, out _), Is.True);
            });
        }

        [Test]
        public async Task SpaceNoPuddleTest()
        {
            var pair = Pair;
            var server = pair.Server;

            var testMap = await pair.CreateTestMap();
            var grid = testMap.Grid;

            var spillSystem = server.System<PuddleSystem>();
            var mapSystem = server.System<SharedMapSystem>();

            // Remove all tiles
            await server.WaitPost(() =>
            {
                var tiles = new List<(Vector2i GridIndices, Tile Tile)>();
                var tileEnumerator = mapSystem.GetAllTiles(grid.Owner, grid.Comp);

                foreach (var tile in tileEnumerator)
                {
                    tiles.Add((tile.GridIndices, Tile.Empty));
                }

                mapSystem.SetTiles(grid, tiles);
            });

            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                var coordinates = grid.Owner.ToCoordinates();
                var solution = new Solution("Water", FixedPoint2.New(20));

                Assert.That(spillSystem.TrySpillAt(coordinates, solution, out _), Is.False);
            });
        }

        [Test]
        public async Task BloodPuddleDecalLifecycleTest()
        {
            var pair = Pair;
            var server = pair.Server;

            var testMap = await pair.CreateTestMap();

            var puddleSystem = server.System<PuddleSystem>();
            var decalSystem = server.System<ServerDecalSystem>();

            await server.WaitAssertion(() =>
            {
                var tile = testMap.Tile;
                var gridUid = tile.GridUid;
                var (x, y) = tile.GridIndices;
                var coordinates = new EntityCoordinates(gridUid, x, y);

                Assert.That(
                    decalSystem.TryAddDecal(
                        "RMCDecalBloodFloor1",
                        coordinates,
                        out var nonCleanableId,
                        cleanable: false),
                    Is.True);

                Assert.That(
                    puddleSystem.TrySpillAt(
                        coordinates,
                        new Solution("Blood", FixedPoint2.New(20)),
                        out var puddleUid,
                        sound: false),
                    Is.True);

                var visuals = server.EntMan.GetComponent<PuddleDecalVisualsComponent>(puddleUid);
                Assert.Multiple(() =>
                {
                    Assert.That(visuals.GridUid, Is.EqualTo(gridUid));
                    Assert.That(visuals.DecalId, Is.Not.Null);
                    Assert.That(decalSystem.ContainsDecal(gridUid, nonCleanableId), Is.True);
                });

                var initialPuddleId = visuals.DecalId!.Value;
                Assert.That(decalSystem.ContainsDecal(gridUid, initialPuddleId), Is.True);

                Assert.That(puddleSystem.CleanDecalsAt(tile), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(decalSystem.ContainsDecal(gridUid, initialPuddleId), Is.False);
                    Assert.That(decalSystem.ContainsDecal(gridUid, nonCleanableId), Is.True);
                    Assert.That(visuals.DecalId, Is.Null);
                    Assert.That(visuals.GridUid, Is.Null);
                });

                Assert.That(
                    puddleSystem.TrySpillAt(
                        coordinates,
                        new Solution("Blood", FixedPoint2.New(20)),
                        out var samePuddleUid,
                        sound: false),
                    Is.True);
                Assert.That(samePuddleUid, Is.EqualTo(puddleUid));
                Assert.That(visuals.DecalId, Is.Not.Null);

                var recreatedPuddleId = visuals.DecalId!.Value;
                Assert.That(decalSystem.ContainsDecal(gridUid, recreatedPuddleId), Is.True);

                server.EntMan.DeleteEntity(puddleUid);
                Assert.Multiple(() =>
                {
                    Assert.That(decalSystem.ContainsDecal(gridUid, recreatedPuddleId), Is.False);
                    Assert.That(decalSystem.ContainsDecal(gridUid, nonCleanableId), Is.True);
                });
            });
        }
    }
}
