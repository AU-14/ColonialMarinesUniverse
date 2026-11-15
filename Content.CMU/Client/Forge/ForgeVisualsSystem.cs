// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Numerics;
using Content.Shared.CMU14.Forge;
using Robust.Client.GameObjects;
using SharedDrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.CMU14.Forge;

// Changes the sprite of a forgeable item as it heats and cools.
public sealed class ForgeVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ForgeableComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<ForgeableComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var seat = _appearance.TryGetData<ForgeableSeatState>(ent.Owner, ForgeableVisuals.Seat, out var value, args.Component)
            ? value
            : ForgeableSeatState.None;

        var depth = seat switch
        {
            ForgeableSeatState.Furnace => SharedDrawDepth.HighFloorObjects,
            ForgeableSeatState.Anvil => SharedDrawDepth.SmallObjects,
            _ => SharedDrawDepth.Objects,
        };
        _sprite.SetDrawDepth((ent.Owner, args.Sprite), (int) depth);

        var offset = seat == ForgeableSeatState.Anvil ? ent.Comp.AnvilOffset : Vector2.Zero;
        _sprite.SetOffset((ent.Owner, args.Sprite), offset);
    }
}
