using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterStrikeOverlay
{
    private void DrawVtol(in OverlayDrawArgs args, TimeSpan now)
    {
        var query = entities.EntityQueryEnumerator<FighterVtolVisualComponent, TransformComponent>();
        var drawn = 0;
        while (query.MoveNext(out var uid, out var visual, out var xform))
        {
            if (xform.MapID != args.MapId) continue;
            var origin = _transform.GetWorldPosition(uid);
            if (!args.WorldAABB.Enlarged(9).Contains(origin)) continue;
            var age = (float) (now - visual.StartedAt).TotalSeconds;
            if (age < 0) continue;
            var rotation = _transform.GetWorldRotation(uid);
            var p = FighterVtol.Progress(now, visual.StartedAt, visual.EndsAt);
            var power = FighterVtolPresentation.Power(visual, now);
            var active = visual.Outcome == FighterVtolOutcome.Active;
            var lift = FighterVtol.Lift(p, visual.Landing);
            var dust = FighterVtolPresentation.Dust(visual.Surface);
            var density = visual.Surface == FighterVtolSurface.Deck ? .55f : 1;
            // Deterministic emission history: existing sheets keep travelling after the jets stop.
            for (var i = 0; i < 96; i++)
            {
                var life = 1.8f + (i % 7) * .17f;
                var delay = i * .071f;
                var particleAge = (age + delay) % life;
                var emittedAt = now - TimeSpan.FromSeconds(particleAge);
                if (emittedAt < visual.StartedAt || !active && emittedAt >= visual.FinishedAt) continue;
                var emittedProgress = FighterVtol.Progress(emittedAt, visual.StartedAt, visual.EndsAt);
                var strength = FighterVtol.Thrust(emittedProgress, visual.Landing);
                var phase = particleAge / life;
                var angle = i * 2.39996f + phase * (i % 2 == 0 ? .35f : -.35f);
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var radius = .55f + particleAge * (1.7f + strength * .9f);
                var local = FighterVtolPresentation.Nozzle(i % 2 == 0 ? -1 : 1) + direction * radius;
                local += new Vector2(particleAge * .28f, 0);
                var alpha = MathF.Sin(phase * MathF.PI) * strength * density * .33f;
                var size = .18f + phase * .8f;
                _particles.Mote(origin + rotation.RotateVec(local), new Vector2(size * 1.6f, size * .65f),
                    dust.WithAlpha(alpha), angle + (float) rotation.Theta, smoke: true, seed: i * .17f);
                if (i % 4 == 0 && phase < .65f)
                    _particles.Trail(origin + rotation.RotateVec(local), origin + rotation.RotateVec(local - direction * .16f),
                        .018f, dust.WithAlpha(alpha * 1.7f));
            }
            if (active)
            {
                var heightOffset = new Vector2(0, lift * 2.5f);
                var opacity = 1 - FighterVtol.Smooth((lift - .55f) / .45f);
                for (var side = -1; side <= 1; side += 2)
                {
                    var nozzle = FighterVtolPresentation.Nozzle(side) * (1 + lift * .28f) + heightOffset;
                    var tip = origin + rotation.RotateVec(nozzle);
                    var ground = origin + rotation.RotateVec(FighterVtolPresentation.Nozzle(side));
                    var flicker = .88f + .12f * MathF.Sin(age * 53 + side);
                    _particles.Trail(ground, tip, .2f * power, Color.FromHex("#EDAF75").WithAlpha(.23f * power * opacity));
                    _particles.Mote(tip, new Vector2(.28f, .48f) * flicker,
                        Color.FromHex("#73BAFF").WithAlpha(power * opacity * .8f));
                    _particles.Mote(tip, new Vector2(.095f, .22f) * flicker, Color.FromHex("#E6F5FF").WithAlpha(power * opacity));
                }
            }
            // Only the server-confirmed touchdown sends a fresh outward skirt across the pad.
            var touchdownAge = (float) (now - visual.FinishedAt).TotalSeconds;
            if (visual.Outcome == FighterVtolOutcome.Touchdown && touchdownAge is >= 0 and < 1.7f)
            {
                for (var i = 0; i < 40; i++)
                {
                    var angle = i * MathF.Tau / 40;
                    var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    var radius = 1.1f + touchdownAge * (2.2f + .3f * MathF.Sin(i * 7));
                    _particles.Mote(origin + rotation.RotateVec(direction * radius + new Vector2(0, -1)),
                        new Vector2(.25f + touchdownAge * .5f, .12f + touchdownAge * .2f),
                        dust.WithAlpha((1 - touchdownAge / 1.7f) * .38f * density), angle, smoke: true, seed: i);
                }
            }
            if (++drawn >= 8) break;
        }
    }
}
