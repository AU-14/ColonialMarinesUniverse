using Content.Shared._CMU14.Yautja;
using Content.Shared.Toggleable;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Yautja;

public sealed partial class YautjaArrowVisualizerSystem : VisualizerSystem<YautjaArrowComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, YautjaArrowComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null ||
            !AppearanceSystem.TryGetData<YautjaArrowVisualState>(uid, YautjaArrowVisuals.State, out var state, args.Component))
        {
            return;
        }

        if (AppearanceSystem.TryGetData<bool>(uid, ToggleableVisuals.Enabled, out var enabled, args.Component) &&
            enabled)
        {
            return;
        }

        SetLayerColor(uid, args.Sprite, "tail", ColorFor(state, "tail"));
        SetLayerColor(uid, args.Sprite, "rod", ColorFor(state, "rod"));
        SetLayerColor(uid, args.Sprite, "tip", ColorFor(state, "tip"));
        SetLayerVisible(uid, args.Sprite, "mark", state != YautjaArrowVisualState.Inert);
        SetLayerColor(uid, args.Sprite, "mark", ColorFor(state, "mark"));
    }

    private void SetLayerColor(EntityUid uid, SpriteComponent sprite, string layerKey, Color color)
    {
        if (_sprite.LayerMapTryGet((uid, sprite), layerKey, out var layer, false))
            _sprite.LayerSetColor((uid, sprite), layer, color);
    }

    private void SetLayerVisible(EntityUid uid, SpriteComponent sprite, string layerKey, bool visible)
    {
        if (_sprite.LayerMapTryGet((uid, sprite), layerKey, out var layer, false))
            _sprite.LayerSetVisible((uid, sprite), layer, visible);
    }

    private static Color ColorFor(YautjaArrowVisualState state, string layer)
    {
        return state switch
        {
            YautjaArrowVisualState.Explosive when layer == "tip" => Color.OrangeRed,
            YautjaArrowVisualState.Explosive when layer == "mark" => Color.Orange,
            YautjaArrowVisualState.Emp when layer == "tip" => Color.DeepSkyBlue,
            YautjaArrowVisualState.Emp when layer == "mark" => Color.Cyan,
            YautjaArrowVisualState.Dynamic when layer == "tail" => Color.MediumPurple,
            YautjaArrowVisualState.Dynamic when layer == "mark" => Color.MediumPurple,
            YautjaArrowVisualState.Snare when layer == "tail" => Color.ForestGreen,
            YautjaArrowVisualState.Snare when layer == "tip" => Color.LawnGreen,
            YautjaArrowVisualState.Snare when layer == "mark" => Color.LawnGreen,
            _ when layer == "tail" => Color.DarkRed,
            _ when layer == "rod" => Color.SaddleBrown,
            _ => Color.White,
        };
    }
}

public sealed partial class YautjaBowVisualizerSystem : VisualizerSystem<YautjaBowComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, YautjaBowComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null ||
            !AppearanceSystem.TryGetData<string>(uid, YautjaBowVisuals.LoadedIcon, out var loadedIcon, args.Component))
        {
            return;
        }

        if (!_sprite.LayerMapTryGet((uid, args.Sprite), "arrow", out var arrowLayer, false))
            return;

        var state = loadedIcon switch
        {
            "expl" => "bow_expl",
            "emp" => "bow_emp",
            "trap" => "bow_trap",
            "loaded" => "bow_loaded",
            _ => "bow_loaded",
        };

        _sprite.LayerSetRsiState((uid, args.Sprite), arrowLayer, state);
    }
}
