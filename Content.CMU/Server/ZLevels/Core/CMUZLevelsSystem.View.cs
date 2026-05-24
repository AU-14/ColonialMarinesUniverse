using System.Numerics;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private ViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private ExamineSystemShared _examine = default!;

    private readonly EntProtoId _zEyeProto = "CMUZLevelEye";
    private const int ZProbeOpeningTileRadius = 24;
    private const float StairPreviewProbeRadius = 5f;

    private bool _zLevelsEnabled = true;
    private int _maxRenderDepth = 1;
    private int _maxViewProbesPerPlayer = 2;
    private float _minProbePvsScale = 1f;
    private TimeSpan _zLevelViewerUpdateRate = TimeSpan.FromSeconds(0.25f);
    private TimeSpan _nextZLevelViewerUpdate = TimeSpan.Zero;
    private readonly Dictionary<EntityUid, Dictionary<int, EntityUid>> _viewerProbeEyes = new();
    private readonly CMUZLevelOpeningCache _zOpeningCache = new();
    private readonly List<int> _wantedProbeDepths = new();
    private readonly List<int> _probeDepthsToRemove = new();
    private readonly List<Vector2> _stairPreviewPositions = new(CMUZLevelViewerComponent.MaxStairPreviewPositions);
    private EntityQuery<MapGridComponent> _viewGridQuery;
    private EntityQuery<CMUZLevelHighGroundComponent> _viewHighGroundQuery;

    private void InitView()
    {
        _viewGridQuery = GetEntityQuery<MapGridComponent>();
        _viewHighGroundQuery = GetEntityQuery<CMUZLevelHighGroundComponent>();

        Subs.CVar(_config, CMUZLevelsCVars.Enabled, OnZLevelsEnabledChanged, true);
        Subs.CVar(_config, CMUZLevelsCVars.MaxRenderDepth, OnMaxRenderDepthChanged, true);
        Subs.CVar(_config, CMUZLevelsCVars.MaxViewProbesPerPlayer, OnMaxViewProbesChanged, true);
        Subs.CVar(_config, CMUZLevelsCVars.MinProbePvsScale, OnMinProbePvsScaleChanged, true);
        Subs.CVar(_config, CMUZLevelsCVars.ProbeUpdateHz, OnProbeUpdateHzChanged, true);

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);

        SubscribeLocalEvent<CMUZLevelViewerComponent, ComponentStartup>(OnViewerStartup);
        SubscribeLocalEvent<CMUZLevelViewerComponent, ComponentShutdown>(OnViewerShutdown);
        SubscribeLocalEvent<CMUZLevelViewerComponent, MapUidChangedEvent>(OnViewerMapUidChanged);
        SubscribeLocalEvent<CMUZLevelViewerComponent, EntParentChangedMessage>(OnViewerParentChange);
        SubscribeLocalEvent<CMUZPhysicsComponent, CMUZLevelFallEvent>(OnZLevelFall);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridShutdown);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
    }

    private void UpdateView(float frameTime)
    {
        if (!_zLevelsEnabled)
            return;

        if (_gameTiming.CurTime < _nextZLevelViewerUpdate)
            return;

        _nextZLevelViewerUpdate = _gameTiming.CurTime + _zLevelViewerUpdateRate;

        using var profile = Prof.Group("CMU Z PVS Probes");

        var query = EntityQueryEnumerator<CMUZLevelViewerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var viewer, out var xform))
        {
            SyncViewerProbes((uid, viewer), xform);

            if (!TryGetViewerRenderOrigin((uid, viewer), xform, out _, out _, out var globalPos))
                continue;

            if (_viewerProbeEyes.TryGetValue(uid, out var probes))
            {
                foreach (var (depth, eye) in probes)
                {
                    _transform.SetWorldPosition(eye, GetProbeWorldPosition(viewer, depth, globalPos));
                    SyncZLevelEye(uid, eye);
                }
            }
        }
    }

    private void OnViewerStartup(Entity<CMUZLevelViewerComponent> ent, ref ComponentStartup args)
    {
        _meta.AddFlag(ent, MetaDataFlags.ExtraTransformEvents);
    }

    private void OnViewerShutdown(Entity<CMUZLevelViewerComponent> ent, ref ComponentShutdown args)
    {
        _meta.RemoveFlag(ent, MetaDataFlags.ExtraTransformEvents);

        foreach (var eye in ent.Comp.Eyes)
        {
            QueueDel(eye);
        }

        ent.Comp.Eyes.Clear();
        _viewerProbeEyes.Remove(ent);
    }

    protected override void OnViewerMove(Entity<CMUZLevelViewerComponent> ent, ref MoveEvent args)
    {
        base.OnViewerMove(ent, ref args);

        var globalPos = _transform.GetWorldPosition(ent);
        if (!_viewerProbeEyes.TryGetValue(ent, out var probes))
            return;

        foreach (var (depth, eye) in probes)
        {
            _transform.SetWorldPosition(eye, GetProbeWorldPosition(ent.Comp, depth, globalPos));
        }
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!_zLevelsEnabled)
            return;

        var viewer = EnsureComp<CMUZLevelViewerComponent>(ev.Entity);
        UpdateViewer((ev.Entity, viewer));
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        RemComp<CMUZLevelViewerComponent>(ev.Entity);
    }

    private void OnViewerMapUidChanged(Entity<CMUZLevelViewerComponent> ent, ref MapUidChangedEvent args)
    {
        UpdateViewer(ent);
    }

    private void OnViewerParentChange(Entity<CMUZLevelViewerComponent> ent, ref EntParentChangedMessage args)
    {
        UpdateViewer(ent);
    }

    private void UpdateViewer(Entity<CMUZLevelViewerComponent> ent)
    {
        ClearViewerProbes(ent);
        SyncViewerProbes(ent);
    }

    private void ClearViewerProbes(Entity<CMUZLevelViewerComponent> ent)
    {
        SetStairPreviewUp(ent, false);

        foreach (var eye in ent.Comp.Eyes)
        {
            QueueDel(eye);
        }

        ent.Comp.Eyes.Clear();
        _viewerProbeEyes.Remove(ent.Owner);
    }

    private void SyncViewerProbes(Entity<CMUZLevelViewerComponent> ent, TransformComponent? xform = null)
    {
        if (!_zLevelsEnabled ||
            _maxViewProbesPerPlayer <= 0 ||
            !TryComp<ActorComponent>(ent, out var actor))
        {
            ClearViewerProbes(ent);
            return;
        }

        xform ??= Transform(ent);
        if (!TryGetViewerRenderOrigin(ent, xform, out var originXform, out var map, out var globalPos))
        {
            ClearViewerProbes(ent);
            return;
        }

        var stairPreviewUp = CanPreviewUpperZFromStair((ent.Owner, ent.Comp), originXform, map, globalPos, _stairPreviewPositions);
        SetStairPreviewUp(ent, stairPreviewUp, _stairPreviewPositions);
        BuildWantedProbeDepths(map, globalPos, _wantedProbeDepths, stairPreviewUp);

        if (!_viewerProbeEyes.TryGetValue(ent.Owner, out var probes))
        {
            probes = new Dictionary<int, EntityUid>();
            _viewerProbeEyes[ent.Owner] = probes;
        }

        _probeDepthsToRemove.Clear();
        foreach (var (depth, eye) in probes)
        {
            if (!_wantedProbeDepths.Contains(depth) ||
                TerminatingOrDeleted(eye))
            {
                _probeDepthsToRemove.Add(depth);
            }
        }

        foreach (var depth in _probeDepthsToRemove)
        {
            if (!probes.Remove(depth, out var eye))
                continue;

            ent.Comp.Eyes.Remove(eye);
            QueueDel(eye);
        }

        foreach (var depth in _wantedProbeDepths)
        {
            if (probes.ContainsKey(depth))
                continue;

            if (!TryMapOffset(map, depth, out var probeMap))
                continue;

            var probePosition = GetProbeWorldPosition(ent.Comp, depth, globalPos);
            var newEye = SpawnAtPosition(_zEyeProto, new EntityCoordinates(probeMap.Value, probePosition));

            Transform(newEye).GridTraversal = false;
            SyncZLevelEye(ent, newEye);
            _viewSubscriber.AddViewSubscriber(newEye, actor.PlayerSession);
            probes[depth] = newEye;
            ent.Comp.Eyes.Add(newEye);
        }

        _wantedProbeDepths.Clear();
        _probeDepthsToRemove.Clear();
    }

    private bool TryGetViewerRenderOrigin(
        Entity<CMUZLevelViewerComponent> viewer,
        TransformComponent viewerXform,
        out TransformComponent originXform,
        out EntityUid map,
        out Vector2 globalPos)
    {
        originXform = viewerXform;
        map = default;
        globalPos = default;

        if (TryComp(viewer.Owner, out EyeComponent? eye) &&
            eye.Target is { } target &&
            TryComp(target, out TransformComponent? targetXform) &&
            targetXform.MapUid is { } targetMap &&
            HasComp<CMUZLevelMapComponent>(targetMap))
        {
            originXform = targetXform;
        }

        if (originXform.MapUid is not { } originMap)
            return false;

        map = originMap;
        globalPos = _transform.GetWorldPosition(originXform);
        return true;
    }

    private void BuildWantedProbeDepths(EntityUid map, Vector2 globalPos, List<int> depths, bool forceUpperPreview)
    {
        depths.Clear();

        var remainingProbes = _maxViewProbesPerPlayer;
        var upperPreviewReserved = false;

        if (forceUpperPreview &&
            remainingProbes > 0 &&
            TryMapUp(map, out _))
        {
            depths.Add(1);
            remainingProbes--;
            upperPreviewReserved = true;
        }

        var lowerDepth = Math.Min(_maxRenderDepth, MaxZLevelsBelowRendering);

        for (var i = 1; i <= lowerDepth && remainingProbes > 0; i++)
        {
            if (!TryMapOffset(map, -i, out _))
                break;

            if (!HasZOpeningPath(map, globalPos, -i))
                break;

            depths.Add(-i);
            remainingProbes--;
        }

        if (remainingProbes <= 0)
            return;

        if (upperPreviewReserved)
            return;

        if (!TryMapUp(map, out var aboveMapUid))
            return;

        // Keep upper-level PVS warm only around local openings, so stairs and look-up transitions stay responsive
        // without subscribing every player to the whole level above them forever.
        if (!HasZOpeningNear(aboveMapUid.Value, globalPos))
            return;

        depths.Add(1);
    }

    private bool CanPreviewUpperZFromStair(
        Entity<CMUZLevelViewerComponent> viewer,
        TransformComponent viewerXform,
        EntityUid map,
        Vector2 globalPos,
        List<Vector2> previewPositions)
    {
        previewPositions.Clear();

        if (!TryMapUp(map, out _) ||
            !_viewGridQuery.TryComp(map, out var grid))
        {
            return false;
        }

        var origin = new MapCoordinates(globalPos, viewerXform.MapID);
        var centerTile = _map.WorldToTile(map, grid, globalPos);
        var tileRadius = Math.Max(1, (int) MathF.Ceiling(StairPreviewProbeRadius / grid.TileSize));

        for (var x = -tileRadius; x <= tileRadius; x++)
        {
            for (var y = -tileRadius; y <= tileRadius; y++)
            {
                var tile = centerTile + new Vector2i(x, y);
                var query = _map.GetAnchoredEntitiesEnumerator(map, grid, tile);
                while (query.MoveNext(out var uid))
                {
                    if (uid is not { } highGroundUid ||
                        !_viewHighGroundQuery.TryComp(highGroundUid, out var highGround) ||
                        !highGround.PreviewUpLevel ||
                        highGround.SupportOnlyFromAbove ||
                        highGround.PreviewRange <= 0f)
                    {
                        continue;
                    }

                    var target = _transform.GetMapCoordinates(highGroundUid);
                    var range = highGround.PreviewRange + 0.05f;
                    if (Vector2.DistanceSquared(origin.Position, target.Position) > range * range)
                        continue;

                    if (_examine.InRangeUnOccluded(origin, target, highGround.PreviewRange, ent => ent == viewer.Owner || ent == highGroundUid))
                    {
                        AddStairPreviewPosition(previewPositions, target.Position);
                        if (previewPositions.Count >= CMUZLevelViewerComponent.MaxStairPreviewPositions)
                            return true;
                    }
                }
            }
        }

        return previewPositions.Count > 0;
    }

    private static void AddStairPreviewPosition(List<Vector2> previewPositions, Vector2 position)
    {
        foreach (var existing in previewPositions)
        {
            if (Vector2.DistanceSquared(existing, position) < 0.001f)
                return;
        }

        previewPositions.Add(position);
    }

    private void SetStairPreviewUp(
        Entity<CMUZLevelViewerComponent> viewer,
        bool enabled,
        IReadOnlyList<Vector2>? previewPositions = null)
    {
        var changed = false;

        if (viewer.Comp.StairPreviewUp != enabled)
        {
            viewer.Comp.StairPreviewUp = enabled;
            changed = true;
        }

        var count = enabled && previewPositions != null
            ? Math.Min(previewPositions.Count, CMUZLevelViewerComponent.MaxStairPreviewPositions)
            : 0;

        if (viewer.Comp.StairPreviewPositionCount != count)
        {
            viewer.Comp.StairPreviewPositionCount = count;
            changed = true;
        }

        for (var i = 0; i < CMUZLevelViewerComponent.MaxStairPreviewPositions; i++)
        {
            var position = i < count ? previewPositions![i] : default;
            if (Vector2.DistanceSquared(viewer.Comp.GetStairPreviewPosition(i), position) <= 0.001f)
                continue;

            viewer.Comp.SetStairPreviewPosition(i, position);
            changed = true;
        }

        if (changed)
            Dirty(viewer.Owner, viewer.Comp);
    }

    private static Vector2 GetProbeWorldPosition(CMUZLevelViewerComponent viewer, int depth, Vector2 globalPos)
    {
        if (depth == 1 &&
            viewer.StairPreviewUp &&
            !viewer.LookUp &&
            viewer.StairPreviewPositionCount > 0)
        {
            return viewer.StairPreviewPosition;
        }

        return globalPos;
    }

    private bool HasZOpeningPath(EntityUid map, Vector2 globalPos, int targetDepth)
    {
        var step = targetDepth < 0 ? -1 : 1;

        for (var depth = 0; depth != targetDepth; depth += step)
        {
            var checkingMap = map;
            if (depth != 0)
            {
                if (!TryMapOffset(map, depth, out var offsetMap))
                    return false;

                checkingMap = offsetMap.Value;
            }

            if (!HasZOpeningNear(checkingMap, globalPos))
                return false;
        }

        return true;
    }

    private bool HasZOpeningNear(EntityUid map, Vector2 globalPos)
    {
        if (!TryComp<MapGridComponent>(map, out var grid))
            return true;

        var mapId = _transform.GetMapId(map);
        if (mapId == MapId.Nullspace)
            return true;

        var mapCoordinates = new MapCoordinates(globalPos, mapId);
        var center = _map.TileIndicesFor(map, grid, mapCoordinates);
        var start = center - new Vector2i(ZProbeOpeningTileRadius, ZProbeOpeningTileRadius);
        var end = center + new Vector2i(ZProbeOpeningTileRadius, ZProbeOpeningTileRadius);
        var gridEnt = new Entity<MapGridComponent>(map, grid);

        return _zOpeningCache.HasOpeningInTileBounds(gridEnt, start, end, _map, TilDefMan);
    }

    private void OnZLevelsEnabledChanged(bool enabled)
    {
        _zLevelsEnabled = enabled;

        if (!enabled)
            _zOpeningCache.Clear();

        RefreshViewers();
    }

    private void OnGridShutdown(GridRemovalEvent args)
    {
        _zOpeningCache.RemoveGrid(args.EntityUid);
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        _zOpeningCache.InvalidateTiles(args.Entity, args.Changes);
        OnZPhysicsTileChanged(ref args);
    }

    private void OnMaxRenderDepthChanged(int value)
    {
        _maxRenderDepth = Math.Clamp(value, 0, MaxZLevelsBelowRendering);
        RefreshViewers();
    }

    private void OnMaxViewProbesChanged(int value)
    {
        _maxViewProbesPerPlayer = Math.Max(0, value);
        RefreshViewers();
    }

    private void OnMinProbePvsScaleChanged(float value)
    {
        _minProbePvsScale = Math.Clamp(value, 0.1f, 100f);
        RefreshViewers();
    }

    private void OnProbeUpdateHzChanged(float value)
    {
        var hz = Math.Clamp(value, 0.1f, 20.0f);
        _zLevelViewerUpdateRate = TimeSpan.FromSeconds(1.0f / hz);
    }

    private void RefreshViewers()
    {
        if (!_zLevelsEnabled)
        {
            var disabledQuery = EntityQueryEnumerator<CMUZLevelViewerComponent>();
            while (disabledQuery.MoveNext(out var uid, out var viewer))
            {
                ClearViewerProbes((uid, viewer));
            }

            return;
        }

        var query = EntityQueryEnumerator<CMUZLevelViewerComponent>();
        while (query.MoveNext(out var uid, out var viewer))
        {
            UpdateViewer((uid, viewer));
        }
    }

    private void SyncZLevelEye(EntityUid viewer, EntityUid zEye)
    {
        var eye = EnsureComp<EyeComponent>(zEye);
        var pvsScale = _minProbePvsScale;

        if (TryComp<EyeComponent>(viewer, out var viewerEye))
        {
            pvsScale = MathF.Max(pvsScale, viewerEye.PvsScale);
            _eye.SetVisibilityMask(zEye, viewerEye.VisibilityMask, eye);
        }

        _eye.SetPvsScale((zEye, eye), pvsScale);
    }

    private void OnZLevelFall(Entity<CMUZPhysicsComponent> ent, ref CMUZLevelFallEvent args)
    {
        //A dirty trick: we call PredictedPopup on the falling entity on SERVER.
        //This means that the one who is falling does not see the popup itself, but everyone around them does. This is what we need.
        _popup.PopupPredictedCoordinates(Loc.GetString("cmu-zlevel-falling-popup", ("name", Identity.Name(ent, EntityManager))), Transform(ent).Coordinates, ent);
    }

}
