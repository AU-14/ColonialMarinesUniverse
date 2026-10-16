// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Numerics;
using Content.Shared.CMU14.Trade;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Trade;

// The flight of the drone: it appears high above the pad, settles down onto it, hangs there a little, and rises and fades out. All of it is drawn from the phase and the time it began.
public sealed class TradeDroneVisualsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<TradeDroneComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var drone, out var sprite))
        {
            var elapsed = (float) (_timing.CurTime - drone.PhaseStart).TotalSeconds;
            var (height, alpha) = Pose(drone, elapsed);

            _sprite.SetOffset((uid, sprite), new Vector2(0f, height));
            _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(alpha));
        }
    }

    // How high above the pad the drone is (in tiles) and how much of it is seen.
    public static (float Height, float Alpha) Pose(TradeDroneComponent drone, float elapsed)
    {
        switch (drone.Phase)
        {
            case TradeDronePhase.Descend:
            {
                var p = Math.Clamp(elapsed / (float) drone.DescendTime.TotalSeconds, 0f, 1f);

                // Fast at first and slow at the end, as it settles.
                var eased = 1f - (1f - p) * (1f - p);
                return (drone.Height * (1f - eased), Math.Clamp(p * 3f, 0f, 1f));
            }

            case TradeDronePhase.Hover:
                // A little bob while it works.
                return (0.05f * MathF.Sin(elapsed * 6f), 1f);

            default:
            {
                var p = Math.Clamp(elapsed / (float) drone.AscendTime.TotalSeconds, 0f, 1f);
                var eased = p * p;
                return (drone.Height * eased, Math.Clamp(1f - (p - 0.5f) * 2f, 0f, 1f));
            }
        }
    }
}
