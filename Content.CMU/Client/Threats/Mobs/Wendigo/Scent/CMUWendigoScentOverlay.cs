using System.Numerics;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Scent;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Threats.Mobs.Wendigo.Scent;

/// <summary>
/// Draws the local Wendigo's smell trail: a soft, drifting dark-red haze along the tracked target's recent
/// positions, fading with age. Only the local player's own <see cref="CMUWendigoScentTrackerComponent"/> is drawn.
/// </summary>
public sealed partial class CMUWendigoScentOverlay : Overlay
{
    private static readonly Color HazeColor = Color.FromHex("#8B0A14");
    private static readonly Color CoreColor = Color.FromHex("#B3121F");

    // Parallel strands make the line read as a smear rather than a laser.
    private static readonly Vector2[] StrandOffsets =
    {
        new(0f, 0f),
        new(0.06f, 0.04f),
        new(-0.05f, 0.06f),
        new(0.04f, -0.06f),
        new(-0.06f, -0.03f),
    };

    private const float PuffSpacing = 0.45f;
    private const int MaxPuffsPerSegment = 24;

    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public CMUWendigoScentOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        if (_player.LocalEntity is not { } local ||
            !_entity.TryGetComponent(local, out CMUWendigoScentTrackerComponent? tracker) ||
            tracker.Trail.Count == 0)
        {
            return;
        }

        var handle = args.WorldHandle;
        var now = _timing.CurTime;
        var fade = Math.Max(1.0, tracker.TrailFade.TotalSeconds);
        var drift = (float) now.TotalSeconds;
        var trail = tracker.Trail;

        for (var i = 0; i < trail.Count; i++)
        {
            var point = trail[i];
            if (point.Coordinates.MapId != args.MapId)
                continue;

            var alpha = Strength(point, now, fade);
            var from = point.Coordinates.Position;

            if (i + 1 >= trail.Count || trail[i + 1].Coordinates.MapId != args.MapId)
            {
                if (alpha > 0f)
                    DrawPuff(handle, from, alpha, i * 31, drift);
                continue;
            }

            var next = trail[i + 1];
            var nextAlpha = Strength(next, now, fade);
            if (alpha <= 0f && nextAlpha <= 0f)
                continue;

            var to = next.Coordinates.Position;
            var segmentAlpha = MathF.Min(alpha, nextAlpha);
            if (segmentAlpha > 0f)
            {
                foreach (var offset in StrandOffsets)
                {
                    handle.DrawLine(from + offset, to + offset, CoreColor.WithAlpha(segmentAlpha * 0.65f));
                }
            }

            // Haze puffs along the segment, jittered and slowly drifting, blending the two ends' ages.
            var length = (to - from).Length();
            var puffs = Math.Clamp((int) MathF.Ceiling(length / PuffSpacing), 1, MaxPuffsPerSegment);
            for (var p = 0; p < puffs; p++)
            {
                var t = (float) p / puffs;
                var puffAlpha = alpha + (nextAlpha - alpha) * t;
                if (puffAlpha <= 0f)
                    continue;

                DrawPuff(handle, Vector2.Lerp(from, to, t), puffAlpha, i * 31 + p, drift);
            }
        }
    }

    private static float Strength(CMUWendigoScentPoint point, TimeSpan now, double fade)
    {
        var age = (now - point.Time).TotalSeconds;
        return (float) Math.Clamp(1.0 - age / fade, 0.0, 1.0);
    }

    private static void DrawPuff(DrawingHandleWorld handle, Vector2 center, float alpha, int seed, float drift)
    {
        var jitter = new Vector2(Noise(seed) - 0.5f, Noise(seed + 7) - 0.5f) * 0.35f;
        var sway = new Vector2(
            MathF.Sin(drift * 0.6f + seed * 1.3f),
            MathF.Cos(drift * 0.45f + seed * 0.7f)) * 0.06f;
        var pos = center + jitter + sway;
        var size = 0.75f + Noise(seed + 13) * 0.5f;

        handle.DrawCircle(pos, 0.38f * size, HazeColor.WithAlpha(alpha * 0.22f));
        handle.DrawCircle(pos, 0.24f * size, HazeColor.WithAlpha(alpha * 0.3f));
        handle.DrawCircle(pos, 0.12f * size, CoreColor.WithAlpha(alpha * 0.45f));
    }

    /// <summary>Cheap deterministic hash in [0, 1), so puffs keep their place between frames.</summary>
    private static float Noise(int seed)
    {
        var x = MathF.Sin(seed * 12.9898f) * 43758.547f;
        return x - MathF.Floor(x);
    }
}
