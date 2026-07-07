using System.Collections.Generic;
using Content.Shared._RMC14.Vehicle;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client._RMC14.Vehicle;

public sealed partial class VehicleHardpointVisualizerSystem : VisualizerSystem<VehicleHardpointVisualsComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, VehicleHardpointVisualsComponent component, ref AppearanceChangeEvent args)
    {
        var sprite = args.Sprite;
        if (sprite == null)
            return;

        if (!AppearanceSystem.TryGetData(
                uid,
                VehicleHardpointVisualsVisuals.Layers,
                out List<VehicleHardpointLayerState>? layers,
                args.Component) ||
            layers == null)
        {
            return;
        }

        foreach (var entry in layers)
        {
            UpdateLayer(uid, sprite, entry.Layer, entry.State);
        }
    }

    private void UpdateLayer(EntityUid uid, SpriteComponent sprite, string layerMap, string state)
    {
        if (!_sprite.LayerMapTryGet((uid, sprite), layerMap, out var layer, false))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            _sprite.LayerSetVisible((uid, sprite), layer, false);
            return;
        }

        _sprite.LayerSetRsiState((uid, sprite), layer, state);
        _sprite.LayerSetVisible((uid, sprite), layer, true);
    }
}
