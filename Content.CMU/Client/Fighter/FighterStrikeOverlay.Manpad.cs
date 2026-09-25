using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterStrikeOverlay
{
    private static readonly Color Exhaust = Color.FromHex("#F3E5C9");

    private void DrawManpadLaunches(in OverlayDrawArgs args, TimeSpan now)
    {
        var query = entities.EntityQueryEnumerator<FighterManpadVisualComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var launch, out var xform))
        {
            if (xform.MapID != args.MapId || now < launch.StartedAt || now >= launch.ExpiresAt) continue;
            var origin = _transform.GetWorldPosition(uid);
            if (!args.WorldAABB.Enlarged(40).Contains(origin)) continue;
            var age = (float) (now - launch.StartedAt).TotalSeconds;
            var direction = launch.TubeDirection;
            var side = new Vector2(-direction.Y, direction.X);
            if (!launch.Launched)
            {
                DrawManpadAcquisition(origin, direction, side, age, launch.WindupSeconds);
                continue;
            }

            // Violent ignition, backblast and a dust pressure ring, all at the real launch point.
            if (age < .18f)
            {
                var flash = 1 - age / .18f;
                _particles.Mote(origin, new Vector2(1.8f + age * 5), Blast.WithAlpha(flash));
                _particles.Mote(origin, new Vector2(.55f), Color.White.WithAlpha(flash));
                _particles.Trail(origin - direction * (2 + age * 11), origin, .75f * flash, Blast.WithAlpha(flash));
                _particles.Trail(origin - direction * (1 + age * 8), origin, .23f * flash, Color.White.WithAlpha(flash));
            }
            // The booster blooms after the missile clears the shoulder tube, then turns into the climb.
            if (age is >= .1f and < .65f)
            {
                var booster = AscentPoint(origin, direction, launch.Direction, age);
                var flash = MathF.Sin((age - .1f) / .55f * MathF.PI);
                _particles.Mote(booster, new Vector2(2 + flash * 2), Blast.WithAlpha(flash));
                _particles.Mote(booster, new Vector2(.8f), Color.White.WithAlpha(flash));
            }
            // The blast cone rolls away from the tube, then leaves a turbulent soot cloud.
            for (var i = 0; i < 16; i++)
            {
                var emitted = i * .035f;
                var plumeAge = age - emitted;
                if (plumeAge < 0 || plumeAge > 5) continue;
                var jitter = MathF.Sin(i * 5.7f);
                var plume = origin - direction * (.4f + plumeAge * (2 + i % 3)) + side * jitter * plumeAge;
                var radius = .22f + plumeAge * .7f;
                _particles.Mote(plume, new Vector2(radius, radius * 1.15f),
                    Soot.WithAlpha((1 - plumeAge / 5) * .55f), smoke: true, seed: i + 100);
                if (plumeAge < .7f)
                    _particles.Mote(plume, new Vector2(radius * .65f), Blast.WithAlpha((1 - plumeAge / .7f) * .8f));
            }
            if (age < 1.8f)
            {
                var radius = .4f + age * 5.5f;
                var alpha = (1 - age / 1.8f) * .8f;
                for (var i = 0; i < 32; i++)
                {
                    var angle = i * MathF.Tau / 32;
                    var outward = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    _particles.Mote(origin + outward * radius, new Vector2(.25f + age * .4f),
                        Dust.WithAlpha(alpha), smoke: true, seed: i);
                    if (age < .35f)
                        _particles.Mote(origin + outward * radius * 1.15f, new Vector2(.12f),
                            Exhaust.WithAlpha((1 - age / .35f) * .65f));
                    var sparkAge = Math.Min(age, .9f);
                    var spark = origin + outward * sparkAge * (2 + i % 5) + Vector2.UnitY * sparkAge * (1 - sparkAge) * 2;
                    _particles.Trail(spark, spark - outward * .5f, .065f, Spark.WithAlpha(Math.Max(0, 1 - age / .9f)));
                }
            }
            for (var i = 0; i < 12; i++)
            {
                var angle = i * 2.4f;
                var outward = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var plume = origin + outward * MathF.Sqrt(age) + new Vector2(.2f, .4f) * age;
                _particles.Mote(plume, new Vector2(.35f + age * .45f, .4f + age * .55f),
                    Dust.WithAlpha(Math.Max(0, 1 - age / 5) * .4f), smoke: true, seed: i + 50);
            }

            // The missile grows as it climbs toward the viewer; its exhaust stays behind and disperses.
            for (var i = 0; i < 40; i++)
            {
                var emittedAt = i / 40f * FighterManpadVisualComponent.AscentSeconds;
                var smokeAge = age - emittedAt;
                if (smokeAge < 0 || smokeAge > 6) continue;
                var point = AscentPoint(origin, direction, launch.Direction, emittedAt) + new Vector2(.22f, .35f) * smokeAge +
                    side * MathF.Sin(i * 1.7f + smokeAge) * smokeAge * .2f;
                var size = .2f + emittedAt * .26f + smokeAge * .42f;
                _particles.Mote(point, new Vector2(size, size * 1.3f),
                    Exhaust.WithAlpha((1 - smokeAge / 6) * .72f), smoke: true, seed: i);
            }
            if (age >= FighterManpadVisualComponent.AscentSeconds) continue;
            var progress = age / FighterManpadVisualComponent.AscentSeconds;
            var missile = AscentPoint(origin, direction, launch.Direction, age);
            var behind = AscentPoint(origin, direction, launch.Direction, Math.Max(0, age - .12f));
            var fade = Math.Min(1, (1 - progress) * 5);
            var width = .15f + progress * .6f;
            _particles.Mote(missile, new Vector2(width * 4), Blast.WithAlpha(fade * .9f));
            _particles.Trail(behind, missile, width, Blast.WithAlpha(fade));
            _particles.Trail(Vector2.Lerp(behind, missile, .55f), missile, width * .35f, Color.White.WithAlpha(fade));
            var delta = missile - behind;
            var forward = delta.LengthSquared() > .001f ? Vector2.Normalize(delta) : direction;
            var fins = new Vector2(-forward.Y, forward.X) * width * .8f;
            var nose = missile + forward * width * 2;
            _particles.Trail(missile, nose, width * .22f, Exhaust.WithAlpha(fade));
            _particles.Trail(missile - fins, missile + fins, width * .15f, Exhaust.WithAlpha(fade));
            for (var i = 1; i <= 4; i++)
                _particles.Mote(missile - forward * i * width * 1.4f, new Vector2(width * .22f),
                    Color.White.WithAlpha(fade * (1 - i / 5f)));
        }
    }

    private void DrawManpadAcquisition(Vector2 origin, Vector2 direction, Vector2 side, float age, float duration)
    {
        var progress = Math.Clamp(age / Math.Max(.01f, duration), 0, 1);
        var pulse = .35f + .65f * MathF.Pow(Math.Max(0, MathF.Sin(age * (14 + progress * 18))), 2);
        var seeker = origin;
        _particles.Mote(seeker, new Vector2(.25f + progress * .15f), Blast.WithAlpha(pulse));
        _particles.Mote(seeker, new Vector2(.07f), Color.White.WithAlpha(pulse));
        // Vent puffs and the hot seeker lens build up before any rocket flame appears.
        for (var i = 0; i < 8; i++)
        {
            var ventAge = age - i * .11f;
            if (ventAge < 0) continue;
            var sign = i % 2 == 0 ? -1 : 1;
            var vent = origin + side * sign * (.3f + ventAge * .65f) - direction * ventAge * .25f;
            _particles.Mote(vent, new Vector2(.09f + ventAge * .2f),
                Exhaust.WithAlpha(Math.Max(0, 1 - ventAge) * .3f), smoke: true, seed: i);
        }
        if (progress > .8f)
        {
            var snap = (progress - .8f) * 5;
            for (var i = -1; i <= 1; i += 2)
            {
                var cap = origin + side * i * (.4f + snap * .6f);
                _particles.Trail(cap, cap - side * i * .18f, .05f, Spark.WithAlpha(1 - snap));
            }
        }
    }

    private static Vector2 AscentPoint(Vector2 origin, Vector2 tubeDirection, Vector2 targetDirection, float seconds)
    {
        var t = seconds / FighterManpadVisualComponent.AscentSeconds;
        return origin + tubeDirection * (2 * t * (2 - t)) + targetDirection * (14 * t * t) + Vector2.UnitY * (12 * t * t);
    }
}
