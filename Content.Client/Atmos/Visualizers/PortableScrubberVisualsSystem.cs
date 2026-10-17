using Robust.Client.GameObjects;
using Content.Shared.Atmos.Visuals;
using Content.Client.Power;

namespace Content.Client.Atmos.Visualizers;

/// <summary>
/// Controls client-side visuals for portable scrubbers.
/// </summary>
public sealed partial class PortableScrubberSystem : VisualizerSystem<PortableScrubberVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, PortableScrubberVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (uid, args.Sprite); // CMU14: CMU sprite ports drop the upstream unlit layers

        if (AppearanceSystem.TryGetData<bool>(uid, PortableScrubberVisuals.IsFull, out var isFull, args.Component)
            && AppearanceSystem.TryGetData<bool>(uid, PortableScrubberVisuals.IsRunning, out var isRunning, args.Component))
        {
            var runningState = isRunning ? component.RunningState : component.IdleState;
            SpriteSystem.LayerSetRsiState(sprite, PortableScrubberVisualLayers.IsRunning, runningState);

            var fullState = isFull ? component.FullState : component.ReadyState;
            if (SpriteSystem.LayerMapTryGet(sprite, PowerDeviceVisualLayers.Powered, out _, false)) // CMU14
                SpriteSystem.LayerSetRsiState(sprite, PowerDeviceVisualLayers.Powered, fullState);
        }

        if (AppearanceSystem.TryGetData<bool>(uid, PortableScrubberVisuals.IsDraining, out var isDraining, args.Component))
        {
            if (SpriteSystem.LayerMapTryGet(sprite, PortableScrubberVisualLayers.IsDraining, out _, false)) // CMU14
                SpriteSystem.LayerSetVisible(sprite, PortableScrubberVisualLayers.IsDraining, isDraining);
        }
    }
}

public enum PortableScrubberVisualLayers : byte
{
    IsRunning,

    IsDraining
}
