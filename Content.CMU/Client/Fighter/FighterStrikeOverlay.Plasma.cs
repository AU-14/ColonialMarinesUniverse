using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterStrikeOverlay
{
    private static readonly Color Plasma = Color.FromHex("#93FF42");

    private void DrawBoilerPlasma(in OverlayDrawArgs args, TimeSpan now)
    {
        var query = entities.EntityQueryEnumerator<FighterPlasmaVisualComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bolt, out var xform))
        {
            if (xform.MapID != args.MapId || now < bolt.StartedAt || now >= bolt.ExpiresAt) continue;
            var origin = _transform.GetWorldPosition(uid);
            if (!args.WorldAABB.Enlarged(30).Contains(origin)) continue;
            var age = (float) (now - bolt.StartedAt).TotalSeconds;
            if (!bolt.Launched)
            {
                var charge = Math.Clamp(age / 2, 0, 1);
                _particles.Mote(origin, new Vector2(.3f + charge * .7f), Plasma.WithAlpha(.5f));
                _particles.Mote(origin, new Vector2(.12f + charge * .2f), Color.White.WithAlpha(.8f));
                for (var i = 0; i < 12; i++)
                {
                    var phase = (age * .8f + i / 12f) % 1;
                    var angle = i * 2.4f + age * 2;
                    var point = origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (1 - phase) * 1.5f;
                    _particles.Mote(point, new Vector2(.06f + phase * .06f), Plasma.WithAlpha(phase * .8f));
                }
                continue;
            }

            var duration = Math.Max(.1f, (float) (bolt.ExpiresAt - bolt.StartedAt).TotalSeconds);
            var progress = Math.Clamp(age / duration, 0, 1);
            var fade = Math.Min(1, (1 - progress) * 5);
            var position = PlasmaAscent(origin, bolt.Direction, progress);
            for (var i = 1; i <= 20; i++)
            {
                var previous = progress - i * .018f;
                if (previous < 0) continue;
                var tail = PlasmaAscent(origin, bolt.Direction, previous);
                var radius = (.18f + progress * .55f) * (1 - i / 24f);
                _particles.Mote(tail, new Vector2(radius), Plasma.WithAlpha((1 - i / 21f) * fade * .65f));
            }
            _particles.Mote(position, new Vector2(.5f + progress), Plasma.WithAlpha(fade));
            _particles.Mote(position, new Vector2(.15f + progress * .4f), Color.White.WithAlpha(fade));
            if (age < .8f)
                for (var i = 0; i < 16; i++)
                {
                    var angle = i * MathF.Tau / 16;
                    var point = origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * age * 3;
                    _particles.Mote(point, new Vector2(.15f), Plasma.WithAlpha(1 - age / .8f));
                }
        }
    }

    private static Vector2 PlasmaAscent(Vector2 origin, Vector2 direction, float progress) =>
        origin + direction * (10 * progress * progress) + Vector2.UnitY * (14 * progress * progress);
}
