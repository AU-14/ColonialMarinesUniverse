using System.Collections.Generic;
using System.Numerics;
using Content.Client.CombatMode;
using Content.Client.Eye;
using Content.Client.Movement.Components;
using Content.Client.Resources;
using Content.Shared._CMU14.Dropship.Integrity;
using Content.Shared._CMU14.Dropship.TacticalLand;
using Content.Shared.Buckle.Components;
using Content.Shared.Eye;
using Content.Shared.Movement.Components;
using Content.Shared.Tag;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.ComponentTrees;
using Robust.Shared.Enums;
using Robust.Shared.IoC;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Dropship.TacticalLand;

public sealed partial class GunshipPilotCameraSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private OccluderSystem _occluder = default!;
    [Dependency] private TagSystem _tags = default!;

    private static readonly TimeSpan HullOccluderRefreshInterval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan HullOccluderMaintenanceInterval = TimeSpan.FromMilliseconds(100);
    private const float PilotCursorMaxOffset = 24f;
    // Larger than the maximum possible opposite-edge cursor delta, so the
    // camera offset reaches the mouse-derived target in one rendered update.
    private const float PilotCursorPanSpeed = 64f;
    private const float PilotCursorPvsIncrease = 2.4f;
    private const float PilotMaximumZoom = 2.25f;
    private readonly HashSet<EntityUid> _suppressedHullOccluders = new();
    private EntityUid? _suppressedGrid;
    private TimeSpan _nextHullOccluderRefresh;
    private TimeSpan _nextHullOccluderMaintenance;
    private float _pilotZoomMultiplier = 1f;
    private bool _pilotZoomActive;
    private EntityUid? _configuredPilotCursor;
    private GunshipPilotOverlay _pilotOverlay = default!;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(OccluderSystem));
        UpdatesAfter.Add(typeof(EyeLerpingSystem));
        _pilotOverlay = new GunshipPilotOverlay(EntityManager, _player);
        _overlay.AddOverlay(_pilotOverlay);
        _overlay.AddOverlay(new GunshipPilotHudOverlay(EntityManager, _player));
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<TagComponent, ComponentStartup>(OnTaggedEntityStartup);
        SubscribeLocalEvent<TagComponent, ComponentShutdown>(OnTaggedEntityShutdown);
        SubscribeLocalEvent<TagComponent, EntParentChangedMessage>(OnTaggedEntityParentChanged);
    }

    public override void Shutdown()
    {
        RestoreHullOccluders();
        base.Shutdown();
        _overlay.RemoveOverlay<GunshipPilotOverlay>();
        _overlay.RemoveOverlay<GunshipPilotHudOverlay>();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // These are strictly local rendering changes. Running them from tick
        // Update makes them execute during prediction and lets the normal eye
        // lerper overwrite the pilot zoom later in the same rendered frame.
        UpdatePilotHullOcclusion();
        UpdatePilotZoom(frameTime);

        // Component state application can legitimately requeue these entries.
        // Remove them from the client rendering tree again without changing
        // any networked component data.
        if (_timing.CurTime < _nextHullOccluderMaintenance)
            return;

        _nextHullOccluderMaintenance = _timing.CurTime + HullOccluderMaintenanceInterval;
        foreach (var uid in _suppressedHullOccluders)
        {
            if (!TerminatingOrDeleted(uid) &&
                TryComp(uid, out OccluderComponent? occluder) &&
                occluder.Enabled)
            {
                RemoveFromClientOccluderTree(occluder);
            }
        }
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        _pilotOverlay.InvalidateGrid(args.Entity.Owner);
    }

    private void OnTaggedEntityStartup(Entity<TagComponent> ent, ref ComponentStartup args)
    {
        InvalidateTaggedEntity(ent);
    }

    private void OnTaggedEntityShutdown(Entity<TagComponent> ent, ref ComponentShutdown args)
    {
        InvalidateTaggedEntity(ent);
    }

    private void InvalidateTaggedEntity(Entity<TagComponent> ent)
    {
        if (!_tags.HasTag(ent.Comp, WallTag) ||
            !TryComp(ent.Owner, out TransformComponent? xform))
        {
            return;
        }

        _pilotOverlay.InvalidateGrid(xform.GridUid);
    }

    private void OnTaggedEntityParentChanged(Entity<TagComponent> ent, ref EntParentChangedMessage args)
    {
        if (!_tags.HasTag(ent.Comp, WallTag))
            return;

        _pilotOverlay.InvalidateGrid(args.OldParent);
        _pilotOverlay.InvalidateGrid(args.Transform.GridUid);
    }

    private void UpdatePilotHullOcclusion()
    {
        EntityUid? desiredGrid = null;
        var localPilot = _player.LocalEntity;
        var seatedInGunshipPilotSeat = localPilot is { } seatedPilot &&
            TryComp(seatedPilot, out BuckleComponent? buckle) &&
            buckle.BuckledTo is { } seat &&
            HasComp<GunshipPilotSeatComponent>(seat);

        if (localPilot is { } local &&
            TryComp(local, out GunshipPilotHudComponent? hud) &&
            hud.Dropship is { } dropship &&
            TryComp(dropship, out DropshipIntegrityComponent? integrity) &&
            !integrity.Crashing &&
            !integrity.Wrecked &&
            hud.ViewOffset == 0 &&
            !hud.RearView &&
            !hud.Malfunctions.Contains(DropshipMalfunction.SensorArrayFault))
        {
            desiredGrid = dropship;

            // This component is split between client and server, so these
            // pilot-specific values must also be applied to the local copy.
            if (TryComp(local, out EyeCursorOffsetComponent? cursor))
            {
                cursor.MaxOffset = hud.PilotPanning ? PilotCursorMaxOffset : 0f;
                cursor.OffsetSpeed = PilotCursorPanSpeed;
                cursor.PvsIncrease = hud.PilotPanning ? PilotCursorPvsIncrease : 0f;
                if (!hud.PilotPanning)
                    ResetPilotCursor(cursor);
                _configuredPilotCursor = local;
            }
        }
        else if (localPilot is { } unlinkedPilot &&
                 seatedInGunshipPilotSeat &&
                 TryComp(unlinkedPilot, out EyeCursorOffsetComponent? unlinkedCursor))
        {
            // The cursor component's tuning fields are client-local. A newly
            // replicated component therefore starts with its generic three-tile
            // pan until the client explicitly neutralizes it.
            ResetPilotCursor(unlinkedCursor);
            _configuredPilotCursor = unlinkedPilot;
        }
        else if (_configuredPilotCursor is { } previousPilot)
        {
            if (!TerminatingOrDeleted(previousPilot) &&
                TryComp(previousPilot, out EyeCursorOffsetComponent? previousCursor))
            {
                ResetPilotCursor(previousCursor);
            }

            _configuredPilotCursor = null;
        }

        if (_suppressedGrid != desiredGrid)
        {
            RestoreHullOccluders();
            _suppressedGrid = desiredGrid;
            _nextHullOccluderRefresh = TimeSpan.Zero;
        }

        if (desiredGrid is not { } grid || _timing.CurTime < _nextHullOccluderRefresh)
            return;

        _nextHullOccluderRefresh = _timing.CurTime + HullOccluderRefreshInterval;
        var children = Transform(grid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (!TryComp(child, out OccluderComponent? occluder) ||
                !occluder.Enabled)
            {
                continue;
            }

            _suppressedHullOccluders.Add(child);
            RemoveFromClientOccluderTree(occluder);
        }
    }

    private void UpdatePilotZoom(float frameTime)
    {
        if (_player.LocalEntity is not { } local ||
            !TryComp(local, out ContentEyeComponent? contentEye))
        {
            _pilotZoomMultiplier = 1f;
            _pilotZoomActive = false;
            return;
        }

        if (!TryComp(local, out GunshipPilotHudComponent? hud) ||
            hud.Dropship is not { } dropship ||
            !TryComp(dropship, out DropshipIntegrityComponent? integrity) ||
            integrity.Crashing ||
            integrity.Wrecked ||
            hud.ViewOffset != 0 ||
            hud.RearView ||
            hud.Malfunctions.Contains(DropshipMalfunction.SensorArrayFault))
        {
            if (_pilotZoomActive)
                ResetPilotZoom(contentEye);
            return;
        }

        _pilotZoomActive = true;
        var targetMultiplier = hud.PilotZoom ? 1.5f : 1f;
        if (hud.PilotPanning && TryComp(local, out EyeCursorOffsetComponent? cursor))
        {
            var distance = Math.Clamp(cursor.CurrentPosition.Length() / PilotCursorMaxOffset, 0f, 1f);
            targetMultiplier = MathHelper.Lerp(1f, PilotMaximumZoom, distance);
        }

        // This runs after EyeLerpingSystem every rendered frame. Updating only
        // on ticks lets the normal eye lerper restore the base zoom between
        // pilot updates, which presents as alternating/choppy zoom. Keep a
        // local multiplier and ease it continuously instead.
        var zoomBlend = 1f - MathF.Exp(-10f * MathF.Max(frameTime, 0f));
        _pilotZoomMultiplier = MathHelper.Lerp(_pilotZoomMultiplier, targetMultiplier, zoomBlend);
        if (MathF.Abs(_pilotZoomMultiplier - targetMultiplier) < 0.001f)
            _pilotZoomMultiplier = targetMultiplier;

        var desiredZoom = contentEye.TargetZoom * _pilotZoomMultiplier;
        if (Vector2.DistanceSquared(_eyeManager.CurrentEye.Zoom, desiredZoom) > 0.0001f)
            _eyeManager.CurrentEye.Zoom = desiredZoom;
    }

    private void ResetPilotZoom(ContentEyeComponent contentEye)
    {
        _pilotZoomMultiplier = 1f;
        _pilotZoomActive = false;
        if (Vector2.DistanceSquared(_eyeManager.CurrentEye.Zoom, contentEye.TargetZoom) > 0.0001f)
            _eyeManager.CurrentEye.Zoom = contentEye.TargetZoom;
    }

    private static void ResetPilotCursor(EyeCursorOffsetComponent cursor)
    {
        cursor.MaxOffset = 0f;
        cursor.OffsetSpeed = PilotCursorPanSpeed;
        cursor.PvsIncrease = 0f;
        cursor.TargetPosition = Vector2.Zero;
        cursor.CurrentPosition = Vector2.Zero;
    }

    private static void RemoveFromClientOccluderTree(OccluderComponent occluder)
    {
        IComponentTreeEntry<OccluderComponent> treeEntry = occluder;
        if (treeEntry.Tree == null)
            return;

        treeEntry.Tree.Remove(new ComponentTreeEntry<OccluderComponent> { Component = occluder });
        treeEntry.Tree = null;
        treeEntry.TreeUid = null;
    }

    private void RestoreHullOccluders()
    {
        foreach (var uid in _suppressedHullOccluders)
        {
            if (!TerminatingOrDeleted(uid) && TryComp(uid, out OccluderComponent? occluder))
                _occluder.QueueTreeUpdate(uid, occluder);
        }

        _suppressedHullOccluders.Clear();
        _suppressedGrid = null;
    }
}

public sealed class GunshipPilotHudOverlay : Overlay
{
    private readonly IEntityManager _entities;
    private readonly IPlayerManager _player;
    private readonly IGameTiming _timing;
    private readonly IInputManager _input;
    private readonly CombatModeSystem _combatMode;
    private readonly Font _font;
    private readonly Font _smallFont;
    private readonly List<string> _warningLines = new();

    private static readonly Color HudColor = new(0.25f, 0.88f, 1f, 0.95f);
    private static readonly Color HudBackground = new(0.015f, 0.06f, 0.08f, 0.78f);
    private static readonly Color HudDim = new(0.25f, 0.88f, 1f, 0.38f);
    private static readonly Color IntegrityColor = new(0.92f, 0.08f, 0.08f, 0.95f);
    private static readonly Color IntegrityBorder = new(1f, 0.22f, 0.22f, 0.8f);
    private static readonly Color ThrustColor = new(0.20f, 0.78f, 1f, 0.95f);
    private static readonly Color ThrustBorder = new(0.35f, 0.9f, 1f, 0.8f);
    private static readonly Color AmmoColor = new(1f, 0.72f, 0.16f, 0.95f);
    private static readonly Color MalfunctionColor = new(1f, 0.24f, 0.12f, 0.98f);

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public GunshipPilotHudOverlay(IEntityManager entities, IPlayerManager player)
    {
        _entities = entities;
        _player = player;
        _timing = IoCManager.Resolve<IGameTiming>();
        _input = IoCManager.Resolve<IInputManager>();
        _combatMode = entities.System<CombatModeSystem>();
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 18);
        _smallFont = cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 12);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _player.LocalEntity is { } local &&
               _entities.HasComponent<GunshipPilotHudComponent>(local) &&
               _entities.TryGetComponent(local, out EyeComponent? eye) &&
               ReferenceEquals(args.Viewport.Eye, eye.Eye);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local ||
            !_entities.TryGetComponent(local, out GunshipPilotHudComponent? hud))
        {
            return;
        }

        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;

        if (hud.Dropship == null)
        {
            DrawUnlinkedVisorTint(handle, bounds, hud);
            var message = Loc.GetString("cmu-gunship-hud-no-controls-linked");
            var measured = handle.DrawString(_font, Vector2.Zero, message, Color.Transparent);
            var position = new Vector2(bounds.Left + (bounds.Width - measured.X) * 0.5f, bounds.Top + 18f);
            handle.DrawString(_font, position + Vector2.One * 2f, message, Color.Black);
            handle.DrawString(_font, position, message, HudColor);
            return;
        }

        DrawDriftIndicator(handle, bounds, hud);
        DrawThrustBar(handle, bounds, hud);
        DrawIntegrityBar(handle, bounds, hud);
        DrawDirectFireAmmo(handle, bounds, hud);
        DrawWarnings(handle, bounds, hud);
        DrawDirectFireReticle(handle, local, hud);

    }

    private void DrawUnlinkedVisorTint(
        DrawingHandleScreen handle,
        UIBox2i bounds,
        GunshipPilotHudComponent hud)
    {
        if (hud.Visor == EntityUid.Invalid ||
            !_entities.TryGetComponent(hud.Visor, out GunshipPilotVisorComponent? visor))
        {
            return;
        }

        handle.DrawRect(bounds, visor.NightVisionTint.WithAlpha(0.05f));
    }

    private void DrawDriftIndicator(
        DrawingHandleScreen handle,
        UIBox2i bounds,
        GunshipPilotHudComponent hud)
    {
        const float size = 116f;
        const float margin = 24f;
        var left = bounds.Right - margin - size;
        var top = bounds.Top + (bounds.Height - size) * 0.5f;
        var box = new UIBox2(left, top, left + size, top + size);
        var center = box.Center + new Vector2(0f, 8f);

        handle.DrawRect(box, HudBackground);
        handle.DrawRect(box, HudDim, false);
        handle.DrawLine(new Vector2(center.X - 38f, center.Y), new Vector2(center.X + 38f, center.Y), HudDim);
        handle.DrawLine(new Vector2(center.X, center.Y - 38f), new Vector2(center.X, center.Y + 38f), HudDim);

        var labelPosition = new Vector2(left + 8f, top + 5f);
        handle.DrawString(_smallFont, labelPosition, Loc.GetString("cmu-gunship-hud-drift"), HudColor);

        var localVelocity = Angle.FromDegrees(-hud.ShipRotationDegrees).RotateVec(hud.LinearVelocity);
        var speed = localVelocity.Length();
        if (speed < 0.01f)
        {
            handle.DrawCircle(center, 4f, HudColor);
            return;
        }

        var direction = Vector2.Normalize(new Vector2(localVelocity.X, -localVelocity.Y));
        var length = 16f + 28f * Math.Clamp(speed / 8f, 0f, 1f);
        var tip = center + direction * length;
        var side = new Vector2(-direction.Y, direction.X);
        handle.DrawLine(center, tip, HudColor);
        handle.DrawLine(tip, tip - direction * 10f + side * 6f, HudColor);
        handle.DrawLine(tip, tip - direction * 10f - side * 6f, HudColor);
    }

    private void DrawIntegrityBar(
        DrawingHandleScreen handle,
        UIBox2i bounds,
        GunshipPilotHudComponent hud)
    {
        const float width = 320f;
        const float height = 24f;
        const float bottomMargin = 24f;
        var left = bounds.Left + (bounds.Width - width) * 0.5f;
        var top = bounds.Bottom - bottomMargin - height;
        var box = new UIBox2(left, top, left + width, top + height);
        var ratio = hud.MaxIntegrity > 0f
            ? Math.Clamp(hud.Integrity / hud.MaxIntegrity, 0f, 1f)
            : 0f;

        handle.DrawRect(box, HudBackground);
        if (ratio > 0f)
        {
            var fill = new UIBox2(box.Left + 3f,
                box.Top + 3f,
                box.Left + 3f + (box.Width - 6f) * ratio,
                box.Bottom - 3f);
            handle.DrawRect(fill, IntegrityColor);
        }

        handle.DrawRect(box, IntegrityBorder, false);

        var text = Loc.GetString("cmu-gunship-hud-hull-percent",
            ("percent", MathF.Round(ratio * 100f)));
        var textSize = handle.DrawString(_smallFont, Vector2.Zero, text, Color.Transparent);
        var textPosition = box.Center - textSize / 2f;
        handle.DrawString(_smallFont, textPosition + Vector2.One, text, Color.Black);
        handle.DrawString(_smallFont, textPosition, text, Color.White);
    }

    private void DrawThrustBar(
        DrawingHandleScreen handle,
        UIBox2i bounds,
        GunshipPilotHudComponent hud)
    {
        const float driftSize = 116f;
        const float margin = 24f;
        const float gap = 8f;
        const float height = 22f;
        var left = bounds.Right - margin - driftSize;
        var driftTop = bounds.Top + (bounds.Height - driftSize) * 0.5f;
        var top = driftTop + driftSize + gap;
        var box = new UIBox2(left, top, left + driftSize, top + height);
        var ratio = Math.Clamp(hud.ThrustPercent / 100f, 0f, 1f);

        handle.DrawRect(box, HudBackground);
        if (ratio > 0f)
        {
            var fill = new UIBox2(box.Left + 3f,
                box.Top + 3f,
                box.Left + 3f + (box.Width - 6f) * ratio,
                box.Bottom - 3f);
            handle.DrawRect(fill, ThrustColor);
        }

        handle.DrawRect(box, ThrustBorder, false);

        var text = Loc.GetString("cmu-gunship-hud-thrust-percent",
            ("percent", MathF.Round(ratio * 100f)));
        var textSize = handle.DrawString(_smallFont, Vector2.Zero, text, Color.Transparent);
        var textPosition = box.Center - textSize / 2f;
        handle.DrawString(_smallFont, textPosition + Vector2.One, text, Color.Black);
        handle.DrawString(_smallFont, textPosition, text, Color.White);
    }

    private void DrawDirectFireAmmo(
        DrawingHandleScreen handle,
        UIBox2i bounds,
        GunshipPilotHudComponent hud)
    {
        if (!hud.HasDirectFireWeapon)
            return;

        const float width = 116f;
        const float margin = 24f;
        const float driftSize = 116f;
        const float hullGap = 8f;
        const float hullHeight = 22f;
        const float gap = 6f;
        const float height = 22f;
        var left = bounds.Right - margin - width;
        var driftTop = bounds.Top + (bounds.Height - driftSize) * 0.5f;
        var top = driftTop + driftSize + hullGap + hullHeight + gap;
        var box = new UIBox2(left, top, left + width, top + height);

        handle.DrawRect(box, HudBackground);
        handle.DrawRect(box, AmmoColor, false);

        var count = Math.Max(0, hud.DirectFireAmmo);
        var text = Loc.GetString("cmu-gunship-hud-direct-ammo", ("count", count));
        var textSize = handle.DrawString(_smallFont, Vector2.Zero, text, Color.Transparent);
        var textPosition = box.Center - textSize / 2f;
        handle.DrawString(_smallFont, textPosition + Vector2.One, text, Color.Black);
        handle.DrawString(_smallFont, textPosition, text, AmmoColor);
    }

    private void DrawDirectFireReticle(
        DrawingHandleScreen handle,
        EntityUid pilot,
        GunshipPilotHudComponent hud)
    {
        if (!hud.HasDirectFireWeapon ||
            hud.ViewOffset != 0 ||
            hud.RearView ||
            !_combatMode.IsInCombatMode(pilot))
        {
            return;
        }

        var center = _input.MouseScreenPosition.Position;
        var top = center + new Vector2(0f, -9f);
        var lowerLeft = center + new Vector2(-8f, 6f);
        var lowerRight = center + new Vector2(8f, 6f);
        var shadow = Vector2.One;

        handle.DrawLine(top + shadow, lowerLeft + shadow, Color.Black);
        handle.DrawLine(lowerLeft + shadow, lowerRight + shadow, Color.Black);
        handle.DrawLine(lowerRight + shadow, top + shadow, Color.Black);
        handle.DrawLine(top, lowerLeft, HudColor);
        handle.DrawLine(lowerLeft, lowerRight, HudColor);
        handle.DrawLine(lowerRight, top, HudColor);
        handle.DrawCircle(center + shadow, 2.5f, Color.Black);
        handle.DrawCircle(center, 2f, HudColor);
    }

    private void DrawWarnings(
        DrawingHandleScreen handle,
        UIBox2i bounds,
        GunshipPilotHudComponent hud)
    {
        if (hud.Malfunctions.Count == 0 && hud.Alarms.Count == 0)
            return;

        const float margin = 24f;
        const float lineHeight = 20f;
        const float padding = 10f;
        var blinkOn = (int)_timing.CurTime.TotalSeconds % 2 == 0;
        if (!blinkOn)
            return;

        var lines = _warningLines;
        lines.Clear();
        lines.Add(Loc.GetString("cmu-gunship-hud-system-warnings"));
        foreach (var alarm in hud.Alarms)
            lines.Add(DropshipAlarmData.GetAlertName(alarm));
        foreach (var malfunction in hud.Malfunctions)
            lines.Add(Loc.GetString("cmu-gunship-malfunction-detected",
                ("malfunction", DropshipMalfunctionData.GetAlertName(malfunction))));
        if (hud.MasterAlarmSilenced && hud.Alarms.Count > 0)
            lines.Add(Loc.GetString("cmu-gunship-hud-master-alarm-silenced"));

        var width = 260f;
        foreach (var text in lines)
            width = MathF.Max(width, handle.DrawString(_smallFont, Vector2.Zero, text, Color.Transparent).X + padding * 2f);

        var height = padding * 2f + lineHeight * lines.Count;
        var bottomMargin = hud.ManeuveringCamera != GunshipManeuveringCamera.None ? 224f : 24f;
        var box = new UIBox2(bounds.Right - margin - width,
            bounds.Bottom - bottomMargin - height,
            bounds.Right - margin,
            bounds.Bottom - bottomMargin);

        var warningColor = MalfunctionColor;
        handle.DrawRect(box, HudBackground);
        handle.DrawRect(box, warningColor, false);
        for (var i = 0; i < lines.Count; i++)
        {
            handle.DrawString(_smallFont,
                new Vector2(box.Left + padding, box.Top + padding + lineHeight * i),
                lines[i],
                i == lines.Count - 1 && hud.MasterAlarmSilenced && hud.Alarms.Count > 0 ? HudColor : warningColor);
        }
    }
}

public sealed class GunshipPilotOverlay : Overlay
{
    private readonly IEntityManager _entities;
    private readonly IPlayerManager _player;
    private readonly TagSystem _tags;
    private readonly HashSet<Vector2i> _tiles = new();
    private readonly HashSet<Vector2i> _hullTiles = new();
    private readonly HashSet<Vector2i> _wallTiles = new();
    private readonly HashSet<Vector2i> _maskTiles = new();
    private readonly HashSet<Vector2i> _selectedMaskTiles = new();
    private readonly List<Box2> _maskRectangles = new();
    private EntityUid? _cachedPreviewGrid;
    private EntityUid? _cachedTileGrid;
    private int _hullRevision;
    private int _batchedHullRevision = -1;
    private Vector2i _batchedPilotTile;
    private Vector2 _batchedForwardLocal;
    private bool _hasBatchedMask;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    private static readonly Color HullMask = Color.Black;
    private static readonly Color Fill = new(0.10f, 0.72f, 1f, 0.07f);
    private static readonly Color Edge = new(0.25f, 0.88f, 1f, 0.92f);
    private static readonly Color Heading = new(0.72f, 0.96f, 1f, 0.98f);
    private static readonly Color CollisionFill = new(1f, 0.04f, 0.02f, 0.20f);
    private static readonly Color CollisionEdge = new(1f, 0.16f, 0.08f, 0.98f);

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public GunshipPilotOverlay(IEntityManager entities, IPlayerManager player)
    {
        _entities = entities;
        _player = player;
        _tags = entities.System<TagSystem>();
    }

    public void InvalidateGrid(EntityUid? grid)
    {
        if (grid == null)
            return;

        if (_cachedPreviewGrid == grid)
            _cachedPreviewGrid = null;

        if (_cachedTileGrid == grid)
        {
            _cachedTileGrid = null;
            _hasBatchedMask = false;
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local ||
            !_entities.TryGetComponent(local, out GunshipPilotHudComponent? hud) ||
            !_entities.TryGetComponent(local, out EyeComponent? localEye) ||
            !ReferenceEquals(args.Viewport.Eye, localEye.Eye) ||
            hud.Dropship is not { } linkedDropship)
        {
            return;
        }

        if (hud.ViewOffset == 0 &&
            !hud.RearView &&
            !hud.Malfunctions.Contains(DropshipMalfunction.SensorArrayFault) &&
            _entities.TryGetComponent(linkedDropship, out TransformComponent? linkedXform) &&
            linkedXform.MapID == args.MapId &&
            _entities.TryGetComponent(linkedDropship, out MapGridComponent? linkedGrid))
        {
            DrawHullMask(args.WorldHandle, local, linkedDropship, linkedGrid);
        }

        if (hud.ViewOffset == 0 &&
            !hud.RearView &&
            _entities.TryGetComponent(linkedDropship, out DropshipIntegrityComponent? integrity) &&
            integrity.ProximityAlarmActive)
        {
            DrawCollisionHazards(args.WorldHandle, integrity.ProximityHazards);
        }

        if (!_entities.TryGetComponent(local, out EyeComponent? eye) ||
            eye.Target is not { } eyeUid ||
            !_entities.TryGetComponent(eyeUid, out GunshipPilotEyeComponent? gunshipEye) ||
            gunshipEye.ViewOffset != -1 ||
            gunshipEye.Dropship is not { } dropship ||
            !_entities.TryGetComponent(dropship, out MapGridComponent? dropshipGrid) ||
            !_entities.TryGetComponent(eyeUid, out TransformComponent? eyeXform))
        {
            return;
        }

        var transform = _entities.System<SharedTransformSystem>();
        var map = _entities.System<SharedMapSystem>();
        var center = transform.GetWorldPosition(eyeXform);
        var rotation = Angle.FromDegrees(gunshipEye.RotationDegrees);
        var handle = args.WorldHandle;

        if (_cachedPreviewGrid != dropship)
        {
            _cachedPreviewGrid = dropship;
            _tiles.Clear();
            foreach (var tile in map.GetAllTiles(dropship, dropshipGrid))
                _tiles.Add(tile.GridIndices);
        }

        var tileSize = dropshipGrid.TileSize;
        var halfTile = tileSize / 2f;
        var tileBox = Box2.CenteredAround(Vector2.Zero, new Vector2(tileSize, tileSize));

        foreach (var tile in _tiles)
        {
            var localCenter = map.TileCenterToVector(dropship, dropshipGrid, tile);
            var worldCenter = center + rotation.RotateVec(localCenter);
            handle.DrawRect(new Box2Rotated(tileBox.Translated(worldCenter), rotation, worldCenter), Fill);

            DrawBoundary(handle, tile, localCenter, rotation, center, halfTile);
        }

        var forward = rotation.RotateVec(Vector2.UnitY);
        var side = new Vector2(-forward.Y, forward.X);
        var shaftStart = center + forward * 0.65f;
        var tip = center + forward * 1.65f;
        handle.DrawLine(shaftStart, tip, Heading);
        handle.DrawLine(tip, tip - forward * 0.42f + side * 0.28f, Heading);
        handle.DrawLine(tip, tip - forward * 0.42f - side * 0.28f, Heading);
        handle.DrawCircle(center, 0.24f, Heading, false);
    }

    private void DrawHullMask(
        DrawingHandleWorld handle,
        EntityUid pilot,
        EntityUid gridUid,
        MapGridComponent grid)
    {
        var map = _entities.System<SharedMapSystem>();
        if (_cachedTileGrid != gridUid)
        {
            _cachedTileGrid = gridUid;
            _hullTiles.Clear();
            _wallTiles.Clear();
            _maskTiles.Clear();

            foreach (var tile in map.GetAllTiles(gridUid, grid))
            {
                _hullTiles.Add(tile.GridIndices);

                foreach (var anchored in map.GetAnchoredEntities(gridUid, grid, tile.GridIndices))
                {
                    if (!_entities.TryGetComponent(anchored, out TagComponent? tags) ||
                        !_tags.HasTag(tags, WallTag))
                    {
                        continue;
                    }

                    _wallTiles.Add(tile.GridIndices);
                    break;
                }
            }

            foreach (var tile in _hullTiles)
            {
                if (!_wallTiles.Contains(tile))
                    _maskTiles.Add(tile);
            }

            _hullRevision++;
        }

        if (_hullTiles.Count == 0)
            return;

        var transform = _entities.System<SharedTransformSystem>();
        var gridCenter = transform.GetWorldPosition(gridUid);
        var rotation = transform.GetWorldRotation(gridUid);
        var pilotTile = map.TileIndicesFor(gridUid, grid, transform.GetMapCoordinates(pilot));
        var pilotTileLocalCenter = map.TileCenterToVector(gridUid, grid, pilotTile);
        // Character facing uses screen-forward (-Y), rather than the gunship
        // movement convention (+Y). Mask only the hull half behind the pilot.
        var pilotForward = transform.GetWorldRotation(pilot).RotateVec(-Vector2.UnitY);
        var pilotForwardLocal = (-rotation).RotateVec(pilotForward);
        if (!_hasBatchedMask ||
            _batchedHullRevision != _hullRevision ||
            _batchedPilotTile != pilotTile ||
            Vector2.DistanceSquared(_batchedForwardLocal, pilotForwardLocal) > 0.0001f)
        {
            BuildHullMaskRectangles(gridUid, grid, pilotTileLocalCenter, pilotForwardLocal);
            _batchedHullRevision = _hullRevision;
            _batchedPilotTile = pilotTile;
            _batchedForwardLocal = pilotForwardLocal;
            _hasBatchedMask = true;
        }

        foreach (var localRect in _maskRectangles)
            handle.DrawRect(new Box2Rotated(localRect.Translated(gridCenter), rotation, gridCenter), HullMask);
    }

    private void BuildHullMaskRectangles(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2 pilotCenter,
        Vector2 pilotForwardLocal)
    {
        var map = _entities.System<SharedMapSystem>();
        var tileSize = grid.TileSize;
        _selectedMaskTiles.Clear();
        _maskRectangles.Clear();

        var minX = int.MaxValue;
        var maxX = int.MinValue;
        var minY = int.MaxValue;
        var maxY = int.MinValue;
        foreach (var tile in _maskTiles)
        {
            var center = map.TileCenterToVector(gridUid, grid, tile);
            // Put the cutoff at the rear edge of the pilot's tile. The tile
            // occupied by the pilot remains visible; masking begins one tile back.
            if (Vector2.Dot(center - pilotCenter, pilotForwardLocal) >= -tileSize * 0.5f)
                continue;

            _selectedMaskTiles.Add(tile);
            minX = Math.Min(minX, tile.X);
            maxX = Math.Max(maxX, tile.X);
            minY = Math.Min(minY, tile.Y);
            maxY = Math.Max(maxY, tile.Y);
        }

        if (_selectedMaskTiles.Count == 0)
            return;

        var halfTile = tileSize * 0.5f + 0.01f;
        for (var y = minY; y <= maxY; y++)
        {
            int? runStart = null;
            for (var x = minX; x <= maxX + 1; x++)
            {
                if (x <= maxX && _selectedMaskTiles.Contains(new Vector2i(x, y)))
                {
                    runStart ??= x;
                    continue;
                }

                if (runStart is not { } start)
                    continue;

                var startCenter = map.TileCenterToVector(gridUid, grid, new Vector2i(start, y));
                var endCenter = map.TileCenterToVector(gridUid, grid, new Vector2i(x - 1, y));
                _maskRectangles.Add(new Box2(
                    new Vector2(startCenter.X - halfTile, startCenter.Y - halfTile),
                    new Vector2(endCenter.X + halfTile, endCenter.Y + halfTile)));
                runStart = null;
            }
        }
    }

    private static void DrawCollisionHazards(DrawingHandleWorld handle, List<Vector2> hazards)
    {
        foreach (var position in hazards)
        {
            var box = Box2.CenteredAround(position, Vector2.One);
            handle.DrawRect(box, CollisionFill);
            handle.DrawRect(box, CollisionEdge, false);
            handle.DrawRect(box.Enlarged(0.08f), CollisionEdge.WithAlpha(0.45f), false);
        }
    }

    private void DrawBoundary(
        DrawingHandleWorld handle,
        Vector2i tile,
        Vector2 localCenter,
        Angle rotation,
        Vector2 worldCenter,
        float halfTile)
    {
        if (!_tiles.Contains(tile + Vector2i.Left))
            DrawLocalLine(handle, localCenter + new Vector2(-halfTile, -halfTile), localCenter + new Vector2(-halfTile, halfTile), rotation, worldCenter);

        if (!_tiles.Contains(tile + Vector2i.Right))
            DrawLocalLine(handle, localCenter + new Vector2(halfTile, -halfTile), localCenter + new Vector2(halfTile, halfTile), rotation, worldCenter);

        if (!_tiles.Contains(tile + Vector2i.Down))
            DrawLocalLine(handle, localCenter + new Vector2(-halfTile, -halfTile), localCenter + new Vector2(halfTile, -halfTile), rotation, worldCenter);

        if (!_tiles.Contains(tile + Vector2i.Up))
            DrawLocalLine(handle, localCenter + new Vector2(-halfTile, halfTile), localCenter + new Vector2(halfTile, halfTile), rotation, worldCenter);
    }

    private static void DrawLocalLine(
        DrawingHandleWorld handle,
        Vector2 localFrom,
        Vector2 localTo,
        Angle rotation,
        Vector2 worldCenter)
    {
        handle.DrawLine(
            worldCenter + rotation.RotateVec(localFrom),
            worldCenter + rotation.RotateVec(localTo),
            Edge);
    }
}
