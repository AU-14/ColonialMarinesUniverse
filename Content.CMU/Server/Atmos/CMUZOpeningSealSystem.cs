using System.Numerics;
using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Maps;
using Content.Server.CMU14.ZLevels.Core;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Atmos;

// CMU14: seals deck-to-deck atmos openings (stairwells, indoor dropship bay
// pads, look-down openings) when the tile column through them ends in a real
// floor on either end. The engine then holds those tiles as normal air
// instead of draining the surrounding room into the deck's vacuum reservoir.
// Columns with no real floor above or below them stay open to space.
public sealed class CMUZOpeningSealSystem : EntitySystem
{
    // No ship stacks this many decks over a single opening; the cap keeps a
    // corrupt map chain from walking forever.
    private const int MaxColumnDepth = 3;

    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private EntityQuery<CMUZLevelMapComponent> _zMapQuery;
    private EntityQuery<GridAtmosphereComponent> _gridAtmosQuery;

    private readonly Dictionary<EntityUid, HashSet<Vector2i>> _sealed = new();

    public override void Initialize()
    {
        _zMapQuery = GetEntityQuery<CMUZLevelMapComponent>();
        _gridAtmosQuery = GetEntityQuery<GridAtmosphereComponent>();

        SubscribeLocalEvent<CMUZOpeningSealCheckEvent>(OnSealCheck);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<CMUZLevelNetworkUpdatedEvent>(OnNetworkUpdated);
        SubscribeLocalEvent<EntParentChangedMessage>(OnGridParentChanged);
        SubscribeLocalEvent<CMUZLevelMapLeftNetworkEvent>(OnZLevelMapLeftNetwork);
        SubscribeLocalEvent<MapGridComponent, EntityTerminatingEvent>(OnGridTerminating);
    }

    private void OnSealCheck(ref CMUZOpeningSealCheckEvent ev)
    {
        if (ev.Sealed)
            return;

        if (IsAirtightSealed(ev.Grid, ev.Indices))
        {
            ev.Sealed = true;
            Track(ev.Grid, ev.Indices);
            return;
        }

        var mapUid = Transform(ev.Grid).MapUid;
        if (mapUid is not { } map
            || !_zMapQuery.TryComp(map, out var zMap))
        {
            Untrack(ev.Grid, ev.Indices);
            return;
        }

        var grid = Comp<MapGridComponent>(ev.Grid);
        var world = _map.GridTileToWorld(ev.Grid, grid, ev.Indices).Position;

        if (TryScanColumn((map, zMap), world, up: false, out var seed)
            || TryScanColumn((map, zMap), world, up: true, out seed))
        {
            ev.Sealed = true;

            if (seed == null || seed.TotalMoles < Atmospherics.GasMinMoles)
            {
                seed = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
                seed.AdjustMoles(Gas.Oxygen, Atmospherics.OxygenMolesStandard);
                seed.AdjustMoles(Gas.Nitrogen, Atmospherics.NitrogenMolesStandard);
            }

            ev.Seed = seed;
        }

        if (!_sealed.TryGetValue(ev.Grid, out var tiles))
        {
            if (!ev.Sealed)
                return;

            _sealed[ev.Grid] = tiles = new HashSet<Vector2i>();
        }

        if (ev.Sealed)
            tiles.Add(ev.Indices);
        else
            tiles.Remove(ev.Indices);
    }

    private void Untrack(EntityUid grid, Vector2i indices)
    {
        if (_sealed.TryGetValue(grid, out var tiles))
            tiles.Remove(indices);
    }

    private void Track(EntityUid grid, Vector2i indices)
    {
        if (!_sealed.TryGetValue(grid, out var tiles))
            _sealed[grid] = tiles = new HashSet<Vector2i>();

        tiles.Add(indices);
    }

    private bool IsAirtightSealed(EntityUid grid, Vector2i indices)
    {
        if (!_gridAtmosQuery.TryComp(grid, out var atmos)
            || !atmos.Tiles.TryGetValue(indices, out var tile))
            return false;

        var data = tile.AirtightData;
        return data.BlockedDirections == AtmosDirection.All
            && !data.NoAirWhenBlocked;
    }

    // Walks the deck column away from the opening. Empty and isSpace tiles are
    // see-through and the scan continues past them; a real floor terminates it
    // and yields a seed mixture cloned from that floor so the opening joins
    // the interior air volume instead of starting as a vacuum pocket.
    private bool TryScanColumn(Entity<CMUZLevelMapComponent> startMap, Vector2 world, bool up, out GasMixture? seed)
    {
        seed = null;

        var current = startMap;
        for (var depth = 0; depth < MaxColumnDepth; depth++)
        {
            if (up)
            {
                if (!_zLevels.TryMapUp((current.Owner, current.Comp), out var above))
                    return false;
                current = above.Value;
            }
            else
            {
                if (!_zLevels.TryMapDown((current.Owner, current.Comp), out var below))
                    return false;
                current = below.Value;
            }

            if (!_map.TryFindGridAt(current.Owner, world, out var gridUid, out var grid))
                continue;

            var indices = _map.WorldToTile(gridUid, grid, world);
            if (!_map.TryGetTileRef(gridUid, grid, indices, out var tileRef))
                continue;

            var tile = tileRef.Tile;
            if (tile.IsEmpty)
                continue;

            if (((ContentTileDefinition) _tileDefs[tile.TypeId]).MapAtmosphere)
                continue;

            seed = _atmosphere.GetTileMixture(gridUid, current.Owner, indices)?.Clone();
            return true;
        }

        return false;
    }

    // A tile change on any deck can open or close the shaft column under or
    // over openings on neighboring decks, so those re-run their seal decision.
    private void OnTileChanged(ref TileChangedEvent args)
    {
        var ent = args.Entity;
        var mapUid = Transform(ent.Owner).MapUid;
        if (mapUid is not { } map
            || !_zMapQuery.TryComp(map, out var zMap))
            return;

        foreach (var change in args.Changes)
        {
            var world = _map.GridTileToWorld(ent.Owner, ent.Comp, change.GridIndices).Position;
            InvalidateAlongColumn((map, zMap), world, up: true);
            InvalidateAlongColumn((map, zMap), world, up: false);
        }
    }

    private void InvalidateAlongColumn(Entity<CMUZLevelMapComponent> startMap, Vector2 world, bool up)
    {
        var current = startMap;
        for (var depth = 0; depth < MaxColumnDepth; depth++)
        {
            if (up)
            {
                if (!_zLevels.TryMapUp((current.Owner, current.Comp), out var above))
                    return;
                current = above.Value;
            }
            else
            {
                if (!_zLevels.TryMapDown((current.Owner, current.Comp), out var below))
                    return;
                current = below.Value;
            }

            if (!_map.TryFindGridAt(current.Owner, world, out var gridUid, out var grid)
                || !_gridAtmosQuery.TryComp(gridUid, out var atmos))
                continue;

            var indices = _map.WorldToTile(gridUid, grid, world);

            if (!_atmosphere.HasTileAtmosphere((gridUid, atmos), indices))
                continue;

            _atmosphere.InvalidateTile((gridUid, atmos), indices);
        }
    }

    // Networks assemble after the maps load, so openings evaluated before
    // membership re-run their decision once the deck column exists.
    private void OnNetworkUpdated(ref CMUZLevelNetworkUpdatedEvent ev)
    {
        foreach (var member in ev.Network.Comp.ZLevels.Values)
        {
            if (member is not { } map)
                continue;

            var query = EntityQueryEnumerator<GridAtmosphereComponent, TransformComponent>();
            while (query.MoveNext(out var grid, out var atmos, out var xform))
            {
                if (xform.MapUid != map)
                    continue;

                foreach (var (indices, tile) in atmos.Tiles)
                {
                    if (tile.NoGridTile || !tile.MapAtmosphere)
                        continue;

                    _atmosphere.InvalidateTile((grid, atmos), indices);
                }
            }
        }
    }

    // A grid changing maps (dropship docking, undocking) silently keeps its
    // sealed tiles: the engine's parent-change refresh only resets MapTiles,
    // which sealed tiles are no longer part of.
    private void OnGridParentChanged(ref EntParentChangedMessage args)
    {
        if (!_gridAtmosQuery.TryComp(args.Entity, out var atmos)
            || !_sealed.TryGetValue(args.Entity, out var tiles)
            || tiles.Count == 0)
            return;

        foreach (var indices in tiles)
            _atmosphere.InvalidateTile((args.Entity, atmos), indices);
    }

    // The map left the z-network: its sealed openings must re-decide as plain
    // map-atmosphere tiles (they will, since the seal check needs zMap).
    private void OnZLevelMapLeftNetwork(ref CMUZLevelMapLeftNetworkEvent ev)
    {
        foreach (var (gridUid, tiles) in _sealed)
        {
            if (!TryComp(gridUid, out TransformComponent? xform) || xform.MapUid != ev.Map)
                continue;

            foreach (var indices in tiles)
                _atmosphere.InvalidateTile(gridUid, indices);
        }
    }

    private void OnGridTerminating(Entity<MapGridComponent> ent, ref EntityTerminatingEvent args)
        => _sealed.Remove(ent.Owner);
}
