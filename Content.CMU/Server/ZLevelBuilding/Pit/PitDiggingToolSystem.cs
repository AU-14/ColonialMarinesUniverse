// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Numerics;
using Content.Server.CMU14.ZLevelBuilding;
using Content.Server.Destructible;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Electric;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Construction.Conditions;
using Content.Shared.DoAfter;
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

namespace Content.Server.CMU14.ZLevelBuilding.Pit;

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

    private EntityQuery<MapGridComponent> _gridQuery;

    // The same placement rules the z-level stairs use: a solid floor that nothing impassable stands on, on a map that allows vertical building.
    private static readonly TileNotBlocked StairTileRule = new();
    private static readonly ZBuildAllowed StairMapRule = new();

    private static readonly TimeSpan DigDelay = TimeSpan.FromSeconds(30);

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
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
            ? TryGetDigUpTarget(args.User, front, out var target, out var failReason)
            : TryGetDigTarget(args.User, front, out target, out failReason);

        if (!ok)
        {
            _popup.PopupEntity(Loc.GetString(failReason), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (!TryValidateDigTool(ent, args.User, target))
            return;

        // A digging tool on a power cell pays three charges for the dig before it begins; with a flat cell it does not begin.
        if (TryComp<ElectricToolComponent>(ent, out var electric) && !_cell.TryUseCharge(ent.Owner, electric.ChargePerUse * 3f, args.User))
        {
            _popup.PopupEntity(Loc.GetString("au14-tool-no-charge"), ent, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, args.User, DigDelay * ent.Comp.DigTimeMultiplier, new PitDigDoAfterEvent(GetNetCoordinates(target)), ent, used: ent.Owner)
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

        if (!TryGetDigTarget(args.User, coords, out var target, out var failReason))
        {
            _popup.PopupEntity(Loc.GetString(failReason), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (!TryValidateDigTool(ent, args.User, target))
            return;

        Spawn("RUMCPit", target);
    }

    private EntityCoordinates GetTileInFront(EntityUid user)
    {
        var xform = Transform(user);
        var offset = xform.LocalRotation.GetDir().ToVec();
        return xform.Coordinates.Offset(offset);
    }

    private bool TryGetDigTarget(EntityUid user, EntityCoordinates coords, out EntityCoordinates target, out string failReason)
    {
        target = default;
        failReason = "au14-pit-tool-no-tile";

        if (_transform.GetGrid(coords) is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
            return false;

        var tile = _map.TileIndicesFor(gridUid, grid, coords);
        return TryValidateTile(user, gridUid, grid, tile, anyEntity: false, out target, out failReason);
    }

    private bool TryGetDigUpTarget(EntityUid user, EntityCoordinates front, out EntityCoordinates target, out string failReason)
    {
        target = default;
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
        return TryValidateTile(user, aboveGrid, aboveGridComp, tile, anyEntity: true, out target, out failReason);
    }

    private bool TryValidateTile(EntityUid user, EntityUid gridUid, MapGridComponent grid, Vector2i tile, bool anyEntity, out EntityCoordinates target, out string failReason)
    {
        target = default;
        failReason = "au14-pit-tool-no-tile";

        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        var occupied = anyEntity
            ? _lookup.GetLocalEntitiesIntersecting(gridUid, tile, gridComp: grid).Count > 0
            : _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile).MoveNext(out _) || HasStructureOnTile(gridUid, grid, tile);

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

    private bool TryValidateDigTool(Entity<PitDiggingToolComponent> tool, EntityUid user, EntityCoordinates location)
    {
        if (_transform.GetMap(location) is not { } mapUid || _transform.GetMap(Transform(user).Coordinates) is not { } originMapUid)
            return false;

        if (!_building.IsEnabledOn(originMapUid) || !_building.IsEnabledOn(mapUid))
        {
            _popup.PopupEntity(Loc.GetString("construction-step-condition-au14-zbuild-allowed"), tool, user, PopupType.SmallCaution);
            return false;
        }

        var digTool = _building.GetEffectiveDigTool(mapUid);
        if (!_whitelist.IsWhitelistFail(digTool, tool.Owner))
            return true;

        _popup.PopupEntity(Loc.GetString("au14-pit-tool-wrong-tool"), tool, user, PopupType.SmallCaution);
        return false;
    }
}
