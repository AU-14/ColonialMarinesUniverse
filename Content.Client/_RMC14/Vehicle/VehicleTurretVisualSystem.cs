using System;
using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Vehicle;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Vehicle;

public sealed partial class VehicleTurretVisualSystem : EntitySystem
{
    private const float PixelsPerMeter = 32f;

    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleTurretSystem _turretSystem = default!;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(VehicleTurretSystem));
        SubscribeLocalEvent<VehicleTurretVisualComponent, ComponentInit>(OnVisualInit);
        SubscribeLocalEvent<VehicleTurretVisualComponent, AfterAutoHandleStateEvent>(OnVisualState);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<VehicleTurretVisualComponent>();
        while (query.MoveNext(out var uid, out var visual))
        {
            if (!TryGetEntity(visual.Turret, out var turretUid))
                continue;

            if (!TryComp(turretUid, out VehicleTurretComponent? turret))
                continue;

            if (!TryComputeRenderedTransform((EntityUid) turretUid,
                    turret,
                    out _,
                    out _,
                    out var localOffset,
                    out var localRotation))
            {
                continue;
            }

            var visualXform = Transform(uid);
            visualXform.ActivelyLerping = false;
            _transform.SetLocalRotationNoLerp(uid, localRotation, visualXform);
            _transform.SetLocalPositionNoLerp(uid, localOffset, visualXform);
        }
    }

    public bool TryGetRenderedPose(EntityUid turretUid, out EntityCoordinates origin, out Angle worldRotation)
    {
        origin = default;
        worldRotation = Angle.Zero;

        if (!TryComp(turretUid, out VehicleTurretComponent? turret))
            return false;

        if (!TryComputeRenderedTransform(turretUid,
                turret,
                out var vehicle,
                out var vehicleRotation,
                out var localOffset,
                out var localRotation))
        {
            return false;
        }

        origin = _transform.GetMoverCoordinates(new EntityCoordinates(vehicle, localOffset));
        worldRotation = (vehicleRotation + localRotation).Reduced();
        return true;
    }

    private void OnVisualInit(Entity<VehicleTurretVisualComponent> ent, ref ComponentInit args)
    {
        UpdateVisual(ent);
    }

    private void OnVisualState(Entity<VehicleTurretVisualComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisual(ent);
    }

    private void UpdateVisual(Entity<VehicleTurretVisualComponent> ent)
    {
        if (!TryComp(ent.Owner, out SpriteComponent? sprite))
            return;

        if (!TryGetEntity(ent.Comp.Turret, out var turretUid))
            return;

        if (TryComp(turretUid, out VehicleTurretComponent? turret) &&
            !string.IsNullOrWhiteSpace(turret.OverlayState))
        {
            SetOverlayDepth(ent.Owner, (EntityUid)turretUid, sprite);
            var overlayState = turret.OverlayState;
            if (!string.IsNullOrWhiteSpace(turret.OverlayRsi))
                _sprite.LayerSetRsi((ent.Owner, sprite), 0, new ResPath(turret.OverlayRsi), overlayState);
            else
                _sprite.LayerSetRsiState((ent.Owner, sprite), 0, overlayState);

            _sprite.LayerSetVisible((ent.Owner, sprite), 0, true);
            return;
        }

        if (!TryComp(turretUid, out SpriteComponent? turretSprite))
            return;

        if (turretSprite.BaseRSI == null || !turretSprite.AllLayers.Any())
            return;

        SetOverlayDepth(ent.Owner, (EntityUid)turretUid, sprite);
        var state = _sprite.LayerGetRsiState(((EntityUid)turretUid, turretSprite), 0).ToString();
        _sprite.LayerSetRsi((ent.Owner, sprite), 0, turretSprite.BaseRSI);
        _sprite.LayerSetRsiState((ent.Owner, sprite), 0, state);
        _sprite.LayerSetVisible((ent.Owner, sprite), 0, true);
    }

    private void SetOverlayDepth(EntityUid overlayUid, EntityUid turretUid, SpriteComponent sprite)
    {
        var depth = (int) DrawDepth.OverMobs;
        if (HasComp<VehicleTurretAttachmentComponent>(turretUid))
            depth += 1;

        if (sprite.DrawDepth != depth)
            _sprite.SetDrawDepth((overlayUid, sprite), depth);
    }

    private bool TryComputeRenderedTransform(
        EntityUid turretUid,
        VehicleTurretComponent turret,
        out EntityUid vehicle,
        out Angle vehicleRot,
        out Vector2 localOffset,
        out Angle localRotation)
    {
        vehicle = default;
        vehicleRot = Angle.Zero;
        localOffset = Vector2.Zero;
        localRotation = Angle.Zero;

        if (!_turretSystem.TryGetVehicle(turretUid, out vehicle))
            return false;

        _turretSystem.TryGetAnchorTurret(turretUid, turret, out var anchorUid, out var anchorTurret);

        vehicleRot = _transform.GetWorldRotation(vehicle);
        var eyeRot = _eye.CurrentEye.Rotation;
        var baseFacingAngle = _turretSystem.GetVehicleFacingAngle(vehicle, vehicleRot);
        var anchorFacingAngle = GetRenderFacing(anchorTurret, anchorTurret, vehicleRot, baseFacingAngle, eyeRot);
        var anchorPixelOffset = _turretSystem.GetPixelOffset(anchorTurret, anchorFacingAngle) / PixelsPerMeter;
        var anchorLocalOffset = GetVehicleLocalOffset(anchorTurret, anchorPixelOffset, vehicleRot, eyeRot);

        var targetLocalRotation = anchorTurret.RotateToCursor ? anchorTurret.WorldRotation : Angle.Zero;
        localOffset = anchorLocalOffset;
        localRotation = targetLocalRotation;

        if (anchorUid == turretUid)
            return true;

        var turretFacingAngle = GetRenderFacing(turret, anchorTurret, vehicleRot, baseFacingAngle, eyeRot);
        var worldOffset = _turretSystem.GetPixelOffset(turret, turretFacingAngle) / PixelsPerMeter;
        Vector2 turretLocalOffset;

        if (turret.OffsetRotatesWithTurret)
        {
            if (turret.UseDirectionalOffsets)
            {
                var dir = VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(turretFacingAngle);
                var snappedAngle = dir.ToAngle();
                turretLocalOffset = (targetLocalRotation - snappedAngle).RotateVec(worldOffset);
            }
            else
            {
                turretLocalOffset = targetLocalRotation.RotateVec(worldOffset);
            }
        }
        else
        {
            turretLocalOffset = GetVehicleLocalOffset(turret, worldOffset, vehicleRot, eyeRot);
        }

        localOffset += turretLocalOffset;
        return true;
    }

    private Angle GetRenderFacing(
        VehicleTurretComponent turret,
        VehicleTurretComponent anchorTurret,
        Angle vehicleRot,
        Angle baseFacingAngle,
        Angle eyeRot)
    {
        return (_turretSystem.GetOffsetFacing(turret, anchorTurret, vehicleRot, baseFacingAngle) + eyeRot).Reduced();
    }

    private static Vector2 GetVehicleLocalOffset(
        VehicleTurretComponent turret,
        Vector2 offset,
        Angle vehicleRot,
        Angle eyeRot)
    {
        if (turret.UseDirectionalOffsets)
            offset = (-eyeRot).RotateVec(offset);

        return (-vehicleRot).RotateVec(offset);
    }

}
