using System.Numerics;
using Content.Shared.Input;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;

namespace Content.Client.Movement.Systems;

// CMU14 - Start: mouse-driven camera rotation used by gunship piloting.
public sealed partial class CameraMouseRotationSystem : EntitySystem
{
    private const float DegreesPerPixel = 0.15f;
    private static readonly TimeSpan ReleaseProtectionTime = TimeSpan.FromMilliseconds(100);

    [Dependency] private IInputManager _input = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IPlacementManager _placement = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedMoverController _mover = default!;

    private bool _rotating;
    private bool _externalMouse;
    private bool _relativeLook;
    private EntityUid? _lookEntity;
    private bool _pendingSync;
    private bool _rotationChanged;
    private bool _awaitingServerRotation;
    private bool _placementRotateEnabled;
    private Vector2 _lastMousePosition;
    private Angle _targetRotation;
    private TimeSpan _releaseProtectionUntil;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.RotateCameraWithMouse,
                InputCmdHandler.FromDelegate(_ => StartRotating(), _ =>
                {
                    if (!_externalMouse) StopRotating();
                }, handle: true))
            .Register<CameraMouseRotationSystem>();

        _placement.PlacementChanged += OnPlacementChanged;
        SubscribeNetworkEvent<CameraMouseRotationAckEvent>(OnCameraRotationAcknowledged);
        UpdatePlacementRotateBinding();
    }

    public override void Shutdown()
    {
        StopRotating();
        _placement.PlacementChanged -= OnPlacementChanged;
        SetPlacementRotateBinding(false);
        CommandBinds.Unregister<CameraMouseRotationSystem>();
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_rotating && !_clyde.IsFocused)
        {
            StopRotating();
            return;
        }

        if (_rotating && !_externalMouse)
        {
            var mouse = _input.MouseScreenPosition;
            if (!mouse.IsValid)
            {
                StopRotating();
                return;
            }

            var relativeX = mouse.Position.X - _lastMousePosition.X;
            _lastMousePosition = mouse.Position;
            ApplyMouseDelta(relativeX);
        }

        if (!_relativeLook && (_rotating || _awaitingServerRotation || _timing.CurTime < _releaseProtectionUntil))
        {
            if (_player.LocalEntity is not { } local ||
                !_mover.SetCameraRotation(local, _targetRotation, immediate: true))
            {
                _awaitingServerRotation = false;
                _releaseProtectionUntil = TimeSpan.Zero;
                StopRotating();
                return;
            }
        }

    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Predictive events are sequenced and replayed with movement input. Coalesce
        // frame-rate mouse samples into at most one camera update per prediction tick.
        if (_timing.IsFirstTimePredicted)
            TrySyncRotation();
    }

    /// <summary>Also used by the local first-person viewport's look gesture.</summary>
    public void StartRotating()
    {
        if (_rotating ||
            _player.LocalEntity is not { } local ||
            !TryComp(local, out InputMoverComponent? mover) ||
            _mover.CameraRotationLocked)
        {
            return;
        }

        var mouse = _input.MouseScreenPosition;
        if (!mouse.IsValid || !_clyde.IsFocused)
            return;

        _relativeLook = false;
        _lookEntity = local;
        _targetRotation = mover.TargetRelativeRotation;
        _rotationChanged = false;
        _awaitingServerRotation = false;
        _releaseProtectionUntil = TimeSpan.Zero;
        _lastMousePosition = mouse.Position;
        _rotating = true;
    }

    public void StopRotating()
    {
        _externalMouse = false;
        if (!_rotating)
            return;

        _rotating = false;

        if (_rotationChanged)
        {
            _awaitingServerRotation = true;
            _pendingSync = true;
        }
    }

    public void StartRelativeLook()
    {
        StartRotating();
        _externalMouse = _rotating;
        _relativeLook = _externalMouse;
    }

    public void RelativeLook(float pixels)
    {
        if (!_externalMouse) return;
        _relativeLook = true;
        _lookEntity = _player.LocalEntity;
        ApplyMouseDelta(pixels);
    }

    /// <summary>Newest local look sample, independent of network state rollback between prediction ticks.</summary>
    public bool TryGetRelativeLookRotation(EntityUid actor, out Angle rotation)
    {
        rotation = _targetRotation;
        return _relativeLook && _lookEntity == actor && _player.LocalEntity == actor && !_mover.CameraRotationLocked &&
            (_rotating || _awaitingServerRotation || _timing.CurTime < _releaseProtectionUntil);
    }

    private void ApplyMouseDelta(float relativeX)
    {
        if (!_rotating ||
            relativeX == 0f ||
            _mover.CameraRotationLocked ||
            _player.LocalEntity is not { } local ||
            !HasComp<InputMoverComponent>(local))
        {
            return;
        }

        // Match the existing CameraRotateLeft/Right direction: dragging left
        // turns the camera left, while dragging right turns it right.
        var delta = Angle.FromDegrees(-relativeX * DegreesPerPixel);
        _targetRotation = (_targetRotation + delta).Reduced();
        _rotationChanged = true;
        // Relative first-person look is presentation on render frames. Its predictive
        // event applies the same heading to movement on the next simulation tick.
        if (!_externalMouse && !_mover.SetCameraRotation(local, _targetRotation, immediate: true))
        {
            StopRotating();
            return;
        }

        _pendingSync = true;
    }

    private void OnCameraRotationAcknowledged(CameraMouseRotationAckEvent args)
    {
        if (!_awaitingServerRotation || !double.IsFinite(args.Radians))
            return;

        var acknowledged = new Angle(args.Radians).Reduced();
        if (!Angle.ShortestDistance(acknowledged, _targetRotation).EqualsApprox(Angle.Zero))
            return;

        _awaitingServerRotation = false;
        _releaseProtectionUntil = _timing.CurTime + ReleaseProtectionTime;
    }

    private void OnPlacementChanged(object? sender, EventArgs args)
    {
        UpdatePlacementRotateBinding();
    }

    private void UpdatePlacementRotateBinding()
    {
        SetPlacementRotateBinding(_placement.IsActive && !_placement.Eraser);
    }

    private void SetPlacementRotateBinding(bool enabled)
    {
        if (_placementRotateEnabled == enabled)
            return;

        var common = _input.Contexts.GetContext("common");
        if (enabled)
            common.AddFunction(EngineKeyFunctions.EditorRotateObject);
        else
            common.RemoveFunction(EngineKeyFunctions.EditorRotateObject);

        _placementRotateEnabled = enabled;
    }

    private void TrySyncRotation()
    {
        if (!_pendingSync ||
            _player.LocalEntity is not { } local ||
            !HasComp<InputMoverComponent>(local))
        {
            return;
        }

        _pendingSync = false;
        RaisePredictiveEvent(new CameraMouseRotationEvent(_targetRotation.Theta));
    }
}
// CMU14 - End
