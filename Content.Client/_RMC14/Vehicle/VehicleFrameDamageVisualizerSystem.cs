using System;
using Content.Shared._RMC14.Vehicle;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Vehicle;

public sealed partial class VehicleFrameDamageVisualizerSystem : VisualizerSystem<HardpointIntegrityComponent>
{
    private const float ShowThreshold = 0.9f;
    private const float MinAlpha = 0.1f;
    [Dependency] private SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, HardpointIntegrityComponent component, ref AppearanceChangeEvent args)
    {
        var sprite = args.Sprite;
        if (sprite == null)
            return;

        if (!_sprite.LayerMapTryGet((uid, sprite), VehicleFrameDamageLayers.DamagedFrame, out var layer, false))
            return;

        float fraction;
        if (!AppearanceSystem.TryGetData(uid, VehicleFrameDamageVisuals.IntegrityFraction, out fraction))
        {
            var max = component.MaxIntegrity > 0f ? component.MaxIntegrity : 1f;
            fraction = Math.Clamp(component.Integrity / max, 0f, 1f);
        }

        if (fraction >= ShowThreshold)
        {
            _sprite.LayerSetVisible((uid, sprite), layer, false);
            return;
        }

        var t = fraction / ShowThreshold;
        var alpha = MinAlpha + (1f - MinAlpha) * (1f - t);

        _sprite.LayerSetVisible((uid, sprite), layer, true);
        _sprite.LayerSetColor((uid, sprite), layer, sprite.Color.WithAlpha(alpha));
    }
}
