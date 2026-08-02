using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Egg.EggRetriever;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Xenonids.Egg;

public sealed partial class XenoEggStorageVisualizerSystem : VisualizerSystem<XenoEggStorageVisualsComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, XenoEggStorageVisualsComponent component, ref AppearanceChangeEvent args)
    {
        var sprite = args.Sprite;

        if (sprite == null || !AppearanceSystem.TryGetData(uid, XenoEggStorageVisuals.Number, out int eggs))
            return;

        if (!_sprite.LayerMapTryGet((uid, sprite), XenoEggStorageVisualLayers.Base, out var layer, false))
            return;

        var level = Math.Clamp((int)Math.Ceiling(((double)eggs / component.MaxEggs) * component.FullStates), 0, component.FullStates);
        var stateSuffix = string.Empty;

        if (AppearanceSystem.TryGetData(uid, RMCXenoStateVisuals.Downed, out bool downed) && downed)
            stateSuffix += "_downed";
        else if (AppearanceSystem.TryGetData(uid, RMCXenoStateVisuals.Resting, out bool resting) && resting)
            stateSuffix += "_rest";

        if (AppearanceSystem.TryGetData(uid, XenoEggStorageVisuals.Active, out bool active) && active)
            stateSuffix += "_active";

        var rsi = sprite[layer].ActualRsi;
        if (rsi == null)
            return;

        // Some sprite sets do not have every egg level for every posture.
        // Fall back to the nearest lower level instead of displaying the error sprite.
        string? layerState = null;
        for (; level >= 0; level--)
        {
            var candidate = $"eggsac_{level}{stateSuffix}";
            if (!rsi.TryGetState(candidate, out _))
                continue;

            layerState = candidate;
            break;
        }

        if (layerState == null)
            return;

        _sprite.LayerSetRsiState((uid, sprite), layer, layerState);

        if (AppearanceSystem.TryGetData(uid, RMCXenoStateVisuals.Dead, out bool dead) && dead)
            _sprite.LayerSetVisible((uid, sprite), layer, false);
        else
            _sprite.LayerSetVisible((uid, sprite), layer, true);
    }
}
