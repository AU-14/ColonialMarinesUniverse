// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Numerics;
using Content.Shared._RMC14.Campfire;
using Content.Shared.CMU14.Forge;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Forge;

// The smoke over a forge: a smeltery that is lit sends up a puff of smoke every second or so, which hangs a moment over its top and thins out.
public sealed class ForgeSmokeSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly EntProtoId SmokeEffect = "RUMCEffectForgeSmoke";

    // Seconds between the puffs, and how far above the middle of the furnace they begin.
    private const float Interval = 1f;
    private const float Rise = 0.4f;

    private float _timer;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _timer += frameTime;
        if (_timer < Interval)
            return;

        _timer = 0f;

        var query = EntityQueryEnumerator<SmelteryComponent, CampfireComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var campfire, out var xform))
        {
            if (!campfire.Lit || !xform.Anchored)
                continue;

            Spawn(SmokeEffect, xform.Coordinates.Offset(new Vector2(_random.NextFloat(-0.15f, 0.15f), Rise)));
        }
    }
}
