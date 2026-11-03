// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Fluids;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Fluids;

// Show every open end. Vertical fittings overlay the shape so their horizontal branches remain visible.
public sealed partial class FluidPipeVisualizerSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FluidPipeComponent, AppearanceChangeEvent>(OnAppearanceChange);
        _overlays.AddOverlay(new CMUFluidPipeFlowOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<CMUFluidPipeFlowOverlay>();
    }

    private void OnAppearanceChange(Entity<FluidPipeComponent> pipe, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (args.AppearanceData.TryGetValue(FluidPipeVisuals.CMUFlowArrows, out var value)
            && value is CMUFluidPipeArrowState { Arrows: { Count: > 0 } } state)
            EnsureComp<CMUFluidPipeFlowComponent>(pipe).Arrows = state.Arrows;
        else
            RemComp<CMUFluidPipeFlowComponent>(pipe);

        if (!args.AppearanceData.TryGetValue(FluidPipeVisuals.Connections, out var raw) || raw is not int mask)
            mask = 0;

        var down = args.AppearanceData.TryGetValue(FluidPipeVisuals.Down, out var rawDown) && rawDown is true;

        _sprite.LayerSetRsiState((pipe.Owner, args.Sprite), 0, $"c{mask}");
        _sprite.LayerSetVisible((pipe.Owner, args.Sprite), "down", down);
        if (down)
            _sprite.LayerSetRsiState((pipe.Owner, args.Sprite), "down", DownState(mask));
    }

    // Down pipes: one piece for each side the liquid can come from; the north one serves the south as well.
    public static string DownState(int mask)
    {
        if ((mask & 1) != 0)
            return "down_n";

        if ((mask & 2) != 0)
            return "down_e";

        if ((mask & 8) != 0)
            return "down_w";

        return "down_n";
    }
}
