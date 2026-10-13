// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Gunsmith;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Gunsmith;

/// <summary>Puts together the picture of a weapon made at the bench out of the parts of other weapons: the rear, the middle and the front.</summary>
public sealed class CraftedFirearmVisualizerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CraftedFirearmComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CraftedFirearmComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnStartup(Entity<CraftedFirearmComponent> ent, ref ComponentStartup args)
    {
        Update(ent);
    }

    private void OnState(Entity<CraftedFirearmComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Update(ent);
    }

    private void Update(Entity<CraftedFirearmComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        Set((ent, sprite), "gunsmith_rear", ent.Comp.Rear);
        Set((ent, sprite), "gunsmith_mid", ent.Comp.Mid);
        Set((ent, sprite), "gunsmith_front", ent.Comp.Front);
    }

    private void Set(Entity<SpriteComponent> ent, string layer, string state)
    {
        if (_sprite.LayerMapTryGet(ent.AsNullable(), layer, out var index, false))
            _sprite.LayerSetRsiState(ent.AsNullable(), index, state);
    }
}
