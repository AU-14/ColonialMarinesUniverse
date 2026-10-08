using System.Numerics;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.CMU14.ThreeD;
using Robust.Client.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map.Components;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    private bool TryReagentTankParts(EntityUid uid, SpriteComponent sprite, CMU3DModelPrototype model,
        out IReadOnlyList<CMU3DModelPart> parts, out string key)
    {
        parts = [];
        key = string.Empty;
        if (!HasComp<SolutionContainerVisualsComponent>(uid) || !sprite.NoRotation || sprite.GranularLayersRendering ||
            _sprites.GetPostShaders(sprite).Count > 0 ||
            sprite.Offset != Vector2.Zero || sprite.Scale != Vector2.One || sprite.Rotation != Angle.Zero ||
            !CMU3DReagentTankAppearance.ValidSpriteColor(sprite.Color) ||
            !_sprites.LayerMapTryGet((uid, sprite), SolutionContainerLayers.Fill, out var fillIndex, false))
            return false;
        List<CMU3DButtonLayer> layers = [];
        foreach (var layer in sprite.AllLayers)
        {
            if (layers.Count >= 4 || layer is not SpriteComponent.Layer actual || actual.CopyToShaderParameters != null ||
                actual.ActualState is not { RsiDirections: RsiDirectionType.Dir1, DelayCount: 1 })
                return false;
            var sample = Snapshot(actual);
            if (actual.DirOffset != SpriteComponent.DirectionOffset.None)
                sample = sample with { IdentityTransform = false };
            layers.Add(sample);
        }
        return CMU3DReagentTankAppearance.TryParts(model, layers, fillIndex, out parts, out key);
    }

    private float ReagentTankFloorOffset(EntityUid uid, CMU3DModelPrototype model)
    {
        if (model.ReagentTankAppearance == null || !TryComp(uid, out TransformComponent? xform) ||
            !TryComp(xform.GridUid, out MapGridComponent? grid))
            return 0;
        var position = _transform.GetWorldPosition(xform);
        var tile = _map.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
        var best = float.PositiveInfinity;
        foreach (var other in _map.GetAnchoredEntities(xform.GridUid.Value, grid, tile))
        {
            if (other == uid || !TryComp(other, out MetaDataComponent? meta) || meta.EntityPrototype is not { } prototype ||
                prototype.ID is not ("CMCatwalk" or "CMCatwalkPrison" or "RMCCatwalkHybrisaElevator") ||
                _catalog?.Resolve(prototype.ID) is not { Exact: true } match || !TryComp(other, out SpriteComponent? sprite) ||
                !sprite.Visible || sprite.Offset != Vector2.Zero || sprite.Scale != Vector2.One || sprite.Rotation != Angle.Zero)
                continue;
            var yaw = CMU3DSceneLayout.RenderYaw(match.Model, (float) _transform.GetWorldRotation(other).Theta,
                sprite.NoRotation, sprite.SnapCardinals);
            var offset = CMU3DReagentTankPlacement.Offset(model, match.Model, prototype.ID,
                position - _transform.GetWorldPosition(other), yaw);
            if (offset > 0)
                best = Math.Min(best, offset);
        }
        return float.IsFinite(best) ? best : 0;
    }
}
