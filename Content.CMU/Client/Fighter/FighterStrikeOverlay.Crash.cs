using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterStrikeOverlay
{
    private static readonly Vector2[] WreckFires = [new(-1.2f, .8f), new(1.2f, .8f), new(0, 2.1f), new(0, -.5f)];

    private void DrawBurningWrecks(in OverlayDrawArgs args, TimeSpan now)
    {
        var query = entities.EntityQueryEnumerator<FighterGroundComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var ground, out var transform))
        {
            // Follow persistent replicated wreck state, rather than the short-lived impact cue.
            // This also shows the fire to spectators arriving after the impact has expired.
            if (ground.State != FighterGroundState.Crashed || transform.MapID != args.MapId) continue;
            var matrix = _transform.GetWorldMatrix(uid);
            if (!args.WorldAABB.Enlarged(12).Contains(matrix.Translation)) continue;
            var clock = (float) now.TotalSeconds;
            for (var fire = 0; fire < WreckFires.Length; fire++)
            {
                var origin = Vector2.Transform(WreckFires[fire], matrix);
                // Draw smoke first so the flame cores remain visible over the scorched airframe.
                for (var puff = 0; puff < 7; puff++)
                {
                    var phase = (clock * .16f + puff / 7f + fire * .17f) % 1;
                    var drift = new Vector2(phase * 2 + MathF.Sin(clock + puff) * .2f, phase * 5);
                    _particles.Mote(origin + drift, new Vector2(.55f + phase * 1.4f),
                        Color.FromHex("#655E57").WithAlpha((1 - phase) * .42f), smoke: true, seed: fire * 7 + puff);
                }
                _particles.Mote(origin, new Vector2(1.25f, .75f),
                    Color.FromHex("#FF7025").WithAlpha(.4f + MathF.Sin(clock * 7 + fire) * .08f));
                for (var tongue = 0; tongue < 6; tongue++)
                {
                    var phase = (clock * .85f + tongue / 6f + fire * .23f) % 1;
                    var flame = origin + new Vector2(MathF.Sin(clock * 8 + tongue * 2) * (.15f + phase * .2f), phase * 1.9f);
                    _particles.Mote(flame, new Vector2(.42f * (1 - phase) + .12f, .6f * (1 - phase) + .15f),
                        Color.FromHex(tongue % 2 == 0 ? "#FF6926" : "#FFD16C").WithAlpha((1 - phase) * .9f));
                    _particles.Mote(origin + new Vector2(0, phase * .6f), new Vector2(.17f, .25f),
                        Color.FromHex("#FFF0AC").WithAlpha((1 - phase) * .65f));
                }
                var emberPhase = (clock * .4f + fire * .21f) % 1;
                var ember = origin + new Vector2(MathF.Sin(clock + fire) * emberPhase, emberPhase * 3.5f);
                _particles.Trail(ember, ember - new Vector2(.05f, .2f), .035f,
                    Color.FromHex("#FFD16C").WithAlpha(1 - emberPhase));
            }
        }
    }

    private void DrawCrashingFighters(in OverlayDrawArgs args, TimeSpan now)
    {
        var query = entities.EntityQueryEnumerator<FighterFlybyComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var flyby, out var transform))
        {
            if (!flyby.Crashing || transform.MapID != args.MapId) continue;
            var point = _transform.GetWorldPosition(uid);
            if (!args.WorldAABB.Enlarged(24).Contains(point)) continue;
            var behind = -_transform.GetWorldRotation(uid).RotateVec(Vector2.UnitY);
            var age = (float) now.TotalSeconds;
            var sideways = new Vector2(-behind.Y, behind.X);
            for (var i = 0; i < 32; i++)
            {
                var offset = i * .65f + age * 4 % .65f;
                var smoke = point + behind * (offset + 1.6f) + sideways * MathF.Sin(i + age * 3) * (.2f + i * .025f);
                _particles.Mote(smoke, new Vector2(.5f + i * .1f),
                    Color.FromHex("#282524").WithAlpha((1 - i / 32f) * .9f), smoke: true, seed: i);
                if (i < 9)
                {
                    _particles.Mote(smoke, new Vector2(.65f + .2f * MathF.Sin(age * 30 + i)),
                        Color.FromHex(i % 2 == 0 ? "#FFB640" : "#FA5124").WithAlpha(1 - i / 9f));
                    _particles.Trail(smoke, smoke + behind * .7f, .12f,
                        Color.FromHex("#FFF1BF").WithAlpha(1 - i / 9f));
                }
            }
            for (var i = 0; i < 12; i++)
            {
                var phase = (age * .8f + i / 12f) % 1;
                var ember = point + behind * (2 + phase * 16) + sideways * MathF.Sin(i * 7) * phase * 4;
                _particles.Trail(ember, ember - behind * .5f, .04f,
                    Color.FromHex("#FFB640").WithAlpha(1 - phase));
            }
        }
    }

    private void DrawCrashImpact(Vector2 origin, float age, Vector2 direction)
    {
        // Fixed-size, cosmetic geometry: the shockwave and fragments cannot damage bystanders.
        var fade = 1 - age / FighterEffects.HistorySeconds;
        var flash = Math.Max(0, 1 - age / .45f);
        _particles.Mote(origin, new Vector2(2 + age * 12), Color.FromHex("#FFF3CC").WithAlpha(flash));

        if (age < 1.6f)
        {
            for (var i = 0; i < 36; i++)
            {
                var radial = new Angle(i * Math.PI / 18).RotateVec(Vector2.UnitX);
                var point = origin + radial * (1 + age * 9);
                _particles.Mote(point, new Vector2(.4f + age * .6f),
                    Color.FromHex("#B4A18A").WithAlpha((1 - age / 1.6f) * .65f), smoke: true, seed: i);
            }
        }

        for (var i = 0; i < 14; i++)
        {
            var radial = new Angle(i * 2.4).RotateVec(Vector2.UnitX);
            var spread = radial * (1 + Math.Min(age, 3) * (1 + i % 3 * .25f));
            var smoke = origin + spread + direction * Math.Min(age, 2) + new Vector2(age * .3f, age * .5f);
            _particles.Mote(smoke, new Vector2(1.2f + age * .55f),
                Color.FromHex(i % 2 == 0 ? "#292626" : "#51463F").WithAlpha(fade * .8f), smoke: true, seed: i);
            var fireAge = age - i % 3 * .18f;
            if (fireAge is >= 0 and < 2.2f)
            {
                _particles.Mote(origin + spread * .6f, new Vector2(.9f + fireAge * .9f),
                    Color.FromHex(i % 2 == 0 ? "#FF6C24" : "#FFD06A").WithAlpha(1 - fireAge / 2.2f));
            }
            if (age < 2.5f)
            {
                var debris = origin + radial * age * (4 + i % 4) + direction * age * 2;
                _particles.Trail(debris, debris - radial * .8f, .06f,
                    Color.FromHex("#FFCA76").WithAlpha(1 - age / 2.5f));
            }
        }
    }
}
