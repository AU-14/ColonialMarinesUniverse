// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Flora;
using Content.Shared.Stacks;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Flora;

// Changes the sprite of a stack by how much is in it (the "levels" of the pile).
public sealed class StackLevelsVisualizerSystem : VisualizerSystem<StackLevelsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, StackLevelsComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || comp.States.Count == 0)
            return;

        if (!AppearanceSystem.TryGetData<int>(uid, StackVisuals.Actual, out var actual, args.Component))
            return;

        var state = comp.States[Math.Clamp(actual, 1, comp.States.Count) - 1];
        if (SpriteSystem.LayerMapTryGet((uid, args.Sprite), comp.Layer, out var layer, false))
            SpriteSystem.LayerSetRsiState((uid, args.Sprite), layer, state);
    }
}
