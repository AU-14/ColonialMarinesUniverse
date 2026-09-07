using System.Numerics;
using Content.Shared.CMU14.DroneOperator;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.DroneOperator;

public sealed class CMUCombatDroneTurretSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private TimeSpan _nextAim;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(TransformSystem));
        UpdatesAfter.Add(typeof(EyeSystem));
    }

    public override void Update(float frameTime)
    {
        if (!_timing.IsFirstTimePredicted || _timing.CurTime < _nextAim || !_input.MouseScreenPosition.IsValid ||
            _player.LocalEntity is not { } uid || !TryComp<CMUCombatDroneComponent>(uid, out var drone) ||
            drone.Wrecked || drone.TurretVisual is not { } turret || TerminatingOrDeleted(turret))
            return;

        var cursor = _eye.PixelToMap(_input.MouseScreenPosition);
        var origin = _transform.GetMapCoordinates(uid);
        var direction = cursor.Position - origin.Position;
        if (cursor.MapId != origin.MapId || direction.LengthSquared() < 0.0001f)
            return;

        _nextAim = _timing.CurTime + TimeSpan.FromMilliseconds(50);
        var angle = CMUCombatDroneSystem.ClampAim(_transform.GetWorldRotation(uid), Angle.FromWorldVec(direction), drone.FireArcDegrees);
        if (Math.Abs(Angle.ShortestDistance(angle, _transform.GetWorldRotation(turret)).Degrees) >= 1)
            RaisePredictiveEvent(new CMUCombatDroneAimEvent(angle));
    }

    public override void FrameUpdate(float frameTime)
    {
        var drones = EntityQueryEnumerator<CMUCombatDroneComponent>();
        while (drones.MoveNext(out var uid, out var drone))
        {
            if (drone.TurretVisual is not { } turret || TerminatingOrDeleted(turret))
                continue;

            // Only the sprite offset positions the mount; the independently aiming entity
            // must retain the hull's origin even if its replicated position has drifted.
            _transform.SetLocalPositionNoLerp(turret, Vector2.Zero);
            var hullDirection = DirectionIndex(_transform.GetWorldRotation(uid) + _eye.CurrentEye.Rotation);
            _sprite.SetOffset(turret, drone.TurretMountOffsets[hullDirection]);
        }

        var flashes = EntityQueryEnumerator<CMUCombatDroneMuzzleFlashComponent>();
        while (flashes.MoveNext(out var uid, out var flash))
            UpdateMuzzleFlash((uid, flash));
    }

    /// <summary>Finds the rendered barrel tip, including sprite height and camera rotation.</summary>
    public bool TryGetMuzzlePose(EntityUid uid, out Vector2 position, out Angle rotation)
    {
        position = default;
        rotation = default;
        if (!TryComp<CMUCombatDroneComponent>(uid, out var drone) || drone.Wrecked ||
            drone.TurretVisual is not { } turret || TerminatingOrDeleted(turret))
            return false;

        var camera = _eye.CurrentEye.Rotation;
        var hullIndex = DirectionIndex(_transform.GetWorldRotation(uid) + camera);
        var turretRotation = _transform.GetWorldRotation(turret);
        var turretIndex = DirectionIndex(turretRotation + camera);
        var screenOffset = drone.TurretMountOffsets[hullIndex] + drone.TurretMuzzleOffsets[turretIndex];
        position = _transform.GetWorldPosition(uid) + (-camera).RotateVec(screenOffset);
        rotation = turretRotation - Angle.FromDegrees(90);
        return true;
    }

    public bool AttachMuzzleFlash(EntityUid effect, EntityUid drone, Angle shotAngle)
    {
        if (!TryGetMuzzlePose(drone, out _, out var rotation))
            return false;

        var flash = EnsureComp<CMUCombatDroneMuzzleFlashComponent>(effect);
        flash.Drone = drone;
        flash.RotationOffset = shotAngle - rotation;
        UpdateMuzzleFlash((effect, flash));
        return true;
    }

    private void UpdateMuzzleFlash(Entity<CMUCombatDroneMuzzleFlashComponent> ent)
    {
        if (!TryGetMuzzlePose(ent.Comp.Drone, out var position, out var rotation))
            return;

        var transform = Transform(ent);
        transform.ActivelyLerping = false;
        _transform.SetWorldRotationNoLerp((ent, transform), rotation + ent.Comp.RotationOffset);
        _transform.SetWorldPosition((ent, transform), position);
    }

    // Use the renderer's four-way direction bias, including at diagonal boundaries.
    private static int DirectionIndex(Angle angle) =>
        (int) SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, angle.Reduced().FlipPositive());
}
