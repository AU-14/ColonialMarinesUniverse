using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Ship;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Ship;

/// <summary>
///     Out on the outer hull of a zero-g ship you float, even with the ship's gravity on; inside on ordinary plating
///     you don't.
/// </summary>
[TestFixture]
public sealed class CMUOuterHullZeroGTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase("CMFloorOuterHull", true)]
    [TestCase("CMFloorOuterHullSouth", true)]
    [TestCase("CMFloorOuterHullPlate", true)]
    [TestCase("Plating", false)]
    public async Task OuterHullIsZeroG(string tile, bool weightless)
    {
        EntityUid map = default;
        EntityUid mob = default;
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            map = maps.CreateMap(runMapInit: true);
            var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
            var gravity = SEntMan.EnsureComponent<GravityComponent>(map);
            gravity.Enabled = true;
            gravity.Inherent = true;
            SEntMan.EnsureComponent<CMUZeroGSpaceComponent>(map);

            var definition = Server.ResolveDependency<ITileDefinitionManager>()[tile];
            for (var x = -2; x <= 2; x++)
            for (var y = -2; y <= 2; y++)
                maps.SetTile(map, grid, new Vector2i(x, y), new Tile(definition.TileId));

            mob = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map, new Vector2(0.5f, 0.5f)));
        });

        await Server.WaitRunTicks(30);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<SharedGravitySystem>().IsWeightless(mob), Is.EqualTo(weightless),
                $"standing on {tile}");
            SEntMan.DeleteEntity(map);
        });
    }
}
