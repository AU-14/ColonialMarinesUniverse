using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.ThreeD;
using Content.Shared.Vehicle.Components;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
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
            sprite.Scale != model.VehicleSpriteScale || sprite.Offset != model.VehicleSpriteOffset ||
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
