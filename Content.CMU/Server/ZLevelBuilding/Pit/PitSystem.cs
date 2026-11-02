// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Ghost.Components;
using System.Numerics;
using Content.Server.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.Insurgency.Sapper;
using Content.Shared.CMU14.ZLevelBuilding.Pit;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.ZLevelBuilding.Pit;

public sealed class PitSystem : SharedPitSystem
{
    [Dependency] private readonly ZLevelBuildingSystem _building = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly MinePlateSystem _plates = default!;

    private EntityQuery<MapGridComponent> _gridQuery;

    private static readonly TimeSpan BuildDelay = TimeSpan.FromSeconds(12);
    public const string MetalStackType = "CMSteel"; // RUMC: public, the ladders that are taken down give the metal back
    private const string RopeCoilProto = "RUMCRopeCoil";

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        SubscribeLocalEvent<PitComponent, MapInitEvent>(OnPitMapInit);
        SubscribeLocalEvent<PitComponent, AnchorStateChangedEvent>(OnPitAnchorChanged);
        SubscribeNetworkEvent<PitBuildEvent>(OnBuild);
        SubscribeLocalEvent<PitComponent, DoAfterAttemptEvent<PitBuildDoAfterEvent>>(OnBuildAttempt);
        SubscribeLocalEvent<PitComponent, PitBuildDoAfterEvent>(OnBuildDoAfter);
    }

    // A pit is dug in the ground and cannot be unanchored by hand: when it comes loose the tile under it has given way (a collapse pulls the floor out), and the pit goes with it.
    private void OnPitAnchorChanged(Entity<PitComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored && !TerminatingOrDeleted(ent))
            QueueDel(ent);
    }

    private void OnPitMapInit(Entity<PitComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);
        if (xform.MapUid is not { } mapUid || xform.GridUid is not { } gridUid || !_gridQuery.HasComp(gridUid))
            return;

        _building.PrepareStoneForStair(mapUid, gridUid, _transform.GetWorldPosition(xform), out _, singleTile: true);
    }

    private void OnBuild(PitBuildEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user || HasComp<GhostComponent>(user))
            return;

        TryStartBuild(GetEntity(msg.Pit), user, msg.Option);
    }

    protected override void BuildMinePlate(Entity<PitComponent> pit, EntityUid user)
    {
        TryStartBuild(pit.Owner, user, PitBuildOption.MinePlate);
    }

    // The build of an option over a pit begins, if it can: the pit is in reach, and the one who builds has what it costs (and the skill, for the plate).
    private void TryStartBuild(EntityUid pit, EntityUid user, PitBuildOption option)
    {
        if (HasComp<GhostComponent>(user) || !HasComp<PitComponent>(pit) || TerminatingOrDeleted(pit))
            return;

        var xform = Transform(pit);
        if (xform.MapUid is not { } mapUid || xform.GridUid is not { } gridUid || !_gridQuery.HasComp(gridUid))
            return;

        if (!_interaction.InRangeUnobstructed(user, xform.Coordinates))
            return;

        if (!TryValidateCost(option, user, pit, mapUid, popupFail: true, out var used))
            return;

        var doAfter = new DoAfterArgs(EntityManager, user, BuildDelay, new PitBuildDoAfterEvent(option), pit, pit, used)
        {
            NeedHand = used != null,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
            BreakOnMove = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("au14-pit-build-started"), pit, user);
    }

    private void OnBuildAttempt(Entity<PitComponent> ent, ref DoAfterAttemptEvent<PitBuildDoAfterEvent> args)
    {
        if (args.Cancelled)
            return;

        var user = args.DoAfter.Args.User;
        if (!_interaction.InRangeUnobstructed(user, Transform(ent).Coordinates))
            args.Cancel();
    }

    private void OnBuildDoAfter(Entity<PitComponent> ent, ref PitBuildDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var pit = ent.Owner;
        var user = args.User;

        var xform = Transform(pit);
        if (xform.MapUid is not { } mapUid || xform.GridUid is not { } gridUid || !_gridQuery.HasComp(gridUid))
            return;

        if (!TryValidateCost(args.Option, user, pit, mapUid, popupFail: true, out var used))
            return;

        var worldPos = _transform.GetWorldPosition(xform);

        ConsumeCost(args.Option, used);

        switch (args.Option)
        {
            case PitBuildOption.Expand:
                BuildExpand(pit, mapUid, gridUid, worldPos, user);
                break;
            case PitBuildOption.Rope:
                BuildRope(pit, mapUid, gridUid, worldPos, user);
                break;
            case PitBuildOption.Hatch:
                BuildHatch(pit, mapUid, gridUid, worldPos, user);
                break;
            case PitBuildOption.Ladder:
                BuildPair(pit, mapUid, gridUid, worldPos, user, "RUMCPitLadderDown", "RUMCPitLadderUp", clearTile: false);
                break;
            case PitBuildOption.Steps:
                BuildStairs(pit, gridUid, worldPos);
                break;
            case PitBuildOption.MinePlate:
                BuildMinePlate(pit, user);
                break;
        }
    }

    public static int GetMetalCost(PitBuildOption option) => option switch
    {
        PitBuildOption.Ladder => 25,
        PitBuildOption.Hatch => 35,
        PitBuildOption.Steps => 50,
        PitBuildOption.MinePlate => 10,
        _ => 0,
    };

    private bool TryValidateCost(PitBuildOption option, EntityUid user, EntityUid pit, EntityUid mapUid, bool popupFail, out EntityUid? used)
    {
        used = null;

        // The plate with the code of a mine is a sapper's work, whoever asks for it and however.
        if (option == PitBuildOption.MinePlate && !HasComp<SapperComponent>(user))
        {
            if (popupFail)
                _popup.PopupEntity(Loc.GetString("au14-pit-build-sapper-only"), pit, user, PopupType.SmallCaution);
            return false;
        }

        if (option == PitBuildOption.Expand)
        {
            if (!_hands.TryGetActiveItem(user, out var tool) || tool is not { } heldTool)
            {
                if (popupFail)
                    FailDigTool(pit, user);
                return false;
            }

            if (!_building.CanDigWith(mapUid, heldTool))
            {
                if (popupFail)
                    FailDigTool(pit, user);
                return false;
            }

            used = heldTool;
            return true;
        }

        if (!_hands.TryGetActiveItem(user, out var item) || item is not { } heldItem)
        {
            if (popupFail)
                FailCost(pit, user, option);
            return false;
        }

        if (option == PitBuildOption.Rope)
        {
            if (MetaData(heldItem).EntityPrototype?.ID != RopeCoilProto)
            {
                if (popupFail)
                    FailCost(pit, user, option);
                return false;
            }

            used = heldItem;
            return true;
        }

        var cost = GetMetalCost(option);
        if (!TryComp<StackComponent>(heldItem, out var stack) ||
            stack.StackTypeId != MetalStackType ||
            _stack.GetCount((heldItem, stack)) < cost)
        {
            if (popupFail)
                FailCost(pit, user, option);
            return false;
        }

        used = heldItem;
        return true;
    }

    private void ConsumeCost(PitBuildOption option, EntityUid? used)
    {
        if (used is not { } item)
            return;

        if (option == PitBuildOption.Rope)
        {
            QueueDel(item);
            return;
        }

        var cost = GetMetalCost(option);
        if (cost > 0 && TryComp<StackComponent>(item, out var stack))
            _stack.TryUse((item, stack), cost);
    }

    private void FailCost(EntityUid pit, EntityUid user, PitBuildOption option)
    {
        var message = option == PitBuildOption.Rope
            ? Loc.GetString("au14-pit-build-need-rope")
            : Loc.GetString("au14-pit-build-need-metal", ("amount", GetMetalCost(option)));
        _popup.PopupEntity(message, pit, user, PopupType.SmallCaution);
    }

    private void FailDigTool(EntityUid pit, EntityUid user)
    {
        _popup.PopupEntity(Loc.GetString("au14-pit-build-need-dig-tool"), pit, user, PopupType.SmallCaution);
    }

    private void BuildExpand(EntityUid pit, EntityUid mapUid, EntityUid gridUid, Vector2 worldPos, EntityUid user)
    {
        if (!_building.PrepareStoneForStair(mapUid, gridUid, worldPos, out _, singleTile: true))
        {
            Fail(pit, user);
            return;
        }

        ClearTile(gridUid, worldPos);
        QueueDel(pit);
    }

    private void BuildPair(
        EntityUid pit,
        EntityUid mapUid,
        EntityUid gridUid,
        Vector2 worldPos,
        EntityUid user,
        string downProto,
        string upProto,
        bool clearTile)
    {
        BuildPairInternal(pit, mapUid, gridUid, worldPos, user, downProto, upProto, clearTile);
    }

    private void BuildRope(EntityUid pit, EntityUid mapUid, EntityUid gridUid, Vector2 worldPos, EntityUid user)
    {
        var (down, up) = BuildPairInternal(pit, mapUid, gridUid, worldPos, user, "RUMCPitRopeDown", "RUMCPitRopeUp", clearTile: false);
        if (down is not { } downUid || up is not { } upUid)
            return;

        EnsureComp<PitRopeComponent>(downUid).Other = upUid;
    }

    private void BuildHatch(EntityUid pit, EntityUid mapUid, EntityUid gridUid, Vector2 worldPos, EntityUid user)
    {
        var (down, up) = BuildPairInternal(pit, mapUid, gridUid, worldPos, user, "RUMCPitHatchDown", "RUMCPitLadderUp", clearTile: false);
        if (down is not { } hatchUid || up is not { } ladderUid)
            return;

        EnsureComp<HatchLinkComponent>(ladderUid).Hatch = hatchUid;
    }

    private (EntityUid? Down, EntityUid? Up) BuildPairInternal(
        EntityUid pit,
        EntityUid mapUid,
        EntityUid gridUid,
        Vector2 worldPos,
        EntityUid user,
        string downProto,
        string upProto,
        bool clearTile)
    {
        if (!_building.PrepareStoneForStair(mapUid, gridUid, worldPos, out var belowGrid, singleTile: true))
        {
            Fail(pit, user);
            return (null, null);
        }

        if (clearTile)
            ClearTile(gridUid, worldPos);

        QueueDel(pit);
        var down = SpawnAtWorld(gridUid, worldPos, downProto);
        var up = SpawnAtWorld(belowGrid, worldPos, upProto);
        return (down, up);
    }

    // The pit is covered with a plate that has the code of a mine in it: it is floor now, and gives way under whoever steps on it.
    private void BuildMinePlate(EntityUid pit, EntityUid user)
    {
        var coordinates = Transform(pit).Coordinates;
        QueueDel(pit);
        _plates.Lay("RUMCMinePlateLaid", coordinates, user);
        _popup.PopupEntity(Loc.GetString("au14-mine-plate-laid"), user, user);
    }

    private void BuildStairs(EntityUid pit, EntityUid gridUid, Vector2 worldPos)
    {
        QueueDel(pit);
        SpawnAtWorld(gridUid, worldPos, "RUMCPitStairDown");
    }

    private void ClearTile(EntityUid gridUid, Vector2 worldPos)
    {
        if (!_gridQuery.TryComp(gridUid, out var grid) ||
            Transform(gridUid).MapUid is not { } mapUid ||
            !TryComp<MapComponent>(mapUid, out var mapComp))
        {
            return;
        }

        var tile = _map.TileIndicesFor(gridUid, grid, new MapCoordinates(worldPos, mapComp.MapId));
        _map.SetTile(gridUid, grid, tile, Tile.Empty);
    }

    private EntityUid? SpawnAtWorld(EntityUid gridUid, Vector2 worldPos, string proto)
    {
        if (string.IsNullOrEmpty(proto) || !_gridQuery.TryComp(gridUid, out var grid))
            return null;

        if (Transform(gridUid).MapUid is not { } mapUid || !TryComp<MapComponent>(mapUid, out var mapComp))
            return null;

        var tile = _map.TileIndicesFor(gridUid, grid, new MapCoordinates(worldPos, mapComp.MapId));
        return Spawn(proto, _map.GridTileToLocal(gridUid, grid, tile));
    }

    private void Fail(EntityUid pit, EntityUid user)
    {
        _popup.PopupEntity(Loc.GetString("au14-pit-build-failed"), pit, user, PopupType.SmallCaution);
    }
}
