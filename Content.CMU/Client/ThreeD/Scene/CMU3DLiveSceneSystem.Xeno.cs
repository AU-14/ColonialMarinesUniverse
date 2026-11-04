using System.Numerics;
using Content.Shared.CMU14.ThreeD;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    private bool TryXenoParts(SpriteComponent sprite, CMU3DModelPrototype model,
        out IReadOnlyList<CMU3DModelPart> parts, out bool animated)
    {
        parts = [];
        animated = false;
        if (sprite.Offset != model.XenoSpriteOffset || sprite.Scale != Vector2.One ||
            sprite.Rotation != Angle.Zero || sprite.GranularLayersRendering || _sprites.GetPostShaders(sprite).Count > 0)
            return false;
        var layers = new List<CMU3DButtonLayer>();
        foreach (var layer in sprite.AllLayers)
        {
            if (!layer.Visible || layer.Color.A <= 0 || !layer.RsiState.IsValid && layer.Texture == null)
                continue;
            if (layer is not SpriteComponent.Layer actual || actual.ActualState == null ||
                actual.Texture != null || actual.CopyToShaderParameters != null)
                return false;
            animated |= actual.ActualState.DelayCount > 1;
            var sample = Snapshot(actual);
            // The sporecaster's unshaded vent is represented by its authored bright organ.
            if (actual.ShaderPrototype == "unshaded" && actual.Scale == Vector2.One &&
                actual.Offset == Vector2.Zero && actual.Rotation == Angle.Zero)
                sample = sample with { IdentityTransform = true };
            layers.Add(sample);
        }
        return CMU3DXenoAppearance.TryParts(model, layers, out parts);
    }
}
