using Content.Shared.CMU14.ThreeD;
using Content.Client.Inventory;
using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Shared.Graphics;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    private readonly List<int> _wornLayerIndices = [];

    public bool TryWornEquipmentAppearance(EntityUid wearer, SpriteComponent sprite, string slot,
        CMU3DEquipmentPosePrototype pose, out Color tint)
    {
        tint = sprite.Color;
        if (!TryComp(wearer, out InventorySlotsComponent? inventory) ||
            !inventory.VisualLayerKeys.TryGetValue(slot, out var keys) || pose.WornLayers.Count == 0)
            return false;
        var sprites = _sprites;
        _wornLayerIndices.Clear();
        foreach (var key in keys)
            if (sprites.LayerMapTryGet((wearer, sprite), key, out var index, false) &&
                sprites.TryGetLayer((wearer, sprite), index, out var layer, false) && layer.Visible && layer.Color.A > 0)
                _wornLayerIndices.Add(index);
        if (_wornLayerIndices.Count != pose.WornLayers.Count) return false;
        _wornLayerIndices.Sort();
        for (var i = 0; i < _wornLayerIndices.Count; i++)
        {
            if (!sprites.TryGetLayer((wearer, sprite), _wornLayerIndices[i], out var layer, false)) return false;
            var expected = pose.WornLayers[i];
            if (layer.Texture != null || layer.Shader != null || layer.ShaderPrototype != null ||
                layer.Scale != Vector2.One || layer.Offset != Vector2.Zero || layer.Rotation != Angle.Zero ||
                layer.ActualState?.DelayCount != 1 || layer.Color != expected.Color ||
                !CMU3DSourceReference.Matches(((ISpriteLayer) layer).ActualRsi?.Path.ToString(), expected.Rsi) ||
                layer.State.Name != expected.State)
                return false;
        }
        return true;
    }

    /// <summary>Contained equipment uses the same authored appearance selection as dropped items.</summary>
    public bool TryEquipmentParts(EntityUid item, CMU3DModelPrototype model, CMU3DEquipmentPosePrototype pose,
        out IReadOnlyList<CMU3DModelPart> parts, out Color tint, out string appearance)
    {
        parts = model.Parts;
        tint = Color.White;
        appearance = string.Empty;
        if (!TryComp(item, out SpriteComponent? sprite) || !sprite.Visible || sprite.Color.A <= 0)
            return false;
        tint = CMU3DSceneLayout.PresentationTint(model, sprite.Color);
        if (model.SpriteStates.Count > 0)
            return TrySpriteParts(sprite, model, out parts, out appearance);
        if (pose.ItemLayers.Count == 0 || sprite.Scale != Vector2.One || sprite.Offset != Vector2.Zero ||
            sprite.GranularLayersRendering || _sprites.GetPostShaders(sprite).Count > 0)
            return false;
        var visible = 0;
        foreach (var layer in sprite.AllLayers)
        {
            if (!layer.Visible || layer.Color.A <= 0 || !layer.RsiState.IsValid && layer.Texture == null)
                continue;
            if (visible >= pose.ItemLayers.Count || layer is not SpriteComponent.Layer actual)
                return false;
            var expected = pose.ItemLayers[visible++];
            if (actual.Texture != null || actual.Shader != null || actual.ShaderPrototype != null ||
                actual.Scale != Vector2.One || Vector2.DistanceSquared(actual.Offset, expected.Offset) > 1e-10f || actual.Rotation != Angle.Zero ||
                actual.ActualState?.DelayCount != 1 || actual.Color != expected.Color ||
                !CMU3DSourceReference.Matches(((ISpriteLayer) actual).ActualRsi?.Path.ToString(), expected.Rsi) || actual.State.Name != expected.State)
                return false;
        }
        if (visible != pose.ItemLayers.Count)
            return false;
        // Specialized machinery, animated effects and stateful containers need a
        // dedicated attachment. A default pose must not freeze their live state.
        return model.DoorSpriteStates.Count == 0 && model.DoorButtonStates.Count == 0 &&
               model.PoweredLightStates.Count == 0 && model.ChargerAppearance == null &&
               model.FoamAppearance == null && model.SolutionAppearance == null &&
               model.ReagentTankAppearance == null && model.BarricadeDamageStates.Count == 0;
    }
}
