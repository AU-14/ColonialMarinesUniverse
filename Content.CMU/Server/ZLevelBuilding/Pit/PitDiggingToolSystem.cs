// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Linq;
using System.Numerics;
using Content.Server.CMU14.ZLevelBuilding;
using Content.Server.Destructible;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Electric;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Construction.Conditions;
using Content.Shared.DoAfter;
using Content.Shared.Gatherable.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.PowerCell;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ZLevelBuilding.Pit;

// Digging a pit down with a tool (shovel, pickaxe, jackhammer): checks the tool fits the layer, runs the do-after, and breaks the ground or rock.
public sealed class PitDiggingToolSystem : EntitySystem
{
    [Dependency] private readonly ZLevelBuildingSystem _building = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;

    // The same placement rules the z-level stairs use: a solid floor that nothing impassable stands on, on a map that allows vertical building.
    private static readonly TileNotBlocked StairTileRule = new();
    private static readonly ZBuildAllowed StairMapRule = new();

    private static readonly TimeSpan DigDelay = TimeSpan.FromSeconds(30);

    // What a pit opens into, on the level it leads to (the one below for a pit dug down, the one above for a pit dug up), ordered from the harmless to the forbidden.
    private enum Destination : byte
    {
        // Open ground, or no level there yet (a stone level is made when the pit is dug).
        Nothing,

        // A wall that is dug through: the rock, the black rock, the clay (anything that is mined), with its ores.
        Diggable,

        // The hard rock of the maps (RMCWallKutjevoRock and all that come from it): only a jackhammer drills a pit down into it, and nothing digs up through it.
        JackhammerRock,

        // Any other wall: a pit is not dug into it.
        Other,
    }

    private const string JackhammerRockRoot = "RMCWallKutjevoRock";
    private static readonly EntityWhitelist JackhammerOnly = new() { Tags = new() { "Jackhammer" } };

    private readonly Dictionary<string, bool> _jackhammerRock = new();

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        SubscribeLocalEvent<PitDiggingToolComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<PitDiggingToolComponent, PitDigDoAfterEvent>(OnDig);
        SubscribeLocalEvent<PitDiggingToolComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<PitDiggingToolComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var target = ent;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.DigUp ? "au14-pit-tool-mode-down" : "au14-pit-tool-mode-up"),
            Act = () =>
            {
                target.Comp.DigUp = !target.Comp.DigUp;
                _popup.PopupEntity(
                    Loc.GetString(target.Comp.DigUp ? "au14-pit-tool-mode-set-up" : "au14-pit-tool-mode-set-down"),
                    target,
                    user);
            },
            Priority = 10,
        });
    }

    private void OnUseInHand(Entity<PitDiggingToolComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (_transform.GetMap(Transform(args.User).Coordinates) is { } userMap && HasComp<VehicleInteriorLinkComponent>(userMap))
        {
            _popup.PopupEntity(Loc.GetString("au14-pit-tool-in-vehicle"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        var front = GetTileInFront(args.User);
        var digUp = ent.Comp.DigUp;

        var ok = digUp
            ? TryGetDigUpTarget(args.User, front, out var target, out var toolOverride, out var failReason)
            : TryGetDigTarget(args.User, front, out target, out toolOverride, out failReason);

        if (!ok)
        {
            _popup.PopupEntity(Loc.GetString(failReason), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (!TryValidateDigTool(ent, args.User, target, toolOverride))
            return;

        // A digging tool on a power cell pays three charges for the dig before it begins; with a flat cell it does not begin.
        if (TryComp<ElectricToolComponent>(ent, out var electric) && !_cell.TryUseCharge(ent.Owner, electric.ChargePerUse * 3f, args.User))
        {
            _popup.PopupEntity(Loc.GetString("au14-tool-no-charge"), ent, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, args.User, DigDelay * ent.Comp.DigTimeMultiplier, new PitDigDoAfterEvent(GetNetCoordinates(target), digUp), ent, used: ent.Owner)
        {
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
            BreakOnMove = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("au14-pit-tool-dig-started"), ent, args.User);
    }

    private void OnDig(Entity<PitDiggingToolComponent> ent, ref PitDigDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var coords = GetCoordinates(args.Location);

        var ok = args.Up
            ? TryGetDigUpTargetAt(args.User, coords, out var target, out var toolOverride, out var failReason)
            : TryGetDigTarget(args.User, coords, out target, out toolOverride, out failReason);

        if (!ok)
        {
            _popup.PopupEntity(Loc.GetString(failReason), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (!TryValidateDigTool(ent, args.User, target, toolOverride))
            return;

        Spawn("RUMCPit", target);
        ClearDugWalls(args.Up, target);
    }

    private EntityCoordinates GetTileInFront(EntityUid user)
    {
        var xform = Transform(user);
        var offset = xform.LocalRotation.GetDir().ToVec();
        return xform.Coordinates.Offset(offset);
    }

    private bool TryGetDigTarget(EntityUid user, EntityCoordinates coords, out EntityCoordinates target, out EntityWhitelist? toolOverride, out string failReason)
    {
        target = default;
        toolOverride = null;
        failReason = "au14-pit-tool-no-tile";

        if (_transform.GetGrid(coords) is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
            return false;

        var tile = _map.TileIndicesFor(gridUid, grid, coords);
        if (!TryValidateTile(user, gridUid, grid, tile, out target, out failReason))
            return false;

        return TryCheckDestination(false, target, out toolOverride, out failReason);
    }

    private bool TryGetDigUpTarget(EntityUid user, EntityCoordinates front, out EntityCoordinates target, out EntityWhitelist? toolOverride, out string failReason)
    {
        target = default;
        toolOverride = null;
        failReason = "au14-pit-tool-no-tile";

        if (_transform.GetMap(front) is not { } mapUid || _transform.GetGrid(front) is not { } gridUid)
            return false;

        if (_building.GetDepth(mapUid) > -1)
        {
            failReason = "au14-pit-tool-too-shallow";
            return false;
        }

        if (!TryComp<CMUZLevelMapComponent>(mapUid, out var zMap) || zMap.MapAbove == null)
        {
            failReason = "au14-pit-tool-nothing-above";
            return false;
        }

        var worldPos = _transform.ToMapCoordinates(front).Position;
        if (!_building.EnsureNeighborLevel(mapUid, 1, gridUid, worldPos, out var aboveMap, out var aboveGrid) ||
            !_gridQuery.TryComp(aboveGrid, out var aboveGridComp) ||
            !TryComp<MapComponent>(aboveMap, out var aboveMapComp))
        {
            return false;
        }

        var tile = _map.TileIndicesFor(aboveGrid, aboveGridComp, new MapCoordinates(worldPos, aboveMapComp.MapId));
        return TryValidateUpTile(user, aboveGrid, aboveGridComp, tile, out target, out toolOverride, out failReason);
    }

    // The same check for a place that is already on the level above, as the dig remembers it (the tool is not asked where the user stands again when it ends).
    private bool TryGetDigUpTargetAt(EntityUid user, EntityCoordinates aboveCoords, out EntityCoordinates target, out EntityWhitelist? toolOverride, out string failReason)
    {
        target = default;
        toolOverride = null;
        failReason = "au14-pit-tool-no-tile";

        if (_transform.GetGrid(aboveCoords) is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
            return false;

        var tile = _map.TileIndicesFor(gridUid, grid, aboveCoords);
        return TryValidateUpTile(user, gridUid, grid, tile, out target, out toolOverride, out failReason);
    }

    private bool TryValidateTile(EntityUid user, EntityUid gridUid, MapGridComponent grid, Vector2i tile, out EntityCoordinates target, out string failReason)
    {
        target = default;
        failReason = "au14-pit-tool-no-tile";

        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        var occupied = HasBlockingEntity(_map.GetAnchoredEntities(gridUid, grid, tile)) || HasStructureOnTile(gridUid, grid, tile);

        if (occupied)
        {
            failReason = "au14-pit-tool-tile-occupied";
            return false;
        }

        var location = _map.GridTileToLocal(gridUid, grid, tile);

        // The ground under and around a concrete support is held: nothing is dug there.
        if (NoDigBelow.IsHeld(EntityManager, location))
        {
            failReason = "au14-pit-tool-held";
            return false;
        }

        // Same rules as building a staircase here, plus anything a mob cannot walk through (bushes, cars, furniture).
        if (!StairMapRule.Condition(user, location, Direction.South) ||
            !StairTileRule.Condition(user, location, Direction.South) ||
            _turf.IsTileBlocked(gridUid, tile, CollisionGroup.MobMask, grid))
        {
            failReason = "au14-pit-tool-tile-blocked";
            return false;
        }

        target = location;
        return true;
    }

    // A pit dug up is made in the floor of the level above, so the place there is the one that is looked at: a rock or a clay over it is dug through, so it is not an obstacle
    // (it is looked at as the destination below), but the loose things, the furniture and the people on that tile would be buried by the pit and do stop it.
    private bool TryValidateUpTile(EntityUid user, EntityUid gridUid, MapGridComponent grid, Vector2i tile, out EntityCoordinates target, out EntityWhitelist? toolOverride, out string failReason)
    {
        target = default;
        toolOverride = null;
        failReason = "au14-pit-tool-no-tile";

        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        var location = _map.GridTileToLocal(gridUid, grid, tile);

        if (NoDigBelow.IsHeld(EntityManager, location))
        {
            failReason = "au14-pit-tool-held";
            return false;
        }

        if (!StairMapRule.Condition(user, location, Direction.South))
        {
            failReason = "au14-pit-tool-tile-blocked";
            return false;
        }

        // The walls say what they are first, so that a wall of steel is told as a wall and not as "something is there".
        if (!TryCheckDestination(true, location, out toolOverride, out failReason))
            return false;

        foreach (var ent in _lookup.GetLocalEntitiesIntersecting(gridUid, tile, gridComp: grid))
        {
            if (IsPartOfTheTerrain(ent))
                continue;

            failReason = "au14-pit-tool-tile-occupied";
            return false;
        }

        target = location;
        return true;
    }

    // What the pit opens into, and the tool it asks for because of that.
    private bool TryCheckDestination(bool digUp, EntityCoordinates target, out EntityWhitelist? toolOverride, out string failReason)
    {
        toolOverride = null;
        failReason = string.Empty;

        var destination = TryGetDestinationTile(digUp, target, out var gridUid, out var grid, out var tile)
            ? ClassifyTile(gridUid, grid, tile)
            : Destination.Nothing;

        switch (destination)
        {
            case Destination.Nothing:
            case Destination.Diggable:
                return true;

            case Destination.JackhammerRock when !digUp:
                toolOverride = JackhammerOnly;
                return true;

            default:
                failReason = digUp ? "au14-pit-tool-wall-above" : "au14-pit-tool-wall-below";
                return false;
        }
    }

    // The tile of the level the pit leads to: for a pit dug up it is the place of the pit itself (it is made in the level above), for a pit dug down it is the same place on the level below.
    private bool TryGetDestinationTile(bool digUp, EntityCoordinates target, out EntityUid gridUid, out MapGridComponent grid, out Vector2i tile)
    {
        gridUid = default;
        grid = default!;
        tile = default;

        if (digUp)
        {
            if (_transform.GetGrid(target) is not { } aboveGrid || !_gridQuery.TryComp(aboveGrid, out var aboveGridComp))
                return false;

            gridUid = aboveGrid;
            grid = aboveGridComp;
            tile = _map.TileIndicesFor(aboveGrid, aboveGridComp, target);
            return true;
        }

        if (_transform.GetMap(target) is not { } mapUid ||
            !TryComp<CMUZLevelMapComponent>(mapUid, out var zMap) ||
            zMap.MapBelow is not { } below ||
            !TryComp<MapComponent>(below, out var belowMap))
        {
            return false;
        }

        var coords = new MapCoordinates(_transform.ToMapCoordinates(target).Position, belowMap.MapId);
        if (!_map.TryFindGridAt(coords, out var belowGrid, out var belowGridComp))
            return false;

        gridUid = belowGrid;
        grid = belowGridComp;
        tile = _map.TileIndicesFor(belowGrid, belowGridComp, coords);
        return true;
    }

    private Destination ClassifyTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var result = Destination.Nothing;

        foreach (var ent in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (HasComp<AreaComponent>(ent))
                continue;

            Destination kind;
            if (HasComp<GatherableComponent>(ent))
                kind = Destination.Diggable;
            else if (IsJackhammerRock(ent))
                kind = Destination.JackhammerRock;
            else if (IsSolid(ent))
                kind = Destination.Other;
            else
                continue;

            if (kind > result)
                result = kind;
        }

        return result;
    }

    // A pit that is dug through a wall takes the wall with it: the rock is not left standing in the hole (where a stone level is made the rock under the pit goes the same way).
    private void ClearDugWalls(bool digUp, EntityCoordinates target)
    {
        if (!TryGetDestinationTile(digUp, target, out var gridUid, out var grid, out var tile))
            return;

        foreach (var ent in _map.GetAnchoredEntities(gridUid, grid, tile).ToList())
        {
            if (HasComp<GatherableComponent>(ent) || IsJackhammerRock(ent))
                QueueDel(ent);
        }
    }

    // The hard rock of the maps: the rock wall the jackhammer is meant for, and every wall that is made from it (the border of it, and so on).
    private bool IsJackhammerRock(EntityUid ent)
    {
        if (MetaData(ent).EntityPrototype is not { } proto)
            return false;

        if (_jackhammerRock.TryGetValue(proto.ID, out var known))
            return known;

        var isRock = proto.ID == JackhammerRockRoot ||
                     _proto.EnumerateParents<EntityPrototype>(proto.ID).Any(parent => parent.ID == JackhammerRockRoot);
        _jackhammerRock[proto.ID] = isRock;
        return isRock;
    }

    // Something a mob cannot walk through, as a wall is (the same test the tile uses to be called blocked).
    private bool IsSolid(EntityUid ent)
    {
        return _physicsQuery.TryComp(ent, out var physics) &&
               physics.CanCollide &&
               physics.Hard &&
               (physics.CollisionLayer & (int) CollisionGroup.Impassable) != 0;
    }

    // What is of the ground itself and is not buried by a pit: the marker of the area every mapped tile has, the rock, the walls (those are told apart by the destination check).
    private bool IsPartOfTheTerrain(EntityUid ent)
    {
        if (HasComp<AreaComponent>(ent) || HasComp<GatherableComponent>(ent) || IsJackhammerRock(ent))
            return true;

        return Transform(ent).Anchored && IsSolid(ent);
    }

    // Mapped maps carry an anchored area marker on every tile; it is bookkeeping, not an object, so it never occupies a tile.
    private bool HasBlockingEntity(IEnumerable<EntityUid> entities)
    {
        foreach (var ent in entities)
        {
            if (!HasComp<AreaComponent>(ent))
                return true;
        }

        return false;
    }

    // Loose structures such as bushes are not anchored and have no collision, but nothing should be dug under them.
    private bool HasStructureOnTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        foreach (var ent in _lookup.GetLocalEntitiesIntersecting(gridUid, tile, gridComp: grid))
        {
            if (HasComp<DestructibleComponent>(ent) && !HasComp<ItemComponent>(ent) && !HasComp<MobStateComponent>(ent))
                return true;
        }

        return false;
    }

    private bool TryValidateDigTool(Entity<PitDiggingToolComponent> tool, EntityUid user, EntityCoordinates location, EntityWhitelist? toolOverride)
    {
        if (_transform.GetMap(location) is not { } mapUid || _transform.GetMap(Transform(user).Coordinates) is not { } originMapUid)
            return false;

        if (!_building.IsEnabledOn(originMapUid) || !_building.IsEnabledOn(mapUid))
        {
            _popup.PopupEntity(Loc.GetString("construction-step-condition-au14-zbuild-allowed"), tool, user, PopupType.SmallCaution);
            return false;
        }

        // The rock of the maps asks for the jackhammer whatever the depth would ask for; anywhere else the depth says what is asked for (and a shovel
        // does not dig into the stone, see CanDigWith).
        var allowed = toolOverride != null
            ? !_whitelist.IsWhitelistFail(toolOverride, tool.Owner)
            : _building.CanDigWith(mapUid, tool.Owner);
        if (allowed)
            return true;

        _popup.PopupEntity(Loc.GetString(toolOverride != null ? "au14-pit-tool-needs-jackhammer" : "au14-pit-tool-wrong-tool"), tool, user, PopupType.SmallCaution);
        return false;
    }
}
