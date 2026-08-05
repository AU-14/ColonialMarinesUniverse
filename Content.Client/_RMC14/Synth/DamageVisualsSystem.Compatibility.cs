using Robust.Client.GameObjects;

namespace Content.Client.Damage;

public sealed partial class DamageVisualsSystem
{
    public void ChangeDamageGroupColor(
        SpriteComponent sprite,
        DamageVisualsComponent damageVisuals,
        string group,
        string color)
    {
        if (damageVisuals.TargetLayers == null || damageVisuals.DamageOverlayGroups == null)
            return;

        foreach (var layerMapKey in damageVisuals.TargetLayerMapKeys)
        {
            if (sprite.LayerMapTryGet($"{layerMapKey}{group}", out var spriteLayer))
                sprite.LayerSetColor(spriteLayer, Color.FromHex(color));
        }

        damageVisuals.DamageOverlayGroups[group].Color = color;
    }
}
