// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Numerics;
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.ZLevelBuilding.Pit;

// Lets a player standing under a pit hang a rope coil up into it, so that it can be climbed.
public sealed class RopeFromBelowSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly TimeSpan BuildDelay = TimeSpan.FromSeconds(12);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RopeCoilComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<RopeCoilComponent, RopeFromBelowDoAfterEvent>(OnDoAfter);
    }

    private void OnUseInHand(Entity<RopeCoilComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetPitAbove(args.User, out var pit))
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, args.User, BuildDelay, new RopeFromBelowDoAfterEvent(GetNetEntity(pit)), ent, used: ent.Owner)
        {
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
            BreakOnMove = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("au14-pit-build-started"), args.User, args.User);
    }

    private bool TryGetPitAbove(EntityUid user, out EntityUid pit)
    {
        pit = default;

        var xform = Transform(user);
        if (xform.MapUid is not { } mapUid)
            return false;

        if (!TryComp<CMUZLevelMapComponent>(mapUid, out var zMap) || zMap.MapAbove is not { } aboveMapUid)
            return false;

        if (!TryComp<MapComponent>(aboveMapUid, out var aboveMapComp))
            return false;

        var worldPos = _transform.GetWorldPosition(xform);
        var coords = new MapCoordinates(worldPos, aboveMapComp.MapId);
        if (!_map.TryFindGridAt(coords, out var aboveGrid, out var aboveGridComp))
            return false;

        var tile = _map.TileIndicesFor(aboveGrid, aboveGridComp, coords);
        var anchored = _map.GetAnchoredEntitiesEnumerator(aboveGrid, aboveGridComp, tile);
        while (anchored.MoveNext(out var candidate))
        {
            if (HasComp<PitComponent>(candidate.Value))
            {
                pit = candidate.Value;
                return true;
            }
        }

        return false;
    }

    private void OnDoAfter(Entity<RopeCoilComponent> ent, ref RopeFromBelowDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var pit = GetEntity(args.Pit);
        if (!Exists(pit) || !HasComp<PitComponent>(pit))
        {
            _popup.PopupEntity(Loc.GetString("au14-pit-build-failed"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        var pitXform = Transform(pit);
        if (pitXform.GridUid is not { } pitGrid)
            return;

        var userXform = Transform(args.User);
        if (userXform.GridUid is not { } userGrid)
            return;

        var worldPos = _transform.GetWorldPosition(pitXform);

        QueueDel(pit);
        QueueDel(ent.Owner);

        var down = SpawnAtWorld(pitGrid, worldPos, "RUMCPitRopeDown");
        var up = SpawnAtWorld(userGrid, worldPos, "RUMCPitRopeUp");

        if (down is { } downUid && up is { } upUid)
            EnsureComp<PitRopeComponent>(downUid).Other = upUid;

        _popup.PopupEntity(Loc.GetString("au14-pit-build-rope-from-below"), args.User, args.User);
    }

    private EntityUid? SpawnAtWorld(EntityUid gridUid, Vector2 worldPos, string proto)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return null;

        if (Transform(gridUid).MapUid is not { } mapUid || !TryComp<MapComponent>(mapUid, out var mapComp))
            return null;

        var tile = _map.TileIndicesFor(gridUid, grid, new MapCoordinates(worldPos, mapComp.MapId));
        return Spawn(proto, _map.GridTileToLocal(gridUid, grid, tile));
    }
}
