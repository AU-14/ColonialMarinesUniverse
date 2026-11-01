using System.Numerics;
using Content.Shared.CMU14.ThreeD;
using Robust.Client.GameObjects;
using Robust.Shared.Graphics.RSI;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    private bool TrySpriteParts(SpriteComponent sprite, CMU3DModelPrototype model,
        out IReadOnlyList<CMU3DModelPart> parts, out string key)
    {
        parts = [];
        key = string.Empty;
        var opening = model.FloorOpening != null || model.CeilingOpening != null;
        if (!CMU3DSpriteAppearance.SupportsRotation(model, sprite.NoRotation, sprite.SnapCardinals,
                model.SourceSpriteRotates && (sprite.GranularLayersRendering ||
                                               _sprites.GetPostShaders(sprite).Count > 0)))
            return false;
        if (opening && (sprite.NoRotation || sprite.SnapCardinals || sprite.Color != Color.White ||
                        sprite.GranularLayersRendering || _sprites.GetPostShaders(sprite).Count > 0 ||
                        model.SourceDirections != 1 || model.SpriteStates.Count != 1))
            return false;
        SpriteComponent.Layer? basis = null;
        foreach (var layer in sprite.AllLayers)
        {
            if (!layer.Visible || layer.Color.A <= 0 || !layer.RsiState.IsValid && layer.Texture == null)
                continue;
            if (basis != null || layer is not SpriteComponent.Layer actual)
                return false;
            basis = actual;
        }
        if (basis?.ActualState == null ||
            model.SourceSpriteRotates && basis.CopyToShaderParameters != null ||
            opening && (basis.CopyToShaderParameters != null || basis.ActualState.DelayCount != 1))
            return false;
        var directions = basis.ActualState.RsiDirections switch
        {
            RsiDirectionType.Dir1 => 1,
            RsiDirectionType.Dir4 => 4,
            _ => 8,
        };
        var layerSource = Snapshot(basis);
        if (basis.DirOffset != SpriteComponent.DirectionOffset.None)
            layerSource = layerSource with { IdentityTransform = false };
        return CMU3DSpriteAppearance.TryParts(model, layerSource, sprite.Offset, sprite.Scale,
            (float) sprite.Rotation.Theta, directions, basis.ActualState.DelayCount, false, out parts, out key);
    }
}
