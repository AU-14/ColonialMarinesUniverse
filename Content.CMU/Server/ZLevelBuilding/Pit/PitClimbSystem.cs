// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared._RMC14.Input;
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Ghost.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.ZLevelBuilding.Pit;

public enum PitClimbResult : byte
{
    // There is no pit over the one who pressed the key.
    None,

    // Up and out, on the ground beside the pit.
    Climbed,

    // Every place around the pit is taken, so the climber comes out over the pit itself and falls back in.
    FellBack,
}

// Climbing out of a pit from below: the key of the unique action (the space bar) begins the climb of whoever stands under a pit, and it takes as long as the dig of the pit did. At
// the end they come out on a free tile beside the pit; when every tile around it is taken (a wall, furniture, a crate, another pit) they come out over the hole and fall in again.
public sealed class PitClimbSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly CMUSharedZLevelsSystem _zLevels = default!;

    // The tiles around a pit, the ones that share a side first.
    private static readonly Vector2i[] Around =
    {
        new(0, 1), new(1, 0), new(0, -1), new(-1, 0),
        new(1, 1), new(1, -1), new(-1, -1), new(-1, 1),
    };

    // Who is climbing now: the key pressed again does not begin another climb.
    private readonly HashSet<EntityUid> _climbing = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DoAfterComponent, PitClimbDoAfterEvent>(OnClimbDoAfter);

        CommandBinds.Builder
            .Bind(CMKeyFunctions.CMUniqueAction,
                InputCmdHandler.FromDelegate(session =>
                    {
                        if (session?.AttachedEntity is { } user)
                            TryStartClimb(user);
                    },
                    handle: false))
            .Register<PitClimbSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<PitClimbSystem>();
    }

    // The climb begins, if there is a pit over the one who asks: it takes as long as the dig of a pit does, and it is given up if they move or are hurt.
    public bool TryStartClimb(EntityUid user)
    {
        if (TerminatingOrDeleted(user) || HasComp<GhostComponent>(user) || _mobState.IsIncapacitated(user) || _climbing.Contains(user))
            return false;

        if (!TryFindPitAbove(user, out _, out _, out _))
            return false;

        var doAfter = new DoAfterArgs(EntityManager, user, PitDiggingToolSystem.DigDelay, new PitClimbDoAfterEvent(), user)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _climbing.Add(user);
        _popup.PopupEntity(Loc.GetString("au14-pit-climb-started"), user, user);
        return true;
    }

    private void OnClimbDoAfter(Entity<DoAfterComponent> ent, ref PitClimbDoAfterEvent args)
    {
        _climbing.Remove(args.User);

        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("au14-pit-climb-cancelled"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (args.Handled)
            return;

        args.Handled = true;
        Climb(args.User);
    }

    // The climb itself, at once: up through the pit that is over the climber (the end of the do-after, and what a test asks for).
    public PitClimbResult Climb(EntityUid user)
    {
        if (TerminatingOrDeleted(user) || HasComp<GhostComponent>(user) || _mobState.IsIncapacitated(user))
            return PitClimbResult.None;

        if (!TryFindPitAbove(user, out var grid, out var gridComp, out var pitTile))
        {
            _popup.PopupEntity(Loc.GetString("au14-pit-climb-cancelled"), user, user, PopupType.SmallCaution);
            return PitClimbResult.None;
        }

        foreach (var offset in Around)
        {
            var tile = pitTile + offset;
            if (!IsFree(grid, gridComp, tile))
                continue;

            if (!_zLevels.TryMove(user, 1, worldPosition: _map.GridTileToWorldPos(grid, gridComp, tile)))
                return PitClimbResult.None;

            _popup.PopupEntity(Loc.GetString("au14-pit-climb-out"), user, user);
            return PitClimbResult.Climbed;
        }

        // Nowhere to stand: out over the hole, and in again.
        if (!_zLevels.TryMove(user, 1, worldPosition: _map.GridTileToWorldPos(grid, gridComp, pitTile)))
            return PitClimbResult.None;

        _popup.PopupEntity(Loc.GetString("au14-pit-climb-blocked"), user, user, PopupType.SmallCaution);
        return PitClimbResult.FellBack;
    }

    // The pit on the level above that is right over the one who is standing here.
    private bool TryFindPitAbove(EntityUid user, out EntityUid grid, out MapGridComponent gridComp, out Vector2i tile)
    {
        grid = default;
        gridComp = default!;
        tile = default;

        var xform = Transform(user);
        if (xform.MapUid is not { } mapUid ||
            !TryComp<CMUZLevelMapComponent>(mapUid, out var zMap) ||
            zMap.MapAbove is not { } aboveMapUid ||
            !TryComp<MapComponent>(aboveMapUid, out var aboveMap))
        {
            return false;
        }

        var coords = new MapCoordinates(_transform.GetWorldPosition(xform), aboveMap.MapId);
        if (!_map.TryFindGridAt(coords, out var aboveGrid, out var aboveGridComp))
            return false;

        var aboveTile = _map.TileIndicesFor(aboveGrid, aboveGridComp, coords);
        var anchored = _map.GetAnchoredEntitiesEnumerator(aboveGrid, aboveGridComp, aboveTile);
        while (anchored.MoveNext(out var candidate))
        {
            if (!HasComp<PitComponent>(candidate.Value))
                continue;

            grid = aboveGrid;
            gridComp = aboveGridComp;
            tile = aboveTile;
            return true;
        }

        return false;
    }

    // A place to stand on: there is a floor, nothing a person cannot walk through is on it, and it is not another hole.
    private bool IsFree(EntityUid grid, MapGridComponent gridComp, Vector2i tile)
    {
        if (!_map.TryGetTileRef(grid, gridComp, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        if (_turf.IsTileBlocked(grid, tile, CollisionGroup.MobMask, gridComp))
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(grid, gridComp, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (HasComp<PitComponent>(ent.Value) || HasComp<CMUZLevelFallThroughComponent>(ent.Value))
                return false;
        }

        return true;
    }
}
