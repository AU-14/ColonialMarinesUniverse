using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Map;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Atmos;

[TestFixture]
public sealed class CMUTileFireReplacementTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task DifferentFireSourcesCannotStackOnOneTile()
    {
        var map = await Pair.CreateTestMap();
        EntityUid latest = default;
        EntityUid neighbor = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(map.Grid);
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            maps.SetTile(map.Grid, grid, new Vector2i(1, 0), floor);
            SSpawnAtPosition("RMCTileFire", map.GridCoords);
            SSpawnAtPosition("RMCTileFireBlue", map.GridCoords);
            latest = SSpawnAtPosition("RMCTileFireGreen", map.GridCoords);
            neighbor = SSpawnAtPosition("RMCTileFire", map.GridCoords.Offset(new Vector2(1, 0)));
        });

        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var fires = SEntMan.System<RMCMapSystem>().GetAnchoredEntitiesEnumerator<TileFireComponent>(map.GridCoords);
            var surviving = new List<EntityUid>();
            while (fires.MoveNext(out var fire))
                surviving.Add(fire);
            Assert.That(surviving, Is.EqualTo(new[] { latest }),
                "each tile must contain only the newest flame, regardless of its creation source or color");
            Assert.That(SEntMan.Deleted(neighbor), Is.False, "replacement is confined to the affected tile");
        });
    }
}
