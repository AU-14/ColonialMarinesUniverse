using Content.Shared._RMC14.Xenonids.Construction.EggMorpher;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Xenonids.Construction;

public sealed partial class EggmorpherVisualizerSystem : VisualizerSystem<EggMorpherComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, EggMorpherComponent component, ref AppearanceChangeEvent args)
    {
        var sprite = args.Sprite;

        if (sprite == null || !AppearanceSystem.TryGetData(uid, EggmorpherOverlayVisuals.Number, out int number) ||
            !_sprite.LayerMapTryGet((uid, sprite), EggmorpherOverlayLayers.Overlay, out var layer, false) ||
            !_sprite.LayerMapTryGet((uid, sprite), EggmorpherOverlayLayers.Base, out var layer2, false))
            return;

        //Same as parasite number calc
        int level = (int)Math.Min(Math.Ceiling(((double)number / component.MaxParasites) * component.OverlayCount), component.OverlayCount);

        if (level == 0)
        {
            _sprite.LayerSetVisible((uid, sprite), layer, false);
            return;
        }

        var wasVisible = true;

        if (!_sprite.TryGetLayer((uid, sprite), layer, out var overlayLayer, false))
            return;

        if (!overlayLayer.Visible)
        {
            _sprite.LayerSetVisible((uid, sprite), layer, true);
            wasVisible = false;
        }

        string state = component.OverlayPrefix + "_" + (level - 1);

        if (state != _sprite.LayerGetRsiState((uid, sprite), layer) || !wasVisible)
        {
            _sprite.LayerSetRsiState((uid, sprite), layer, state);
            var stat = _sprite.LayerGetRsiState((uid, sprite), layer2);
            _sprite.LayerSetRsiState((uid, sprite), layer2, state);
            _sprite.LayerSetRsiState((uid, sprite), layer2, stat);
        }
    }
}
