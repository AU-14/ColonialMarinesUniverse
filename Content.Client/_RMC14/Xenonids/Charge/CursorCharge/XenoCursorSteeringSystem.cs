// Content.Client/_RMC14/Xenonids/Charge/CursorCharge/XenoCursorSteeringClientSystem.cs

using System.Numerics;
using Content.Shared._RMC14.Xenonids.Charge.CursorCharge;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client._RMC14.Xenonids.Charge.CursorCharge;

public sealed partial class XenoCursorSteeringSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private Angle _lastSentAngle;
    private XenoCursorSteeringOverlay? _overlay;
    public Vector2 CursorWorldPosition { get; private set; }

    private const float MinCursorDistanceSq = 0.01f; // ~0.1 units from player
    private const float MinAngleDeltaRadians = 0.02f;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoChargerComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<XenoChargerComponent, LocalPlayerDetachedEvent>(OnDetached);
    }

    private void OnAttached(Entity<XenoChargerComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        _lastSentAngle = default;
        _overlay = new XenoCursorSteeringOverlay(EntityManager);
        _overlayManager.AddOverlay(_overlay);
    }

    private void OnDetached(Entity<XenoChargerComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        _lastSentAngle = default;
        if (_overlay != null)
        {
            _overlayManager.RemoveOverlay(_overlay);
            _overlay = null;
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_player.LocalEntity is not { } controlled)
            return;

        var screenPos = _input.MouseScreenPosition;
        var mapCoords = _eye.PixelToMap(screenPos);

        if (mapCoords.MapId == MapId.Nullspace)
            return;
        // Always cache cursor position
        CursorWorldPosition = mapCoords.Position;

        // Only send network messages while actively charging
        if (!TryComp(controlled, out XenoChargerStateComponent? state) || state.MoveState == XenoChargerMoveState.Idle)
            return;

        var xenoPos = _transform.GetMapCoordinates(controlled).Position;
        var diff = mapCoords.Position - xenoPos;
        if (diff.LengthSquared() < MinCursorDistanceSq)
            return;

        var newAngle = diff.ToAngle();
        if (Math.Abs((newAngle - _lastSentAngle).Reduced().Theta) < MinAngleDeltaRadians)
            return;

        _lastSentAngle = newAngle;
        RaiseNetworkEvent(new XenoCursorSteeringMessage { CursorWorldPosition = mapCoords.Position });
    }
}
