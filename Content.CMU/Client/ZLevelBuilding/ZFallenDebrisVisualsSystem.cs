// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Numerics;
using Content.Shared._AU14.ZLevelBuilding;
using Robust.Client.GameObjects;

namespace Content.Client._AU14.ZLevelBuilding;

/// <summary>
/// Renders structures that fell through a collapsed z-level floor as battered rubble: the sprite is
/// shrunken and tinted dark so players can tell at a glance it's debris, not an intact wall. Applied when
/// the networked <see cref="ZFallenDebrisComponent"/> arrives from the server.
/// </summary>
public sealed class ZFallenDebrisVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    // 🔧 TUNABLE: how much fallen debris shrinks and darkens. Scale multiplies the sprite's current scale
    // (so half-scale 1-tile sprites stay proportional); the color is a flat dark-grey modulate.
    private const float DebrisScale = 0.7f;
    private static readonly Robust.Shared.Maths.Color DebrisTint = new(0.45f, 0.42f, 0.4f);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZFallenDebrisComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<ZFallenDebrisComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.SetScale((ent.Owner, sprite), sprite.Scale * new Vector2(DebrisScale, DebrisScale));
        _sprite.SetColor((ent.Owner, sprite), sprite.Color * DebrisTint);
    }
}
