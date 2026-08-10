using Content.Shared._CMU14.Xenomorphs.Pathogen.SporeSac;
using Robust.Client.GameObjects;

namespace Content.Client._CMU14.Xenomorphs.Pathogen.SporeSac;

public sealed class SporeSacVisualizerSystem : VisualizerSystem<CMUPathogenSporeSacComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, CMUPathogenSporeSacComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<SporeSacStatus>(uid, SporeSacVisuals.State, out var status, args.Component))
            return;

        var state = status == SporeSacStatus.Waiting ? "closed" : "open";
        args.Sprite.LayerSetState(SporeSacVisualLayers.Base, state);
    }
}