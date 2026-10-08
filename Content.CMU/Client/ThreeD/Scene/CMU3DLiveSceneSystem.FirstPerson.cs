using System.Numerics;
using Content.Client.Movement.Systems;
using Content.Shared.Camera;
using Content.Client.UserInterface.Controls;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private CameraMouseRotationSystem _cameraRotation = default!;
    [Dependency] private IConfigurationManager _viewConfig = default!;
    [Dependency] private IClyde _viewClyde = default!;
    [Dependency] private IInputManager _viewInput = default!;
    [Dependency] private IEyeManager _viewEyes = default!;
    private IViewportControl? _previousViewport;
    private bool _captureRequested;
    private bool _captureRequestedFromUi;
    private string? _captureWaitReason;
    private int _captureMotionEvents;
    private float ViewRadius => _firstPersonView == null ? Radius :
        CMU3DViewSettings.Clamp(_viewConfig.GetCVar(CMU3DViewSettings.Distance), 4, 24, CMU3DViewSettings.DefaultDistance);
    private float SampleRadius => ViewRadius + (_firstPersonView == null ? 0 : 6);

    private Vector2 SampleOrigin(Vector2 position)
    {
        if (_firstPersonView == null) return position;
        // Keep four tiles of movement inside an existing snapshot, with another two
        // tiles buffered while its replacement is assembled. Stay inside packed XY bounds.
        if (_actor != null && MathF.Abs(position.X - _sceneOrigin.X) <= 4 &&
            MathF.Abs(position.Y - _sceneOrigin.Y) <= 4)
            return _sceneOrigin;
        return new Vector2(MathF.Floor(position.X) + .5f, MathF.Floor(position.Y) + .5f);
    }
    private int ScenePartLimit => _firstPersonView == null ? PartLimit : CMU3DSceneEncoding.ExtendedMaxBoxes;
    private int _mapPartLimit = PartLimit;
    private int ViewPartLimit => _mapPartLimit;
    private CMU3DSceneControl? _firstPersonView;
    private MainViewport? _firstPersonHost;
    private bool _worldWasVisible;
    private Vector2 _sceneOrigin;
    private CMU3DSceneControl? View => _firstPersonView ?? _window?.View;

    public bool ToggleFirstPerson()
    {
        if (_firstPersonView == null)
            return OpenFirstPerson();
        Close();
        return true;
    }

    public bool OpenFirstPerson()
    {
        if (!TryContext(out _, out _, firstPerson: true) || _ui.ActiveScreen?.GetWidget<MainViewport>() is not { } viewport)
            return false;
        if (_firstPersonView != null)
            return true;
        Close();
        _firstPersonHost = viewport;
        _worldWasVisible = viewport.Viewport.Visible;
        _firstPersonView = new CMU3DSceneControl(true)
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MapProjection = viewport.Viewport,
        };
        _firstPersonView.LookStarted += _cameraRotation.StartRelativeLook;
        _firstPersonView.LookEnded += _cameraRotation.StopRotating;
        _firstPersonView.LookDelta += OnFirstPersonLookDelta;
        // MainViewport is below the HUD and windows in both existing screen layouts.
        // Preserve its eye and input owners; the replacement is presentation only.
        viewport.AddChild(_firstPersonView);
        viewport.Viewport.Visible = false;
        _previousViewport = _viewEyes.MainViewport;
        _viewEyes.MainViewport = _firstPersonView;
        RequestMouseCapture(false);
        _actor = null;
        _untilRefresh = 0;
        Refresh();
        UpdateFirstPersonCamera();
        ShowCaptureControls();
        Log.Info($"First-person 3D enabled: {_boxes.Count} scene solids, {_spriteEntities.Count} sprite candidates, {ViewRadius} tile radius.");
        return true;
    }

    private void CloseFirstPerson()
    {
        if (_firstPersonView == null)
            return;
        CloseCaptureControls();
        ClearGeometryCache();
        _viewConfig.SetCVar(CMU3DViewSettings.Enabled, false);
        SetMouseCapture(false, "first-person view closed");
        if (_viewEyes.MainViewport == _firstPersonView && _previousViewport != null)
            _viewEyes.MainViewport = _previousViewport;
        _previousViewport = null;
        _firstPersonView.ReleaseResources();
        _firstPersonView.Orphan();
        _firstPersonView.Dispose();
        _firstPersonView = null;
        UpdateSubscription(0);
        if (_firstPersonHost != null)
            _firstPersonHost.Viewport.Visible = _worldWasVisible;
        _firstPersonHost = null;
        _captureRequested = false;
        _captureRequestedFromUi = false;
        _captureWaitReason = null;
        _actor = null;
        _boxes.Clear();
        _animatedSprites.Clear();
    }

    private void UpdateFirstPersonCamera()
    {
        if (_firstPersonView is not { } view)
            return;
        if (_ui.ActiveScreen?.GetWidget<MainViewport>() != _firstPersonHost ||
            !TryContext(out var actor, out var xform))
        {
            Close();
            return;
        }
        var rotation = TryComp(actor, out InputMoverComponent? mover)
            ? (float) _mover.GetParentGridAngle(mover).Theta
            : 0;
        if (mover != null && _cameraRotation.TryGetRelativeLookRotation(actor, out var localLook))
        {
            var parent = TryComp(mover.RelativeEntity, out TransformComponent? relative)
                ? _transform.GetWorldRotation(relative) : Angle.Zero;
            rotation = (float) (parent + localLook).Theta;
        }
        var blocker = _captureControls is { IsOpen: true } ? "capture controls are open" :
            MouseCaptureBlocker(_viewClyde.IsFocused, _ui.KeyboardFocused, _ui.ModalRoot.Children);
        var escapeDown = _viewInput.IsKeyDown(Keyboard.Key.Escape);
        var capture = NextMouseCapture(view.MouseCaptured, blocker == null, escapeDown,
            _captureRequestedFromUi, ref _captureRequested);
        var reason = escapeDown ? "Escape is held" : blocker;
        SetMouseCapture(capture, reason ?? "capture request ready");
        if (_captureRequested && reason != _captureWaitReason)
        {
            Log.Debug($"First-person mouse capture pending: {reason}.");
            _captureWaitReason = reason;
        }
        if (!_captureRequested)
        {
            _captureRequestedFromUi = false;
            _captureWaitReason = null;
        }
        if (view.MouseCaptured)
            _ui.ControlFocused = view;
        view.SceneOrigin = _sceneOrigin;
        view.SceneMap = xform.MapID;
        view.VisibleRadius = ViewRadius;
        view.Brightness = CMU3DViewSettings.Clamp(_viewConfig.GetCVar(CMU3DViewSettings.Brightness), .25f, 2, 1);
        view.RenderScale = _viewConfig.GetCVar(CMU3DViewSettings.Resolution);
        view.PixelBudget = _viewConfig.GetCVar(CMU3DViewSettings.PixelBudget);
        if (view.SceneRadius != SampleRadius)
        {
            view.SceneRadius = SampleRadius;
            _untilRefresh = 0;
        }
        // Geometry is sampled at 10 Hz; camera translation follows the current predicted actor each frame.
        var pitch = view.FirstPersonPitch;
        if (TryComp(actor, out CameraRecoilComponent? recoil))
        {
            // Reuse the predicted kick and its recovery, including the user's screen-shake preference.
            // This never writes into the mover's persistent look rotation.
            var right = new Vector2(MathF.Cos(rotation), MathF.Sin(rotation));
            var forward = new Vector2(-right.Y, right.X);
            rotation += Vector2.Dot(recoil.CurrentKick, right) * .08f;
            pitch -= Vector2.Dot(recoil.CurrentKick, forward) * .08f;
        }
        view.SetCameraOverride(CMU3DFirstPersonCamera.Frame(_transform.GetWorldPosition(xform) - _sceneOrigin,
            rotation, pitch, view.PixelSize, _viewConfig.GetCVar(CMU3DViewSettings.Fov),
            _elevation.CameraHeight(actor, view.SceneDepth)));
    }

    public bool ToggleMouseCapture()
    {
        if (_firstPersonView == null) return false;
        return RequestMouseCapture(!_firstPersonView.MouseCaptured && !_captureRequested);
    }

    /// <summary>Wait for console/chat to close before entering relative mode.</summary>
    public bool RequestMouseCapture(bool capture)
    {
        if (_firstPersonView == null) return false;
        _captureRequested = capture;
        // Escape also closes the console/chat. Remember the context of this request so that
        // closing that UI does not cancel the capture it just requested.
        _captureRequestedFromUi = capture && !CanCaptureMouse(true, _ui.KeyboardFocused, _ui.ModalRoot.Children);
        _captureWaitReason = null;
        if (!capture) SetMouseCapture(false, "capture explicitly disabled");
        return true;
    }

    /// <summary>Preserve a UI-issued request through its closing Escape until the key is released.</summary>
    public static bool NextMouseCapture(bool captured, bool canCapture, bool escapeDown,
        bool requestedFromUi, ref bool requested)
    {
        if (escapeDown)
        {
            if (captured || !requestedFromUi)
                requested = false;
            return false;
        }
        if (!canCapture)
            return false;
        if (!requested)
            return captured;
        requested = false;
        return true;
    }

    public static bool CanCaptureMouse(bool windowFocused, Control? keyboardFocused, IEnumerable<Control> modals) =>
        MouseCaptureBlocker(windowFocused, keyboardFocused, modals) == null;

    public static string? MouseCaptureBlocker(bool windowFocused, Control? keyboardFocused, IEnumerable<Control> modals)
    {
        if (!windowFocused) return "game window is not focused";
        if (keyboardFocused is { VisibleInTree: true }) return $"keyboard focus belongs to {keyboardFocused.GetType().Name}";
        // Cached popups remain attached to ModalRoot after Close(); only displayed UI blocks look.
        foreach (var modal in modals)
        {
            if (modal.VisibleInTree) return $"visible modal {modal.GetType().Name}";
        }
        return null;
    }

    private void SetMouseCapture(bool capture, string reason)
    {
        if (_firstPersonView is not { } view || view.MouseCaptured == capture) return;
        _viewClyde.MainWindow.SetRelativeMouseMode(capture);
        view.SetMouseCaptured(capture);
        if (capture)
        {
            _captureMotionEvents = 0;
            _ui.ControlFocused = view;
            Log.Debug($"First-person mouse capture enabled: {reason}.");
        }
        else
        {
            Log.Debug($"First-person mouse capture released: {reason}; {_captureMotionEvents} relative motion events received.");
            if (_ui.ControlFocused == view) _ui.ControlFocused = null;
        }
    }

    private void OnFirstPersonLookDelta(float pixels)
    {
        if (_captureMotionEvents++ == 0)
            Log.Debug($"First-person relative look input received; camera rotation locked: {_mover.CameraRotationLocked}.");
        _cameraRotation.RelativeLook(pixels);
    }

    // Used by continuous gun/melee input as well as discrete viewport clicks.
    public bool TryFirstPersonAim(out MapCoordinates coordinates, out EntityUid? target)
    {
        coordinates = default;
        target = null;
        if (_firstPersonView is not { } view) return false;
        if (view.MouseCaptured || _ui.MouseGetControl(_viewInput.MouseScreenPosition) == view)
            coordinates = view.Aim(_viewInput.MouseScreenPosition.Position, out target);
        return true;
    }
}
