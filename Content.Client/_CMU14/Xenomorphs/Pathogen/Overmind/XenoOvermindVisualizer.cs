using Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;
using Robust.Client.GameObjects;

namespace Content.Client._CMU14.Xenomorphs.Pathogen.Overmind;

/// <summary>
/// Client-side visualizer for the Overmind.
///
/// Layer layout (matches overmind.rsi):
///   0 - base        : the main body sprite (overmind_manifested / overmind_eye / dead)
///   1 - transition  : overmind_appear / overmind_disappear (one-shot, hidden otherwise)
///   2 - glow        : overmind_eye overlay always visible in incorporeal mode (pulsing eye)
///   3 - strengthen  : overmind_manifested tint/aura layer, visible only when strengthened+manifested
/// </summary>
public sealed class CMUXenoOvermindVisualizerSystem : VisualizerSystem<CMUXenoOvermindAppearanceComponent>
{
    private const int LayerBase = 0;
    private const int LayerTransition = 1;
    private const int LayerEyeGlow = 2;
    private const int LayerStrengthen = 3;

    protected override void OnAppearanceChange(EntityUid uid, CMUXenoOvermindAppearanceComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<OvermindVisualState>(uid, OvermindVisuals.VisualState, out var state, args.Component))
            return;

        // Reset everything to a known baseline first
        args.Sprite.LayerSetVisible(LayerBase, true);
        args.Sprite.LayerSetVisible(LayerTransition, false);
        args.Sprite.LayerSetVisible(LayerEyeGlow, false);
        args.Sprite.LayerSetVisible(LayerStrengthen, false);

        switch (state)
        {
            case OvermindVisualState.Incorporeal:
                args.Sprite.LayerSetState(LayerBase, "overmind_eye");
                break;

            case OvermindVisualState.Appearing:
                args.Sprite.LayerSetVisible(LayerBase, false); // hidden until transform completes
                args.Sprite.LayerSetVisible(LayerTransition, true);
                args.Sprite.LayerSetState(LayerTransition, "overmind_appear");
                break;

            case OvermindVisualState.Disappearing:
                args.Sprite.LayerSetVisible(LayerBase, false);
                args.Sprite.LayerSetVisible(LayerTransition, true);
                args.Sprite.LayerSetState(LayerTransition, "overmind_disappear");
                break;

            case OvermindVisualState.Manifested:
                args.Sprite.LayerSetState(LayerBase, "overmind_manifested");
                break;

            case OvermindVisualState.ManifestedStrengthened:
                args.Sprite.LayerSetState(LayerBase, "overmind_manifested");
                args.Sprite.LayerSetVisible(LayerStrengthen, true);
                args.Sprite.LayerSetState(LayerStrengthen, "overmind_manifested");
                args.Sprite.LayerSetColor(LayerStrengthen, Color.FromHex("#ffcc44aa"));
                break;

            case OvermindVisualState.Dying:
                args.Sprite.LayerSetVisible(LayerBase, false);
                args.Sprite.LayerSetVisible(LayerTransition, true);
                args.Sprite.LayerSetState(LayerTransition, "overmind_disappear");
                break;
        }
    }
}