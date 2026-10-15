using System.Numerics;
using Content.Shared.CMU14.Xenomorphs.Reaper;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Xenomorphs.Reaper;

public sealed class CMUReaperRitualOverlay(IEntityManager entities, IGameTiming timing, SharedTransformSystem transform) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    private readonly CMUReaperVascularDrawing _vessels = new();
    private static readonly Color Pale = Color.FromHex("#B2A4BC");

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = entities.EntityQueryEnumerator<CMUReaperRitualComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var ritual, out var xform))
        {
            if (xform.MapID != args.MapId || ritual.Duration <= TimeSpan.Zero
                || !args.WorldAABB.Enlarged(3).Contains(transform.GetWorldPosition(uid)))
                continue;

            var ended = ritual.State != CMUReaperCastState.Charging;
            var clock = (float) ((ended ? ritual.EndedAt : timing.CurTime) - ritual.StartedAt).TotalSeconds;
            var after = ended ? (float) (timing.CurTime - ritual.EndedAt).TotalSeconds : 0;
            var progress = Math.Clamp(clock / (float) ritual.Duration.TotalSeconds, 0, 1);
            var release = ended ? CMUReaperVascularDrawing.Smooth(after / 1.8f) : 0;
            var alpha = 1 - release;
            if (alpha <= 0 || clock < 0)
                continue;

            handle.SetTransform(transform.GetWorldMatrix(uid));
            var origin = ritual.Origin + new Vector2(0.3f, -0.15f);
            // Keep the last accepted origin on cancellation, even when the caster is deleted.
            for (var i = 0; i < (ritual.Kind == CMUReaperRitualKind.Revival ? 5 : 4); i++)
            {
                var end = new Vector2((i - 2) * 0.28f, -0.35f);
                end = Vector2.Lerp(end, origin, release);
                _vessels.Curve(handle, origin, origin + new Vector2(0.5f, (i - 2) * 0.25f),
                    end + new Vector2(-0.5f, -0.4f), end,
                    CMUReaperVascularDrawing.Smooth(progress / 0.15f), 0.025f, clock, i, alpha);
            }

            if (ritual.Kind == CMUReaperRitualKind.Revival)
                DrawWrap(handle, progress, clock, release, alpha);
            else
                DrawHarvest(handle, origin, progress, clock, release, alpha,
                    ritual.State == CMUReaperCastState.Succeeded);
        }
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawWrap(DrawingHandleWorld handle, float progress, float clock, float release, float alpha)
    {
        var grow = CMUReaperVascularDrawing.Smooth((progress - 0.04f) / 0.7f);
        var beat = MathF.Pow(MathF.Max(0, MathF.Sin(clock * (0.7f + progress))), 8);
        for (var i = 0; i < 13; i++)
        {
            var angle = i * 2.399f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.65f);
            var reach = (1 + (i * 7 % 11) / 10f) * grow * (1 - release);
            var end = direction * reach;
            var start = direction * 0.35f;
            var b = start + direction * 0.6f;
            var c = end + new Vector2(-MathF.Sin(angle) * 0.45f, MathF.Cos(angle) * 0.25f);
            _vessels.Curve(handle, start, b, c, end,
                CMUReaperVascularDrawing.Smooth((progress - i * 0.015f) / 0.42f), 0.018f, clock, i, alpha);
            for (var j = 1; j < 3; j++)
            {
                var branch = CMUReaperVascularDrawing.Bezier(start, b, c, end, j * 0.28f);
                handle.DrawLine(branch, branch + new Vector2(MathF.Cos(angle + j), MathF.Sin(angle + j)) * 0.25f * grow,
                    Pale.WithAlpha(alpha * 0.35f));
            }
            var flow = 1 - (clock * 0.16f + i * 0.17f) % 1;
            handle.DrawCircle(CMUReaperVascularDrawing.Bezier(start, b, c, end, flow),
                0.025f + beat * 0.015f, Pale.WithAlpha(alpha * grow * 0.6f));
        }

        // Fine oblique sutures leave the real corpse sprite visible beneath the binding.
        for (var i = 0; i < 6; i++)
        {
            var growth = CMUReaperVascularDrawing.Smooth((progress - 0.25f - i * 0.055f) / 0.2f) * (1 - release);
            var y = 0.5f - i * 0.18f;
            _vessels.Curve(handle, new Vector2(-0.5f * growth, y - 0.18f),
                new Vector2(-0.3f, y + 0.1f + beat * 0.04f), new Vector2(0.25f, y + 0.1f),
                new Vector2(0.5f * growth, y + 0.2f), growth, 0.015f, clock, i + 8, alpha);
        }

        for (var i = 0; i < 25; i++)
        {
            var u = (clock * 0.06f + i * 0.173f) % 1;
            var angle = i * 2.399f;
            var radius = (0.5f + u * 1.35f) * grow;
            var pos = new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius * 0.58f + u * 0.3f);
            handle.DrawCircle(pos, 0.015f, Pale.WithAlpha(MathF.Sin(u * MathF.PI) * grow * alpha * 0.45f));
        }
    }

    private void DrawHarvest(DrawingHandleWorld handle, Vector2 origin, float progress, float clock,
        float release, float alpha, bool success)
    {
        for (var i = 0; i < 4; i++)
        {
            var angle = i * MathF.PI / 2 + 0.5f;
            var end = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 0.5f * (1 - release);
            _vessels.Curve(handle, end, end * 1.5f, new Vector2(-end.Y, end.X) * 0.7f,
                end * 0.3f, CMUReaperVascularDrawing.Smooth((progress - 0.22f) / 0.2f), 0.02f, clock * 4, i, alpha);
        }
        if (progress < 0.35f || release > 0 && !success)
            return;
        for (var i = 0; i < 18; i++)
        {
            var flow = (clock * 0.65f + i * 0.163f + release) % 1;
            var pos = CMUReaperVascularDrawing.Bezier(Vector2.Zero, new Vector2(0, 0.3f),
                origin + new Vector2(0.2f, 0), origin, flow);
            handle.DrawCircle(pos, 0.035f, Pale.WithAlpha(alpha * 0.7f));
        }
    }
}
