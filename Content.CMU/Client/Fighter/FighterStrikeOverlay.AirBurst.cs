using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterStrikeOverlay
{
    private void DrawAirBursts(in OverlayDrawArgs args, TimeSpan now)
    {
        var query = entities.EntityQueryEnumerator<FighterAirBurstComponent, FighterEffectsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var effects, out var xform))
        {
            if (xform.MapID != args.MapId) continue;
            var origin = _transform.GetWorldPosition(uid);
            if (!args.WorldAABB.Enlarged(10).Contains(origin)) continue;
            foreach (var cue in effects.Cues)
            {
                var age = FighterEffects.Age(effects, cue, now);
                if (age < 0 || age >= 3) continue;
                var flares = cue.Kind == FighterEffectKind.Flares;
                var fade = 1 - age / 3;
                if (!flares)
                {
                    _particles.Mote(origin, new Vector2(.3f + age * 4),
                        Color.FromHex("#FFBE77").WithAlpha(Math.Max(0, 1 - age * 3)));
                    for (var i = 0; i < 8; i++)
                    {
                        var angle = i * 2.4f;
                        var at = origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * age;
                        _particles.Mote(at, new Vector2(.4f + age * .7f),
                            Color.FromHex("#5B5650").WithAlpha(fade * .45f), smoke: true, seed: i);
                    }
                    continue;
                }
                for (var i = 0; i < 6; i++)
                {
                    var angle = (i - 2.5f) * .42f;
                    var drift = new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
                    var tip = origin + drift * age * 3;
                    _particles.Trail(tip, tip - drift * .6f, .08f,
                        Color.FromHex("#FFB957").WithAlpha(fade));
                    _particles.Mote(tip, new Vector2(.15f), Color.White.WithAlpha(fade));
                    for (var j = 1; j <= 5; j++)
                        _particles.Mote(tip - drift * j * .45f, new Vector2(.12f + j * .06f),
                            Color.FromHex("#C7C1B7").WithAlpha(fade * .3f), smoke: true, seed: i + j);
                }
            }
        }
    }
}
