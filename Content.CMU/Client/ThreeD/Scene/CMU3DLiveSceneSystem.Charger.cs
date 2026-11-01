using System.Numerics;
using Content.Client.PowerCell;
using Content.Shared.CMU14.ThreeD;
using Content.Shared.Power.Components;
using Content.Shared.Storage.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Graphics.RSI;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    private bool TryChargerParts(EntityUid uid, SpriteComponent sprite, CMU3DModelPrototype model,
        out IReadOnlyList<CMU3DModelPart> parts, out string key)
    {
        parts = [];
        key = string.Empty;
        if (!HasComp<ChargerComponent>(uid) || !HasComp<PowerChargerVisualsComponent>(uid) ||
            !HasComp<ItemMapperComponent>(uid) || sprite.NoRotation || !sprite.SnapCardinals ||
            sprite.Offset != Vector2.Zero || sprite.Scale != Vector2.One || sprite.Rotation != Angle.Zero ||
            sprite.GranularLayersRendering || _sprites.GetPostShaders(sprite).Count > 0 ||
            !CMU3DReagentTankAppearance.ValidSpriteColor(sprite.Color) ||
            !_sprites.LayerMapTryGet((uid, sprite), PowerChargerVisualLayers.Base, out var baseIndex, false) ||
            !_sprites.LayerMapTryGet((uid, sprite), PowerChargerVisualLayers.Light, out var lightIndex, false) ||
            baseIndex >= lightIndex ||
            !_sprites.TryGetLayer((uid, sprite), baseIndex, out var basis, false) ||
            !_sprites.TryGetLayer((uid, sprite), lightIndex, out var light, false))
            return false;
        SpriteComponent.Layer? taser = null;
        SpriteComponent.Layer? baton = null;
        var taserIndex = -1;
        if (_sprites.LayerMapTryGet((uid, sprite), CMU3DChargerAppearance.Taser, out taserIndex, false) &&
            (taserIndex <= lightIndex || !_sprites.TryGetLayer((uid, sprite), taserIndex, out taser, false)))
            return false;
        if (_sprites.LayerMapTryGet((uid, sprite), CMU3DChargerAppearance.Baton, out var batonIndex, false) &&
            (batonIndex <= Math.Max(lightIndex, taserIndex) || !_sprites.TryGetLayer((uid, sprite), batonIndex, out baton, false)))
            return false;
        foreach (var layer in sprite.AllLayers)
            if (layer != basis && layer != light && layer != taser && layer != baton && layer.Visible &&
                (layer.RsiState.IsValid || layer.Texture != null))
                return false;
        return CMU3DChargerAppearance.TryParts(model, ChargerSnapshot(basis), ChargerSnapshot(light),
            taser == null ? null : ChargerSnapshot(taser), baton == null ? null : ChargerSnapshot(baton),
            false, out parts, out key);
    }

    private static CMU3DChargerLayer ChargerSnapshot(SpriteComponent.Layer layer)
    {
        var unshaded = layer.ShaderPrototype == "unshaded";
        var validShader = unshaded || layer.Shader == null && layer.ShaderPrototype == null;
        return new CMU3DChargerLayer(((ISpriteLayer) layer).ActualRsi?.Path.ToString(), layer.State.Name,
            layer.AnimationFrame, layer.ActualState?.DelayCount ?? 0, layer.Visible, layer.Color,
            layer.ActualState is { RsiDirections: RsiDirectionType.Dir1 } && layer.Scale == Vector2.One &&
            layer.Offset == Vector2.Zero && layer.Rotation == Angle.Zero && layer.Texture == null &&
            layer.DirOffset == SpriteComponent.DirectionOffset.None && layer.CopyToShaderParameters == null && validShader,
            unshaded);
    }
}
