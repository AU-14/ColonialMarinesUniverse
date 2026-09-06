using System.Collections.Generic;
using System.Numerics;
using Content.Client.Viewport;
using Content.Shared._CMU14.Dropship.Integrity;
using Content.Shared._CMU14.Dropship.TacticalLand;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Buckle.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using GraphicsEye = Robust.Shared.Graphics.Eye;

namespace Content.Client._CMU14.Dropship.TacticalLand;

/// <summary>
/// Maintains the selected low-rate maneuvering camera feed without replacing the
/// pilot's main eye or increasing the rate of pilot HUD network updates.
/// </summary>
public sealed partial class GunshipManeuveringCameraSystem : EntitySystem
{
    private const float PreviewZoomMultiplier = 2f;
    private static readonly TimeSpan CameraStateUpdateInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan VerticalRenderInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan RearRenderInterval = TimeSpan.FromMilliseconds(300);

    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly GraphicsEye _upperEye = CreateCameraEye();
    private readonly GraphicsEye _lowerEye = CreateCameraEye();
    private readonly GraphicsEye _rearEye = CreateCameraEye();
    private readonly List<(Vector2 From, Vector2 To)> _outline = new();

    private GunshipCameraPanel? _panel;
    private GunshipManeuveringCamera _panelMode;
    private LayoutContainer? _parent;
    private EntityUid? _outlineGrid;
    private TimeSpan _nextCameraStateUpdate;

    public override void Shutdown()
    {
        HideCameras();
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } pilot ||
            !TryComp(pilot, out GunshipPilotHudComponent? hud) ||
            hud.Dropship is not { } dropship ||
            !TryComp(dropship, out DropshipIntegrityComponent? integrity) ||
            integrity.Crashing ||
            integrity.Wrecked ||
            hud.Malfunctions.Contains(DropshipMalfunction.SensorArrayFault) ||
            !TryComp(dropship, out MapGridComponent? grid) ||
            !TryComp(dropship, out TransformComponent? dropshipXform) ||
            dropshipXform.MapUid is not { } currentMap)
        {
            HideCameras();
            return;
        }

        if (hud.ManeuveringCamera == GunshipManeuveringCamera.None)
        {
            HideCameras();
            return;
        }

        if (!EnsureCamera(hud.ManeuveringCamera))
            return;

        if (_timing.CurTime < _nextCameraStateUpdate)
            return;

        _nextCameraStateUpdate = _timing.CurTime + CameraStateUpdateInterval;

        CacheOutline(dropship, grid);

        var position = _transform.GetWorldPosition(dropshipXform);
        var rotation = _transform.GetWorldRotation(dropshipXform);
        var verticalZoom = GetVerticalCameraZoom(grid);

        switch (hud.ManeuveringCamera)
        {
            case GunshipManeuveringCamera.Upper:
                if (hud.FlightControlsAvailable)
                    UpdateVerticalEye(_upperEye, _panel!, currentMap, 1, position, rotation, verticalZoom);
                else
                    _panel!.SetEye(_upperEye, false);
                break;
            case GunshipManeuveringCamera.Lower:
                if (hud.FlightControlsAvailable)
                    UpdateVerticalEye(_lowerEye, _panel!, currentMap, -1, position, rotation, verticalZoom);
                else
                    _panel!.SetEye(_lowerEye, false);
                break;
            case GunshipManeuveringCamera.Rear:
                if (TryGetRearEye(pilot, rotation, out var rearEye))
                    _panel!.SetEye(rearEye, true);
                else
                    _panel!.SetEye(_upperEye, false);
                break;
        }

        _panel?.SetOutline(_outline,
            hud.ShowDropshipOutline &&
            hud.ManeuveringCamera is GunshipManeuveringCamera.Upper or GunshipManeuveringCamera.Lower);
    }

    private bool TryGetRearEye(EntityUid pilot, Angle rotation, out IEye rearEye)
    {
        rearEye = default!;
        if (!TryComp(pilot, out BuckleComponent? buckle) ||
            buckle.BuckledTo is not { } seat ||
            !TryComp(seat, out GunshipPilotSeatComponent? pilotSeat) ||
            pilotSeat.Eye is not { } eyeEntity ||
            !TryComp(eyeEntity, out EyeComponent? eye))
        {
            return false;
        }

        // ContentEyeSystem refreshes every entity-backed eye's rotation each
        // frame. Copy the subscribed rear eye's position into a render-only eye
        // so the preview can continuously retain the dropship's rotation.
        _rearEye.Position = eye.Eye.Position;
        _rearEye.Rotation = rotation;
        _rearEye.Zoom = new Vector2(1.5f * PreviewZoomMultiplier);
        _rearEye.DrawFov = eye.Eye.DrawFov;
        _rearEye.DrawLight = eye.Eye.DrawLight;
        rearEye = _rearEye;
        return true;
    }

    private void UpdateVerticalEye(
        GraphicsEye eye,
        GunshipCameraPanel panel,
        EntityUid currentMap,
        int offset,
        Vector2 position,
        Angle rotation,
        float zoom)
    {
        if (!_zLevels.TryMapOffset(currentMap, offset, out var targetMap) ||
            !TryComp(targetMap.Value.Owner, out MapComponent? map))
        {
            eye.Position = MapCoordinates.Nullspace;
            panel.SetEye(eye, false);
            return;
        }

        eye.Position = new MapCoordinates(position, map.MapId);
        eye.Rotation = rotation;
        eye.Zoom = new Vector2(zoom);
        panel.SetEye(eye, true);
    }

    private float GetVerticalCameraZoom(MapGridComponent grid)
    {
        const float cameraWidthTiles = 300f / EyeManager.PixelsPerMeter;
        const float cameraHeightTiles = 164f / EyeManager.PixelsPerMeter;
        var widthZoom = grid.LocalAABB.Width / cameraWidthTiles;
        var heightZoom = grid.LocalAABB.Height / cameraHeightTiles;
        return MathF.Max(1f, MathF.Max(widthZoom, heightZoom) * 1.15f) * PreviewZoomMultiplier;
    }

    private void CacheOutline(EntityUid dropship, MapGridComponent grid)
    {
        if (_outlineGrid == dropship)
            return;

        _outlineGrid = dropship;
        _outline.Clear();
        var tiles = new HashSet<Vector2i>();
        foreach (var tile in _map.GetAllTiles(dropship, grid))
            tiles.Add(tile.GridIndices);

        var half = grid.TileSize * 0.5f;
        foreach (var tile in tiles)
        {
            var center = _map.TileCenterToVector(dropship, grid, tile);
            if (!tiles.Contains(tile + Vector2i.Left))
                _outline.Add((center + new Vector2(-half, -half), center + new Vector2(-half, half)));
            if (!tiles.Contains(tile + Vector2i.Right))
                _outline.Add((center + new Vector2(half, -half), center + new Vector2(half, half)));
            if (!tiles.Contains(tile + Vector2i.Down))
                _outline.Add((center + new Vector2(-half, -half), center + new Vector2(half, -half)));
            if (!tiles.Contains(tile + Vector2i.Up))
                _outline.Add((center + new Vector2(-half, half), center + new Vector2(half, half)));
        }
    }

    private bool EnsureCamera(GunshipManeuveringCamera mode)
    {
        var screen = _ui.ActiveScreen;
        if (screen == null)
            return false;

        var parent = screen.FindControl<LayoutContainer>("ViewportContainer");
        if (_parent != parent)
        {
            HideCameras();
            _parent = parent;
        }

        if (_panel != null && _panelMode == mode)
            return true;

        _panel?.Orphan();
        _panelMode = mode;
        var rear = mode == GunshipManeuveringCamera.Rear;
        var label = mode switch
        {
            GunshipManeuveringCamera.Rear => Loc.GetString("cmu-gunship-view-rear-camera"),
            GunshipManeuveringCamera.Lower => Loc.GetString("cmu-gunship-view-lower-camera-level"),
            GunshipManeuveringCamera.Upper => Loc.GetString("cmu-gunship-view-upper-camera-level"),
            _ => string.Empty,
        };
        _panel = AddPanel(parent,
            label,
            rear,
            rear ? RearRenderInterval : VerticalRenderInterval,
            TimeSpan.Zero,
            LayoutContainer.LayoutPreset.BottomRight,
            -324f,
            -24f,
            -214f,
            -24f);
        return true;
    }

    private static GunshipCameraPanel AddPanel(
        LayoutContainer parent,
        string label,
        bool renderZLevels,
        TimeSpan renderInterval,
        TimeSpan initialRenderDelay,
        LayoutContainer.LayoutPreset preset,
        float left,
        float right,
        float top,
        float bottom)
    {
        var panel = new GunshipCameraPanel(label, renderZLevels, renderInterval, initialRenderDelay);
        parent.AddChild(panel);
        LayoutContainer.SetAnchorPreset(panel, preset);
        LayoutContainer.SetMarginLeft(panel, left);
        LayoutContainer.SetMarginRight(panel, right);
        LayoutContainer.SetMarginTop(panel, top);
        LayoutContainer.SetMarginBottom(panel, bottom);
        return panel;
    }

    private void HideCameras()
    {
        _panel?.Orphan();
        _panel = null;
        _panelMode = GunshipManeuveringCamera.None;
        _parent = null;
        _nextCameraStateUpdate = TimeSpan.Zero;
    }

    private static GraphicsEye CreateCameraEye()
    {
        return new GraphicsEye
        {
            DrawFov = false,
            DrawLight = true,
            Zoom = new Vector2(1.5f * PreviewZoomMultiplier),
        };
    }
}

public sealed class GunshipCameraPanel : PanelContainer
{
    private readonly Label _label;
    private readonly GunshipCameraViewport? _camera;
    private readonly ScalingViewport? _zCamera;
    private IEye? _assignedEye;
    private bool? _available;

    public GunshipCameraPanel(
        string label,
        bool renderZLevels,
        TimeSpan renderInterval,
        TimeSpan initialRenderDelay)
    {
        MinSize = new Vector2(300f, 190f);
        MouseFilter = MouseFilterMode.Ignore;
        PanelOverride = new StyleBoxFlat(new Color(0.01f, 0.035f, 0.045f, 0.94f))
        {
            BorderColor = new Color(0.25f, 0.88f, 1f, 0.92f),
            BorderThickness = new Thickness(2f),
            ContentMarginLeftOverride = 4f,
            ContentMarginRightOverride = 4f,
            ContentMarginTopOverride = 3f,
            ContentMarginBottomOverride = 4f,
        };

        var contents = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        AddChild(contents);

        _label = new Label
        {
            Text = label,
            FontColorOverride = new Color(0.25f, 0.88f, 1f, 0.98f),
            HorizontalAlignment = HAlignment.Center,
        };
        contents.AddChild(_label);

        if (renderZLevels)
        {
            _zCamera = new ScalingViewport
            {
                AlwaysRender = true,
                HorizontalExpand = true,
                VerticalExpand = true,
                MouseFilter = MouseFilterMode.Ignore,
                RenderZLevels = true,
                ViewportSize = new Vector2i(195, 107),
                MinimumRenderInterval = renderInterval,
            };
            contents.AddChild(_zCamera);
        }
        else
        {
            _camera = new GunshipCameraViewport(renderInterval, initialRenderDelay)
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                ViewportResolution = new Vector2(0.65f),
            };
            contents.AddChild(_camera);
        }
    }

    public void SetEye(IEye eye, bool available)
    {
        if (!ReferenceEquals(_assignedEye, eye))
        {
            _assignedEye = eye;
            if (_camera?.Viewport != null)
                _camera.Viewport.Eye = eye;
            if (_zCamera != null && available)
                _zCamera.Eye = eye;
        }

        // ViewportContainer creates its internal viewport lazily. The first
        // assignment commonly happens before that creation, so repeat the
        // assignment once the viewport exists instead of leaving the vertical
        // feed permanently without an eye.
        if (_camera?.Viewport != null && !ReferenceEquals(_camera.Viewport.Eye, eye))
            _camera.Viewport.Eye = eye;

        if (_available == available)
            return;

        _available = available;
        if (_camera != null)
            _camera.SignalAvailable = available;
        if (_zCamera != null)
            _zCamera.Eye = available ? eye : null;
        _label.FontColorOverride = available
            ? new Color(0.25f, 0.88f, 1f, 0.98f)
            : new Color(1f, 0.22f, 0.12f, 0.98f);
    }

    public void SetOutline(
        IReadOnlyList<(Vector2 From, Vector2 To)> outline,
        bool enabled)
    {
        if (_camera == null)
            return;

        _camera.Outline = outline;
        _camera.DrawShipOutline = enabled;
    }
}

public sealed class GunshipCameraViewport : ViewportContainer
{
    private readonly IGameTiming _timing;
    private readonly TimeSpan _renderInterval;
    private readonly TimeSpan _initialRenderDelay;
    private TimeSpan _nextRender;
    private bool _renderScheduleInitialized;

    public bool SignalAvailable;
    public bool DrawShipOutline;
    public IReadOnlyList<(Vector2 From, Vector2 To)> Outline = Array.Empty<(Vector2, Vector2)>();

    public GunshipCameraViewport(TimeSpan renderInterval, TimeSpan initialRenderDelay)
    {
        _timing = IoCManager.Resolve<IGameTiming>();
        _renderInterval = renderInterval;
        _initialRenderDelay = initialRenderDelay;
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(IRenderHandle handle)
    {
        if (Viewport?.Eye == null)
            return;

        if (!_renderScheduleInitialized)
        {
            _renderScheduleInitialized = true;
            _nextRender = _timing.CurTime + _initialRenderDelay;
        }

        if (_timing.CurTime >= _nextRender)
        {
            _nextRender = _timing.CurTime + _renderInterval;
            Viewport.Render();
        }

        var destination = UIBox2.FromDimensions(Vector2.Zero, (Vector2i)(Viewport.Size / ViewportResolution));
        handle.DrawingHandleScreen.DrawTextureRect(Viewport.RenderTarget.Texture, destination);

        if (!SignalAvailable)
        {
            handle.DrawingHandleScreen.DrawRect(destination, new Color(0f, 0f, 0f, 0.82f));
            return;
        }

        if (!DrawShipOutline)
            return;

        var color = new Color(0.25f, 0.88f, 1f, 0.95f);
        var center = destination.Center;
        var scale = new Vector2(
            EyeManager.PixelsPerMeter / Viewport.Eye.Zoom.X / ViewportResolution.X,
            EyeManager.PixelsPerMeter / Viewport.Eye.Zoom.Y / ViewportResolution.Y);
        foreach (var (from, to) in Outline)
        {
            var localFrom = center + new Vector2(from.X * scale.X, -from.Y * scale.Y);
            var localTo = center + new Vector2(to.X * scale.X, -to.Y * scale.Y);
            handle.DrawingHandleScreen.DrawLine(localFrom, localTo, color);
        }
    }
}
