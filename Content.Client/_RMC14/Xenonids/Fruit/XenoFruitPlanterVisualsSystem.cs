using Content.Shared._RMC14.Xenonids.Fruit.Components;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Xenonids.Fruit;

// System for updating the sprite of a fruit planter based on their selected fruit
public sealed partial class XenoFruitPlanterVisualsSystem : VisualizerSystem<XenoFruitPlanterVisualsComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, XenoFruitPlanterVisualsComponent component, ref AppearanceChangeEvent args)
    {
        var sprite = args.Sprite;

        if (sprite == null ||
            !AppearanceSystem.TryGetData(uid, XenoFruitPlanterVisuals.Color, out Color color) ||
            !_sprite.LayerMapTryGet((uid, sprite), XenoFruitVisualLayers.Base, out var layer, false))
        {
            return;
        }

        _sprite.LayerSetVisible((uid, sprite), layer, true);
        _sprite.LayerSetColor((uid, sprite), layer, color);

        if (AppearanceSystem.TryGetData(uid, XenoFruitPlanterVisuals.Downed, out bool downed) && downed)
        {
            _sprite.LayerSetRsiState((uid, sprite), layer, $"{component.Prefix}_downed");
            return;
        }

        if (AppearanceSystem.TryGetData(uid, XenoFruitPlanterVisuals.Resting, out bool resting) && resting)
        {
            _sprite.LayerSetRsiState((uid, sprite), layer, $"{component.Prefix}_rest");
            return;
        }

        _sprite.LayerSetRsiState((uid, sprite), layer, $"{component.Prefix}_walk");
    }
}
