using System.Collections.Generic;
using System.Numerics;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Vehicle.Components;
using ClientPhysicsSystem = Robust.Client.Physics.PhysicsSystem;
using Robust.Client.Player;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Client.Physics;

namespace Content.Client.Vehicle;

public sealed partial class GridVehicleMoverSystem : EntitySystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private ClientPhysicsSystem _physics = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private SharedEyeSystem _eye = default!;

    public static readonly List<Vector2> DebugCollisionPositions = new();

    private GridVehicleMoverOverlay? _overlay;
    private VehicleHardpointDebugOverlay? _hardpointOverlay;
    private EntityUid? _lastPredictedVehicle;

    public override void Initialize()
    {
        _overlay = new GridVehicleMoverOverlay(EntityManager);
        RefreshSharedDebugFlags();
        _hardpointOverlay = new VehicleHardpointDebugOverlay(EntityManager);

        SubscribeLocalEvent<GridVehicleMoverComponent, UpdateIsPredictedEvent>(OnUpdateIsPredicted);

        _overlayManager.AddOverlay(_hardpointOverlay);
    }

    public override void Shutdown()
    {
        if (_overlay != null)
            _overlayManager.RemoveOverlay(_overlay);
        if (_hardpointOverlay != null)
            _overlayManager.RemoveOverlay(_hardpointOverlay);
    }

    public bool ToggleDebugOverlay()
    {
        if (_overlay == null)
            return false;

        _overlay.DebugEnabled = !_overlay.DebugEnabled;
        RefreshSharedDebugFlags();
        RefreshVehicleDebugOverlay();
        return _overlay.DebugEnabled;
    }

    public bool ToggleHardpointOverlay()
    {
        if (_hardpointOverlay == null)
            return false;

        _hardpointOverlay.Enabled = !_hardpointOverlay.Enabled;
        return _hardpointOverlay.Enabled;
    }

    public bool ToggleCollisionOverlay()
    {
        if (_overlay == null)
            return false;

        _overlay.CollisionsEnabled = !_overlay.CollisionsEnabled;
        RefreshSharedDebugFlags();
        RefreshVehicleDebugOverlay();
        return _overlay.CollisionsEnabled;
    }

    public bool ToggleMovementOverlay()
    {
        if (_overlay == null)
            return false;

        _overlay.MovementEnabled = !_overlay.MovementEnabled;
        RefreshSharedDebugFlags();
        RefreshVehicleDebugOverlay();
        return _overlay.MovementEnabled;
    }

    private void RefreshSharedDebugFlags()
    {
        Content.Shared.Vehicle.GridVehicleMoverSystem.CollisionDebugEnabled =
            _overlay is { DebugEnabled: true } or { CollisionsEnabled: true };
        Content.Shared.Vehicle.GridVehicleMoverSystem.MovementDebugEnabled =
            _overlay is { MovementEnabled: true };
    }

    private void RefreshVehicleDebugOverlay()
    {
        if (_overlay == null)
            return;

        var enabled = _overlay.DebugEnabled || _overlay.CollisionsEnabled || _overlay.MovementEnabled;
        var hasOverlay = _overlayManager.HasOverlay<GridVehicleMoverOverlay>();
        if (enabled && !hasOverlay)
        {
            _overlayManager.AddOverlay(_overlay);
        }
        else if (!enabled && hasOverlay)
        {
            _overlayManager.RemoveOverlay(_overlay);
        }
    }

    private void OnUpdateIsPredicted(Entity<GridVehicleMoverComponent> ent, ref UpdateIsPredictedEvent args)
    {
        if (_playerManager.LocalEntity is not { } local)
            return;

        if (!TryComp(ent.Owner, out VehicleComponent? vehicle))
            return;

        if (vehicle.Operator == local)
            args.IsPredicted = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_playerManager.LocalEntity is not { } local)
        {
            if (_lastPredictedVehicle is { } oldVehicle)
                _physics.UpdateIsPredicted(oldVehicle);
            _lastPredictedVehicle = null;
            return;
        }

        RefreshOutsideViewTarget(local);

        if (TryComp(local, out VehicleOperatorComponent? op) && op.Vehicle is { } vehicle)
        {
            if (_lastPredictedVehicle != vehicle)
            {
                if (_lastPredictedVehicle is { } oldVehicle)
                    _physics.UpdateIsPredicted(oldVehicle);

                _lastPredictedVehicle = vehicle;
                _physics.UpdateIsPredicted(vehicle);
            }
            return;
        }

        if (_lastPredictedVehicle is { } oldPredicted)
        {
            _physics.UpdateIsPredicted(oldPredicted);
            _lastPredictedVehicle = null;
        }
    }

    private void RefreshOutsideViewTarget(EntityUid local)
    {
        if (!TryComp(local, out VehicleViewToggleComponent? toggle) ||
            !toggle.IsOutside ||
            toggle.OutsideTarget is not { } outsideTarget ||
            !TryComp(local, out EyeComponent? eye) ||
            eye.Target == outsideTarget)
        {
            return;
        }

        _eye.SetTarget(local, outsideTarget, eye);
    }
}
