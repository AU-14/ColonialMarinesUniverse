using Content.Shared.CMU14.Xenomorphs.Pathogen.Overmind;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Xenomorphs.Pathogen.Overmind;

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
        SpriteSystem.LayerSetVisible((uid, args.Sprite), LayerBase, true);
        SpriteSystem.LayerSetVisible((uid, args.Sprite), LayerTransition, false);
        SpriteSystem.LayerSetVisible((uid, args.Sprite), LayerEyeGlow, false);
        SpriteSystem.LayerSetVisible((uid, args.Sprite), LayerStrengthen, false);

        switch (state)
        {
            case OvermindVisualState.Incorporeal:
                SpriteSystem.LayerSetRsiState((uid, args.Sprite), LayerBase, "overmind_eye");
                break;

            case OvermindVisualState.Appearing:
                SpriteSystem.LayerSetVisible((uid, args.Sprite), LayerBase, false); // hidden until transform completes
                SpriteSystem.LayerSetVisible((uid, args.Sprite), LayerTransition, true);
                SpriteSystem.LayerSetRsiState((uid, args.Sprite), LayerTransition, "overmind_appear");
                break;

            case OvermindVisualState.Disappearing:
                SpriteSystem.LayerSetVisible((uid, args.Sprite), LayerBase, false);
                SpriteSystem.LayerSetVisible((uid, args.Sprite), LayerTransition, true);
                SpriteSystem.LayerSetRsiState((uid, args.Sprite), LayerTransition, "overmind_disappear");
                break;

            case OvermindVisualState.Manifested:
                SpriteSystem.LayerSetRsiState((uid, args.Sprite), LayerBase, "overmind_manifested");
                break;

            case OvermindVisualState.ManifestedStrengthened:
                SpriteSystem.LayerSetRsiState((uid, args.Sprite), LayerBase, "overmind_manifested");
                SpriteSystem.LayerSetVisible((uid, args.Sprite), LayerStrengthen, true);
                SpriteSystem.LayerSetRsiState((uid, args.Sprite), LayerStrengthen, "overmind_manifested");
                SpriteSystem.LayerSetColor((uid, args.Sprite), LayerStrengthen, Color.FromHex("#ffcc44aa"));
                break;

            case OvermindVisualState.Dying:
                SpriteSystem.LayerSetVisible((uid, args.Sprite), LayerBase, false);
                SpriteSystem.LayerSetVisible((uid, args.Sprite), LayerTransition, true);
                SpriteSystem.LayerSetRsiState((uid, args.Sprite), LayerTransition, "overmind_disappear");
                break;
        }
    }
}