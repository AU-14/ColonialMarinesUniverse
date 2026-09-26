using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Hijack;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Coordinates;
using Content.Shared.Destructible;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Fluids;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Hijack;

public sealed partial class ShipHijackSystem
{
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private SharedPuddleSystem _puddles = default!;
    [Dependency] private SharedGridTraversalSystem _gridTraversal = default!;

    private void PrepareGroundCrash(Entity<CMUShipHijackComponent> ship)
    {
        var planets = EntityQueryEnumerator<RMCPlanetComponent, TransformComponent>();
        while (planets.MoveNext(out _, out _, out var transform))
        {
            if (transform.MapUid is not { } map || ship.Comp.ShipMaps.Contains(map))
                continue;
            ship.Comp.CrashGroundMap = map;
            break;
        }
        if (ship.Comp.CrashGroundMap is not { } ground)
        {
            // Administrative/test maps can lack a colony. Still produce a traversable
            // crash site instead of abandoning the state machine half way through.
            ground = _maps.CreateMap(out _, runMapInit: true);
            EnsureComp<RMCPlanetComponent>(ground);
            ship.Comp.CrashGroundMap = ground;
        }
        ship.Comp.GroundMaps = _levels.GetAllNetworkMaps(ground).ToHashSet();
        var shipGrid = ship.Comp.ShipGrids.FirstOrDefault();
        if (TryComp(shipGrid, out MapGridComponent? grid))
        {
            var grids = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
            while (grids.MoveNext(out var planetUid, out var planetGrid, out var transform))
            {
                if (transform.MapUid != ground)
                    continue;
                var shipBounds = _transform.GetWorldMatrix(shipGrid).TransformBox(grid.LocalAABB);
                var planetBounds = _transform.GetWorldMatrix(planetUid).TransformBox(planetGrid.LocalAABB);
                ship.Comp.CrashOffset = planetBounds.BottomLeft + new Vector2(16, 16) - shipBounds.BottomLeft;
                break;
            }
        }
        var origin = ship.Comp.CrashOffset;
        if (grid != null)
            origin += _transform.GetWorldMatrix(shipGrid).TransformBox(grid.LocalAABB).BottomLeft;
        foreach (var x in new[] { 50f, 100f, 150f, 200f })
        {
            var coords = new EntityCoordinates(ground, origin + new Vector2(x, 50));
            QueueCellExplosion(_transform.ToMapCoordinates(coords), ship, 1200, 30, "RMCOB");
        }
    }

    private void ImpactGround(Entity<CMUShipHijackComponent> ship)
    {
        if (ship.Comp.CrashGroundMap is not { } ground || TerminatingOrDeleted(ground))
            return;

        // Z activation needs a map grid even when the hull is a separate movable grid.
        // Its empty tiles let bodies leaving a breach become direct children of the map.
        foreach (var map in ship.Comp.ShipMaps)
            EnsureComp<MapGridComponent>(map);
        EnsureComp<MapGridComponent>(ground);

        // Relocate the grids together, preserving all deck and shuttle coordinates.
        // The supplied Almayer is imported with its grid separate from its map entity.
        var movable = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        var decks = new List<(EntityUid Uid, TransformComponent Transform)>();
        while (movable.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.MapUid is { } map && ship.Comp.ShipMaps.Contains(map) && !HasComp<MapComponent>(uid))
                decks.Add((uid, transform));
        }
        foreach (var (uid, transform) in decks)
            _transform.SetWorldPosition(uid, _transform.GetWorldPosition(transform) + ship.Comp.CrashOffset);

        CreateCrashTerrain(ship, ground);
        var network = _levels.TryGetZNetwork(ground, out var existing)
            ? existing.Value : _levels.CreateZNetwork();
        if (!_levels.IsMapInNetwork(network, ground))
            _levels.TryAddMapsIntoZNetwork(network, new() { [ground] = 0 });
        var nextDepth = network.Comp.ZLevels.Keys.Max() + 1;
        var shipMaps = ship.Comp.ShipMaps.OrderBy(m => TryComp(m, out CMUZLevelMapComponent? level) ? level.Depth : 0).ToArray();
        foreach (var map in shipMaps)
            _levels.TryRemoveMapFromZNetwork(map);
        var additions = new Dictionary<EntityUid, int>();
        foreach (var map in shipMaps)
            additions[map] = nextDepth++;
        if (!_levels.TryAddMapsIntoZNetwork(network, additions))
            Log.Error("Could not connect the mainship wreck to the colony Z network.");

        ship.Comp.GroundImpacted = true;
        _shake.ShakeCamera(ShipFilter(ship), 50, 8);
        Announce(ship, "cmu-hijack-ground-impact");
        ExplodePumps(ship);
        CrackShip(ship);
        ReleaseBodiesOverBreaches(ship);
        ExplodeApcs(ship);
        Dirty(ship);
    }

    private void ReleaseBodiesOverBreaches(Entity<CMUShipHijackComponent> ship)
    {
        var query = EntityQueryEnumerator<CMUZPhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var physics, out var transform))
        {
            if (transform.Anchored || transform.MapUid is not { } map || !ship.Comp.ShipMaps.Contains(map) ||
                transform.ParentUid != transform.GridUid && transform.ParentUid != map ||
                HasComp<MapGridComponent>(uid))
                continue;
            _gridTraversal.CheckTraversal(uid, transform, map);
            _levels.WakeZPhysics((uid, physics));
        }
    }

    private void CreateCrashTerrain(Entity<CMUShipHijackComponent> ship, EntityUid ground)
    {
        Entity<MapGridComponent>? surface = null;
        var grids = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (grids.MoveNext(out var uid, out var grid, out var transform))
        {
            if (transform.MapUid == ground &&
                (surface == null || Box2.Area(grid.LocalAABB) > Box2.Area(surface.Value.Comp.LocalAABB)))
                surface = (uid, grid);
        }
        surface ??= _maps.CreateGridEntity(Transform(ground).MapID);
        var groundGrid = surface.Value;
        var groundTile = new Tile(_tiles["CMPlanetMarsDirt"].TileId);
        var plating = new Tile(_tiles["CMFloorPlating"].TileId);
        var patch = new Dictionary<Vector2i, Tile>();
        foreach (var uid in ship.Comp.ShipGrids)
        {
            if (!TryComp(uid, out MapGridComponent? grid))
                continue;
            var bounds = _transform.GetWorldMatrix(uid).TransformBox(grid.LocalAABB).Enlarged(12);
            for (var x = (int) MathF.Floor(bounds.Left); x <= MathF.Ceiling(bounds.Right); x++)
            for (var y = (int) MathF.Floor(bounds.Bottom); y <= MathF.Ceiling(bounds.Top); y++)
            {
                var index = _maps.WorldToTile(groundGrid, groundGrid.Comp, new Vector2(x + 0.5f, y + 0.5f));
                patch[index] = groundTile;
            }
            // Walkable wreck floor beneath the first deck prevents falling into walls.
            foreach (var tile in _maps.GetAllTiles(uid, grid))
            {
                var position = _transform.ToMapCoordinates(_maps.GridTileToLocal(uid, grid, tile.GridIndices)).Position;
                patch[_maps.WorldToTile(groundGrid, groundGrid.Comp, position)] = plating;
            }
        }
        foreach (var tile in patch.Keys)
        {
            foreach (var entity in _maps.GetAnchoredEntities(groundGrid, groundGrid.Comp, tile).ToArray())
            {
                if (!HasComp<MobStateComponent>(entity))
                    QueueDel(entity);
            }
        }
        _maps.SetTiles(groundGrid, groundGrid.Comp, patch.Select(p => (p.Key, p.Value)).ToList());
    }

    private void CrackShip(Entity<CMUShipHijackComponent> ship)
    {
        if (ship.Comp.CrackPositions.Count == 0)
            return;
        var crack = _random.Pick(ship.Comp.CrackPositions);
        foreach (var uid in ship.Comp.ShipGrids)
        {
            if (!TryComp(uid, out MapGridComponent? grid))
                continue;
            var tiles = _maps.GetAllTiles(uid, grid).Where(t => t.GridIndices.X == crack).ToArray();
            foreach (var tile in tiles)
            {
                if (!CrackTile(uid, grid, tile.GridIndices))
                    continue;
                foreach (var direction in new[] { -1, 1 })
                {
                    var index = tile.GridIndices;
                    do
                    {
                        index += new Vector2i(direction, 0);
                        if (!CrackTile(uid, grid, index))
                            break;
                    } while (_random.Prob(0.25f));
                }
            }
        }
    }

    private bool CrackTile(EntityUid uid, MapGridComponent grid, Vector2i index)
    {
        if (!_maps.TryGetTile(grid, index, out var tile) || tile.IsEmpty)
            return false;
        var entities = _maps.GetAnchoredEntities(uid, grid, index).ToArray();
        if (entities.Any(e => HasComp<EvacuationDoorComponent>(e) || HasComp<CMUZLevelLadderComponent>(e) ||
                              HasComp<CMUCrackImmuneComponent>(e)))
            return false;
        var coordinates = _maps.GridTileToLocal(uid, grid, index);
        foreach (var entity in entities)
        {
            if (!HasComp<MobStateComponent>(entity))
                QueueDel(entity);
        }
        if (_random.Prob(0.15f))
            _fire.SpawnFireDiamond("RMCHijackPipeFire", coordinates, 0);
        if (_random.Prob(0.15f))
            Spawn("Smoke", coordinates);
        // Loose wreckage falls onto the plating below with the crew.
        if (entities.Length > 0)
            Spawn(_random.Prob(0.5f) ? "CMShardGlass" : "CMSheetMetal1", coordinates);
        if (_random.Prob(0.05f))
        {
            foreach (var side in new[] { -1, 1 })
            {
                var edge = _maps.GridTileToLocal(uid, grid, index + new Vector2i(side, 0));
                _puddles.TrySpillAt(edge, new Solution("Water", 30), out _, sound: false);
            }
        }
        _maps.SetTile(uid, grid, index, Tile.Empty);
        return true;
    }
}
