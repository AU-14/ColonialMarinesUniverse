using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Blackfoot;
using Content.Shared.CMU14.ThreeD;
using Content.Shared.Vehicle.Components;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    [Dependency] private VehicleTurretSystem _vehicleTurrets = default!;

    private bool TryVehiclePose(EntityUid uid, out System.Numerics.Vector2 position, out Angle yaw)
    {
        position = default;
        yaw = default;
        if (!TryComp(uid, out VehicleTurretVisualComponent? visual) ||
            !TryGetEntity(visual.Turret, out var turretUid) ||
            !TryComp(turretUid, out VehicleTurretComponent? turret) ||
            !_vehicleTurrets.TryGetVehicle(turretUid.Value, out var vehicle))
            return false;
        _vehicleTurrets.TryGetAnchorTurret(turretUid.Value, turret, out var anchorUid, out var anchor);
        var (vehiclePosition, vehicleYaw) = _transform.GetWorldPositionRotation(vehicle);
        (position, yaw) = CMU3DVehiclePose.Mounted(vehiclePosition, vehicleYaw, anchor,
            anchorUid == turretUid ? null : turret);
        return true;
    }

    private CMU3DSceneMatch? VehicleTurretMatch(EntityUid uid, CMU3DSceneMatch? ordinary)
    {
        if (!TryComp(uid, out VehicleTurretVisualComponent? visual))
            return ordinary;
        if (!TryGetEntity(visual.Turret, out var turret) ||
            !TryComp(turret, out MetaDataComponent? metadata) || metadata.EntityPrototype == null)
            return null;
        return _catalog!.ResolveVehicleTurret(metadata.EntityPrototype.ID);
    }

    private bool TryVehicleParts(EntityUid uid, SpriteComponent sprite, CMU3DModelPrototype model,
        out IReadOnlyList<CMU3DModelPart> parts)
    {
        parts = [];
        if ((!HasComp<GridVehicleMoverComponent>(uid) && !HasComp<VehicleTurretVisualComponent>(uid)) ||
            sprite.Scale != model.VehicleSpriteScale ||
            // Blackfoot's takeoff offset is a screen-space lift owned by its 2D
            // visual system. Physical Z already places the 3D aircraft in space.
            sprite.Offset != model.VehicleSpriteOffset && !HasComp<BlackfootVisualsComponent>(uid) ||
            sprite.Rotation != Angle.Zero || _sprites.GetPostShaders(sprite).Count > 0)
            return false;
        var layers = new List<(string Rsi, string State)>();
        foreach (var layer in sprite.AllLayers)
        {
            if (!layer.Visible || layer.Color.A <= 0 || !layer.RsiState.IsValid && layer.Texture == null)
                continue;
            if (layer is not SpriteComponent.Layer actual || actual.ActualRsi == null ||
                actual.Texture != null || actual.Color != Color.White ||
                actual.Scale != System.Numerics.Vector2.One || actual.Offset != System.Numerics.Vector2.Zero ||
                actual.CopyToShaderParameters != null || actual.State.Name is not { } state)
                return false;
            // VehicleExactCardinalDirectionSystem rotates its 2D layers toward the eye.
            // The authored solid uses the entity's physical yaw, never that camera-facing rotation.
            layers.Add((((ISpriteLayer) actual).ActualRsi!.Path.ToString(), state));
        }
        return _catalog!.TryVehicleParts(model, layers, out parts);
    }
}
