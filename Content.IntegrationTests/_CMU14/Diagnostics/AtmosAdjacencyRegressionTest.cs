#pragma warning disable RA0002 // Exercise the transient adjacency state recorded in the server log.
using System.Reflection;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class AtmosAdjacencyRegressionTest
{
    [TestCase(false, true, true)]
    [TestCase(true, true, true)]
    [TestCase(true, false, true)]
    [TestCase(false, true, false)]
    public async Task EqualizationDoesNotTraverseAnUnpairedTileLink(bool reciprocal, bool reverseOpen, bool neighborPresent)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var grid = map.GridCoords.EntityId;
            var air = new GasMixture(Atmospherics.CellVolume);
            air.SetMoles(Gas.Oxygen, 100);
            var giver = new TileAtmosphere(grid, Vector2i.Zero, air);
            var taker = new TileAtmosphere(grid, new Vector2i(1, 0), new GasMixture(Atmospherics.CellVolume));
            giver.AdjacentBits = AtmosDirection.East;
            if (neighborPresent)
                giver.AdjacentTiles[AtmosDirection.East.ToIndex()] = taker;
            // Flags can still be set while the reverse tile reference is absent.
            taker.AdjacentBits = reverseOpen ? AtmosDirection.West : AtmosDirection.Invalid;
            if (reciprocal)
                taker.AdjacentTiles[AtmosDirection.West.ToIndex()] = giver;
            var atmos = entities.System<AtmosphereSystem>();
            typeof(AtmosphereSystem).GetProperty("TileEqualize", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(atmos, true);
            var ent = new Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent>(grid,
                entities.EnsureComponent<GridAtmosphereComponent>(grid), entities.EnsureComponent<GasTileOverlayComponent>(grid),
                entities.GetComponent<MapGridComponent>(grid), entities.GetComponent<TransformComponent>(grid));
            typeof(AtmosphereSystem).GetMethod("EqualizePressureInZone", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(atmos, [ent, giver, 1]);
            Assert.That(giver.Air!.TotalMoles + taker.Air!.TotalMoles, Is.EqualTo(100).Within(0.01));
            var valid = reciprocal && reverseOpen && neighborPresent;
            Assert.That(taker.Air.TotalMoles, valid ? Is.GreaterThan(0) : Is.EqualTo(0));
            if (!valid)
                Assert.That(ent.Comp1.InvalidatedCoords, Does.Contain(giver.GridIndices),
                    "A broken adjacency must be scheduled for repair, not permanently ignored.");
        });
        await pair.CleanReturnAsync();
    }
}
