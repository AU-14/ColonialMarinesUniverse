using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Projectile.Parasite;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Xenonids.Parasite;

public sealed partial class XenoParasitesVisualSystem : VisualizerSystem<XenoParasiteThrowerComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, XenoParasiteThrowerComponent component, ref AppearanceChangeEvent args)
    {
        var sprite = args.Sprite;

        if (sprite == null || !AppearanceSystem.TryGetData(uid, ParasiteOverlayVisuals.States, out bool[] states))
            return;

        string layerState = "para_";

        if(AppearanceSystem.TryGetData(uid, RMCXenoStateVisuals.Downed, out bool downed) && downed)
            layerState = "para_downed_";
        else if(AppearanceSystem.TryGetData(uid, RMCXenoStateVisuals.Resting, out bool resting) && resting)
            layerState = "para_rest_";

        foreach(var layer in Enum.GetValues<ParasiteOverlayLayers>())
        {
            if (!_sprite.LayerMapTryGet((uid, sprite), layer, out var paraLayer, false))
                continue;

            _sprite.LayerSetVisible((uid, sprite), layer, states[(int)layer]);

            _sprite.LayerSetRsiState((uid, sprite), layer, $"{layerState}{(int)layer}");
        }
    }
}
