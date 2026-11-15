using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.CMU14.Ship;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Gravity;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Atmos;

[TestFixture]
public sealed class CMUStairEnvironmentTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase(0.5f)]
    [TestCase(-0.1f)]
    public async Task UpperStairOpeningUsesStairwellAirAndGravityButHullHolesDoNot(float x)
    {
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var atmos = SEntMan.System<AtmosphereSystem>();
            var zLevels = SEntMan.System<CMUZLevelsSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var plating = new Tile(tiles["Plating"].TileId);
            var lower = maps.CreateMap(runMapInit: true);
            var upper = maps.CreateMap(runMapInit: true);
            foreach (var map in new[] { lower, upper })
            {
                var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                for (var tileX = -2; tileX <= 2; tileX++)
                for (var tileY = -2; tileY <= 2; tileY++)
                    maps.SetTile(map, grid, new Vector2i(tileX, tileY), plating);
                var gravity = SEntMan.EnsureComponent<GravityComponent>(map);
                gravity.Enabled = true;
                gravity.Inherent = true;
                SEntMan.EnsureComponent<CMUZeroGSpaceComponent>(map);
                SEntMan.EnsureComponent<GasTileOverlayComponent>(map);
                var gridAtmos = SEntMan.EnsureComponent<GridAtmosphereComponent>(map);
                atmos.RebuildGridAtmosphere((map, gridAtmos, grid));
            }

            var network = zLevels.CreateZNetwork();
            Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new() { [lower] = 0, [upper] = 1 }), Is.True);
            var upperGrid = SEntMan.GetComponent<MapGridComponent>(upper);
            var openingTile = new Vector2i((int) MathF.Floor(x), 0);
            maps.SetTile(upper, upperGrid, openingTile, Tile.Empty);
            atmos.RebuildGridAtmosphere((upper, SEntMan.GetComponent<GridAtmosphereComponent>(upper), upperGrid));
            var stairs = SEntMan.SpawnEntity("CMUMultiZStairs", new EntityCoordinates(lower, new Vector2(0.5f, 0.5f)));
            SEntMan.System<SharedTransformSystem>().SetLocalRotation(stairs, Direction.East.ToAngle());
            var human = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(upper, new Vector2(x, 0.5f)));
            var lowerAir = atmos.GetTileMixture(lower, lower, openingTile)!;

            Assert.Multiple(() =>
            {
                Assert.That(atmos.GetContainingMixture(human), Is.SameAs(lowerAir),
                    "crossing the missing upper tile must breathe the real lower stairwell air");
                Assert.That(atmos.GetTileMixture(human)!.Pressure, Is.GreaterThan(50f));
                Assert.That(SEntMan.System<SharedGravitySystem>().IsWeightless(human), Is.False,
                    "the supported stair transition remains inside the ship's gravity");
            });

            SEntMan.DeleteEntity(stairs);
            SEntMan.System<SharedGravitySystem>().RefreshWeightless(human);
            Assert.Multiple(() =>
            {
                Assert.That(atmos.GetContainingMixture(human)?.Pressure ?? 0f, Is.LessThan(1f),
                    "removing the stair leaves a real hull hole, not an artificial air source");
                Assert.That(SEntMan.System<SharedGravitySystem>().IsWeightless(human), Is.True);
            });

            SEntMan.DeleteEntity(lower);
            SEntMan.DeleteEntity(upper);
            SEntMan.DeleteEntity(network);
        });
    }
}
