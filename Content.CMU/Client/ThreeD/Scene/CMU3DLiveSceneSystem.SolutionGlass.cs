using System.Numerics;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.CMU14.ThreeD;
using Robust.Client.GameObjects;
using Robust.Shared.Graphics.RSI;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    private bool TrySolutionGlassParts(EntityUid uid, SpriteComponent sprite, CMU3DModelPrototype model,
        out IReadOnlyList<CMU3DModelPart> parts, out string key)
    {
        parts = [];
        key = string.Empty;
        if (!HasComp<SolutionContainerVisualsComponent>(uid) || sprite.NoRotation || sprite.SnapCardinals ||
            sprite.Offset != Vector2.Zero || sprite.Scale != Vector2.One || sprite.Rotation != Angle.Zero ||
            sprite.GranularLayersRendering || _sprites.GetPostShaders(sprite).Count > 0 ||
            !CMU3DReagentTankAppearance.ValidSpriteColor(sprite.Color) ||
            !_sprites.LayerMapTryGet((uid, sprite), SolutionContainerLayers.Base, out var baseIndex, false) || baseIndex != 0 ||
            !_sprites.LayerMapTryGet((uid, sprite), SolutionContainerLayers.Fill, out var fillIndex, false) || fillIndex != 1)
            return false;
        var overlay = _sprites.LayerMapTryGet((uid, sprite), SolutionContainerLayers.Overlay, out var overlayIndex, false);
        if (overlay && overlayIndex != 2)
            return false;
        List<CMU3DButtonLayer> samples = [];
        foreach (var layer in sprite.AllLayers)
        {
            if (samples.Count >= (overlay ? 3 : 2) || layer is not SpriteComponent.Layer actual ||
                actual.ActualState is not { RsiDirections: RsiDirectionType.Dir1, DelayCount: 1 } ||
                actual.CopyToShaderParameters != null || actual.DirOffset != SpriteComponent.DirectionOffset.None)
                return false;
            samples.Add(Snapshot(actual));
        }
        if (samples.Count != (overlay ? 3 : 2))
            return false;
        return CMU3DSolutionGlassAppearance.TryParts(model, samples, out parts, out key);
    }
}
