using Content.Shared._RMC14.Fireman;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>Helpers the Early Warning Joe fixtures share (TEST-04).</summary>
public static class EWJoeTestHelpers
{
    /// <summary>Gives the one-tile test grid a tile <paramref name="x"/> steps east and returns its centre.</summary>
    public static EntityCoordinates TileEast(IEntityManager entities, TestMapData map, int x)
    {
        var maps = entities.System<SharedMapSystem>();
        var origin = maps.CoordinatesToTile(map.Grid.Owner, map.Grid.Comp, map.GridCoords);
        var tile = origin + new Vector2i(x, 0);
        maps.SetTile(map.Grid, tile, map.Tile.Tile);
        return maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, tile);
    }

    /// <summary>Turns <paramref name="uid"/> to face <paramref name="other"/>.</summary>
    public static void Face(IEntityManager entities, EntityUid uid, EntityUid other)
    {
        var xforms = entities.System<SharedTransformSystem>();
        var delta = xforms.GetWorldPosition(other) - xforms.GetWorldPosition(uid);
        xforms.SetWorldRotation(uid, delta.ToWorldAngle());
    }

    public static int CountShards(IEntityManager entities)
    {
        var count = 0;
        var query = entities.EntityQueryEnumerator<EWJoeCoreShardComponent>();
        while (query.MoveNext(out _, out _))
            count++;
        return count;
    }

    /// <summary>
    /// Puts <paramref name="victim"/> in the Joe's aggressive grab: a pull, then RMC's grab toggle once its
    /// cooldown has passed. The toggle is retried once, later, rather than trusting one fixed wait (TEST-05).
    /// </summary>
    public static async Task<bool> AggressiveGrab(Pair.TestPair pair, EntityUid joe, EntityUid victim)
    {
        var server = pair.Server;
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<PullingSystem>().TryStartPull(joe, victim), "pull started");
        });

        var grabbed = false;
        for (var attempt = 0; attempt < 2 && !grabbed; attempt++)
        {
            await pair.RunTicksSync((int) Math.Ceiling((attempt == 0 ? 5.5 : 3) / timing.TickPeriod.TotalSeconds));

            await server.WaitAssertion(() =>
            {
                var toggle = new RMCPullToggleEvent();
                entities.EventBus.RaiseLocalEvent(joe, ref toggle);
            });

            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                grabbed = entities.System<FiremanCarrySystem>().IsAggressivelyGrabbed(victim);
            });
        }

        return grabbed;
    }
}
