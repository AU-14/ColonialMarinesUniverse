// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Fluids;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Fluids;

// Shows a pipe as the piece of the sprite that has an arm on every side the pipe is joined on; a pipe that goes down is shown
// as the piece that looks down, from the side its liquid comes from.
public sealed class FluidPipeVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FluidPipeComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<FluidPipeComponent> pipe, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!args.AppearanceData.TryGetValue(FluidPipeVisuals.Connections, out var raw) || raw is not int mask)
            mask = 0;

        var down = args.AppearanceData.TryGetValue(FluidPipeVisuals.Down, out var rawDown) && rawDown is true;

        var state = down ? DownState(mask) : $"c{mask}";
        _sprite.LayerSetRsiState((pipe.Owner, args.Sprite), 0, state);
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
