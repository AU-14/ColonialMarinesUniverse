using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._NC14.DayNightCycle;
using Content.Shared._RMC14.Areas;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Vehicle.Jeep;

/// <summary>
/// Jeep test server helper, off unless the jeep test config turns it on (see <see cref="CMUJeepDevCVars"/>).
/// On a server without a lobby it gives joining players a job their station offers, so a test account
/// spawns as a person on any map, then parks the four jeeps and a test kit on the nearest open ground
/// around each player and brings the player next to them.
/// </summary>
public sealed class CMUJeepDevKitSystem : EntitySystem
{
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private CMUVehicleSeatSystem _seats = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private ITileDefinitionManager _tileDefs = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;

    private static readonly EntProtoId[] Jeeps =
        { "CMUVehicleJeepCargo", "CMUVehicleJeepTransport", "CMUVehicleJeepGunner", "CMUVehicleJeepMedical" };

    // What the jeep's parts take: welding, screwing and anchoring tools, a crayon, refuelling, cupola
    // ammo. The loose tools share the player's tile; the crate and the fuel tank are hard and need a
    // tile each.
    private static readonly EntProtoId[] Tools = { "CMWelder", "CMWrench", "CMScrewdriver", "CrayonBox", "CMUVehicleSprayPainter", "RMCFuelCan", "VehicleAmmoBoxCupola" };
    private static readonly EntProtoId[] Props = { "RMCCrateGreen", "RMCTankReagentFuel" };

    private const CollisionGroup Blockers = CollisionGroup.Impassable | CollisionGroup.MidImpassable |
                                            CollisionGroup.HighImpassable | CollisionGroup.LowImpassable |
                                            CollisionGroup.BarricadeImpassable | CollisionGroup.MobLayer |
                                            CollisionGroup.LargeMobLayer | CollisionGroup.MachineLayer;

    // Jeeps park facing south, three columns apart so their side exits stay clear.
    private const int Pitch = 3;
    private const int SearchRadius = 30;
    // A player further than this from their jeeps is moved next to them.
    private const int GatherDistance = 2;

    private readonly HashSet<NetUserId> _served = new();
    private bool _daylight;

    // Per search: tiles nothing stands on and tiles under open sky. Tiles taken by kits, including
    // their exits, stay taken for the round so later kits keep clear of them.
    private readonly Dictionary<Vector2i, bool> _clear = new();
    private readonly Dictionary<Vector2i, bool> _open = new();
    private readonly HashSet<(EntityUid Grid, Vector2i Tile)> _reserved = new();
    private readonly HashSet<EntityUid> _onTile = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<RulePlayerJobsAssignedEvent>(OnJobsAssigned);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent args)
    {
        if (args.Handled || args.JobId != null || _ticker.LobbyEnabled)
            return;

        var job = _cfg.GetCVar(CMUJeepDevCVars.DevJob);
        if (job.Length == 0 ||
            !_prototypes.HasIndex<JobPrototype>(job) ||
            !TryComp(args.Station, out StationJobsComponent? stationJobs) ||
            !_stationJobs.TryGetJobSlot(args.Station, job, out _, stationJobs))
        {
            return;
        }

        args.JobId = job;
    }

    /// <summary>
    /// At round start the ticker only spawns players whose own preferences got them a job, and leaves
    /// the rest waiting with no character. Spawn those as late joiners, which forces the dev job above.
    /// </summary>
    private void OnJobsAssigned(RulePlayerJobsAssignedEvent args)
    {
        if (_ticker.LobbyEnabled || _cfg.GetCVar(CMUJeepDevCVars.DevJob).Length == 0)
            return;

        foreach (var session in args.Players)
        {
            if (session.AttachedEntity == null)
                _ticker.MakeJoinGame(session, EntityUid.Invalid);
        }
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!_cfg.GetCVar(CMUJeepDevCVars.DevKit) || _ticker.LobbyEnabled || !_served.Add(args.Player.UserId))
            return;

        if (!_daylight && _cfg.GetCVar(CMUJeepDevCVars.DevDaylight))
            _daylight = SetDaylight(args.Mob);

        if (!TrySpawnKit(args.Mob, out _, out var error))
            Log.Warning($"No jeep dev kit for {args.Player.Name}: {error}");
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _served.Clear();
        _reserved.Clear();
        _daylight = false;
    }

    /// <summary>
    /// Parks the four jeeps and the test kit on the nearest open ground around a player, under open
    /// sky where there is room, and moves the player next to them if they ended up further away.
    /// </summary>
    public bool TrySpawnKit(EntityUid player, [NotNullWhen(true)] out List<EntityUid>? jeeps, [NotNullWhen(false)] out string? error)
    {
        jeeps = null;
        var xform = Transform(player);
        if (_containers.IsEntityInContainer(player))
        {
            error = "the player is inside something";
            return false;
        }

        if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? gridComp))
        {
            error = "the player is not on a grid";
            return false;
        }

        _clear.Clear();
        _open.Clear();
        var grid = new Entity<MapGridComponent>(gridUid, gridComp);
        var origin = _map.TileIndicesFor(gridUid, gridComp, xform.Coordinates);

        var placed = new List<Vector2i>();
        jeeps = new List<EntityUid>();
        foreach (var proto in Jeeps)
        {
            // Line the jeeps up beside the first one where there is room.
            Vector2i? beside = placed.Count == 0 ? null : placed[^1] + new Vector2i(Pitch, 0);
            if (!TryFindTile(grid, placed.Count == 0 ? origin : placed[0], beside, JeepTiles, out var tile))
            {
                Log.Warning($"No room for {proto} within {SearchRadius} tiles of {ToPrettyString(player)}.");
                continue;
            }

            Reserve(grid, JeepTiles(tile));
            placed.Add(tile);
            var jeep = SpawnAttachedTo(proto, _map.GridTileToLocal(gridUid, gridComp, tile));
            jeeps.Add(jeep);
            LeaveKitKey(jeep);
        }

        if (placed.Count == 0)
        {
            error = $"no open ground within {SearchRadius} tiles";
            return false;
        }

        // The player stands by the first jeep with the tools; the crate and fuel tank go on their own
        // tiles. Those look for open sky only when the jeep is under it, so they stay next to it.
        var nearOpen = IsOpen(grid, placed[0]);
        var stand = origin;
        if (TryFindTile(grid, placed[0], null, SingleTile, out var spot, nearOpen))
        {
            Reserve(grid, SingleTile(spot));
            stand = spot;
        }

        var standCoords = _map.GridTileToLocal(gridUid, gridComp, stand);
        if (Math.Max(Math.Abs(stand.X - origin.X), Math.Abs(stand.Y - origin.Y)) > GatherDistance)
        {
            _transform.SetCoordinates(player, standCoords);
            // A rider moved off their seat is unbuckled and would be put back at the seat's exit.
            _seats.CancelExit(player);
        }

        foreach (var tool in Tools)
        {
            SpawnAttachedTo(tool, standCoords);
        }

        foreach (var prop in Props)
        {
            if (!TryFindTile(grid, placed[0], null, SingleTile, out var propTile, nearOpen))
                continue;

            Reserve(grid, SingleTile(propTile));
            SpawnAttachedTo(prop, _map.GridTileToLocal(gridUid, gridComp, propTile));
        }

        Log.Info($"Jeep dev kit for {ToPrettyString(player)} on {ToPrettyString(gridUid)}: " +
                 string.Join(", ", jeeps.Select(j => $"{ToPrettyString(j)} at {Transform(j).LocalPosition}")) +
                 $"; player at {stand}.");
        error = null;
        return true;
    }

    /// <summary>
    /// A test jeep's key comes out of the ignition and lies on the ground by the driver's door, to be
    /// put in before driving off.
    /// </summary>
    private void LeaveKitKey(EntityUid jeep)
    {
        if (!TryComp(jeep, out CMUVehicleIgnitionComponent? ignition) ||
            !_itemSlots.TryEject(jeep, ignition.KeySlot, null, out var key, true))
        {
            return;
        }

        _transform.SetCoordinates(key.Value, new EntityCoordinates(jeep, new Vector2(0.95f, 0.25f)));
        _transform.AttachToGridOrMap(key.Value);
    }

    /// <summary>
    /// Tiles a south-facing jeep needs free: its body, a tile in front and behind (the gunner's exit),
    /// and the side exits of the front and rear seats.
    /// </summary>
    private static IEnumerable<Vector2i> JeepTiles(Vector2i tile)
    {
        for (var dy = -2; dy <= 2; dy++)
        {
            yield return tile + new Vector2i(0, dy);
        }

        yield return tile + new Vector2i(-1, 0);
        yield return tile + new Vector2i(1, 0);
        // The transport's rear seats step out at (+/-0.95, 0.75), in the tiles beside the rear.
        yield return tile + new Vector2i(-1, 1);
        yield return tile + new Vector2i(1, 1);
    }

    private static IEnumerable<Vector2i> SingleTile(Vector2i tile)
    {
        yield return tile;
    }

    /// <summary>
    /// The nearest tile around an anchor whose footprint is free, trying the preferred tile first and,
    /// with preferOpen, tiles under open sky before covered ones.
    /// </summary>
    private bool TryFindTile(
        Entity<MapGridComponent> grid,
        Vector2i anchor,
        Vector2i? preferred,
        Func<Vector2i, IEnumerable<Vector2i>> footprint,
        out Vector2i found,
        bool preferOpen = true)
    {
        foreach (var outdoors in preferOpen ? new[] { true, false } : new[] { false })
        {
            if (preferred is { } first && Fits(grid, footprint(first), outdoors))
            {
                found = first;
                return true;
            }

            for (var radius = 0; radius <= SearchRadius; radius++)
            {
                foreach (var tile in Ring(anchor, radius))
                {
                    if (!Fits(grid, footprint(tile), outdoors))
                        continue;

                    found = tile;
                    return true;
                }
            }
        }

        found = default;
        return false;
    }

    private static IEnumerable<Vector2i> Ring(Vector2i center, int radius)
    {
        if (radius == 0)
        {
            yield return center;
            yield break;
        }

        for (var x = -radius; x <= radius; x++)
        {
            yield return center + new Vector2i(x, -radius);
            yield return center + new Vector2i(x, radius);
        }

        for (var y = -radius + 1; y < radius; y++)
        {
            yield return center + new Vector2i(-radius, y);
            yield return center + new Vector2i(radius, y);
        }
    }

    private bool Fits(Entity<MapGridComponent> grid, IEnumerable<Vector2i> tiles, bool outdoors)
    {
        foreach (var tile in tiles)
        {
            if (_reserved.Contains((grid.Owner, tile)) || !IsClear(grid, tile) || outdoors && !IsOpen(grid, tile))
                return false;
        }

        return true;
    }

    private void Reserve(EntityUid grid, IEnumerable<Vector2i> tiles)
    {
        foreach (var tile in tiles)
        {
            _reserved.Add((grid, tile));
        }
    }

    /// <summary>
    /// A floor tile with nothing hard on it and nobody standing or lying there.
    /// </summary>
    private bool IsClear(Entity<MapGridComponent> grid, Vector2i tile)
    {
        if (_clear.TryGetValue(tile, out var clear))
            return clear;

        clear = _map.TryGetTileRef(grid, grid.Comp, tile, out var tileRef) &&
                !tileRef.Tile.IsEmpty &&
                !_turf.IsSpace(tileRef) &&
                !_turf.IsTileBlocked(grid, tile, Blockers, grid.Comp, minIntersectionArea: 0.01f);

        if (clear)
        {
            _onTile.Clear();
            _lookup.GetLocalEntitiesIntersecting(grid, tile, _onTile, gridComp: grid.Comp);
            clear = !_onTile.Any(e => HasComp<MobStateComponent>(e));
        }

        _clear[tile] = clear;
        return clear;
    }

    /// <summary>
    /// A tile under open sky: its area has no ceiling and no level above has a solid tile over it.
    /// </summary>
    private bool IsOpen(Entity<MapGridComponent> grid, Vector2i tile)
    {
        if (_open.TryGetValue(tile, out var open))
            return open;

        var coordinates = _map.GridTileToLocal(grid, grid.Comp, tile);
        open = !_areas.IsLightBlocked(grid, tile);

        var world = _transform.ToMapCoordinates(coordinates).Position;
        var level = Transform(grid).MapUid;
        while (open && TryComp(level, out CMUZLevelMapComponent? z) && z.MapAbove is { } above)
        {
            open = !HasFloor(above, world);
            level = above;
        }

        _open[tile] = open;
        return open;
    }

    private bool HasFloor(EntityUid map, Vector2 world)
    {
        var coordinates = new MapCoordinates(world, Transform(map).MapID);
        foreach (var grid in _map.GetAllGrids(coordinates.MapId))
        {
            // Glass and other see-through tiles are openings, not a roof.
            if (!CMUZLevelOpeningCache.IsOpeningTile(_map.GetTileRef(grid, coordinates).Tile, _tileDefs))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Sets every level of the player's map to midday, so the test starts in daylight.
    /// </summary>
    private bool SetDaylight(EntityUid player)
    {
        var level = Transform(player).MapUid;
        while (TryComp(level, out CMUZLevelMapComponent? z) && z.MapBelow is { } below)
        {
            level = below;
        }

        var any = false;
        while (level is { } current)
        {
            if (TryComp(current, out DayNightCycleComponent? cycle))
            {
                cycle.CurrentCycleTime = 0.5f;
                Dirty(current, cycle);
                any = true;
            }

            level = TryComp(current, out CMUZLevelMapComponent? z) ? z.MapAbove : null;
        }

        return any;
    }
}
