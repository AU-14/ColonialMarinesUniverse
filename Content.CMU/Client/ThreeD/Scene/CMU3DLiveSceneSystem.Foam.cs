using System.Numerics;
using Content.Client.IconSmoothing;
using Content.Shared._RMC14.IconSmoothing;
using Content.Shared.CMU14.ThreeD;
using Content.Shared.IconSmoothing;
using Robust.Client.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Reflection;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    [Dependency] private IReflectionManager _foamReflection = default!;

    // EdgeLayer is private to the source owner. Resolve its actual enum keys using
    // the same engine service as the prototype serializer, without duplicating it.
    private static readonly string[] FoamLayerKeys = ["enum.FoamVisualLayers.Base",
        "enum.EdgeLayer.South", "enum.EdgeLayer.East", "enum.EdgeLayer.North", "enum.EdgeLayer.West"];

    private bool TryFoamParts(EntityUid uid, SpriteComponent sprite, CMU3DModelPrototype model,
        out IReadOnlyList<CMU3DModelPart> parts, out string key)
    {
        parts = [];
        key = string.Empty;
        if (!TryComp(uid, out MetaDataComponent? metadata) || metadata.EntityPrototype?.ID != CMU3DFoamAppearance.Prototype ||
            !HasComp<SmoothEdgeComponent>(uid) || !TryComp(uid, out IconSmoothComponent? smooth) ||
            !smooth.Enabled || smooth.SmoothKey != "walls" || smooth.Mode != IconSmoothingMode.NoSprite ||
            smooth.AdditionalKeys.Count != 0 || TryComp(uid, out CMIconSmoothComponent? cmSmooth) && cmSmooth.Smooth ||
            sprite.NoRotation || sprite.SnapCardinals || sprite.Offset != Vector2.Zero || sprite.Scale != Vector2.One ||
            sprite.Rotation != Angle.Zero || sprite.GranularLayersRendering || _sprites.GetPostShaders(sprite).Count > 0 ||
            !CMU3DReagentTankAppearance.ValidColor(sprite.Color))
            return false;
        List<CMU3DFoamLayer> samples = [];
        for (var i = 0; i < FoamLayerKeys.Length; i++)
        {
            if (!_foamReflection.TryParseEnumReference(FoamLayerKeys[i], out var mapKey, false) ||
                !_sprites.LayerMapTryGet((uid, sprite), mapKey, out var index, false) || index != i ||
                !_sprites.TryGetLayer((uid, sprite), index, out var layer, false))
                return false;
            samples.Add(new CMU3DFoamLayer(((ISpriteLayer) layer).ActualRsi?.Path.ToString(), layer.State.Name,
                layer.AnimationFrame, layer.ActualState?.DelayCount ?? 0, layer.Visible, layer.Color, layer.Offset,
                layer.ActualState is { RsiDirections: RsiDirectionType.Dir1 } && layer.Scale == Vector2.One &&
                layer.Rotation == Angle.Zero && layer.Texture == null && layer.DirOffset == SpriteComponent.DirectionOffset.None &&
                layer.CopyToShaderParameters == null && layer.Shader == null && layer.ShaderPrototype == null));
        }
        var count = 0;
        foreach (var layer in sprite.AllLayers)
            count++;
        return count == 5 && CMU3DFoamAppearance.TryParts(model, samples, out parts, out key);
    }
}
