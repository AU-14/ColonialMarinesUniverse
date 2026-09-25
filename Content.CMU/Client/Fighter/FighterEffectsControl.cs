using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Fighter;

/// <summary>Aircraft-space effects. Controls and the reconnaissance image stay steady and unobscured.</summary>
public sealed class FighterEffectsControl : Control
{
    private readonly FighterParticleBatch _particles = new();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private FighterShipControl? _ship;
    private FighterAircraftComponent? _aircraft;
    private FighterWeaponsComponent? _weapons;
    private FighterAirCombatComponent? _combat;
    private FighterEffectsComponent? _effects;
    private static readonly Color Hot = Color.FromHex("#FFF1AD");
    private static readonly Color Flame = Color.FromHex("#FF8236");
    private static readonly Color Smoke = Color.FromHex("#A6ADA8");
    private static readonly Color PlasmaColor = Color.FromHex("#93FF42");

    public FighterEffectsControl()
    {
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    public void SetFlight(FighterShipControl ship, FighterAircraftComponent aircraft, FighterWeaponsComponent? weapons,
        FighterAirCombatComponent? combat, FighterEffectsComponent? effects)
    {
        _ship = ship;
        _aircraft = aircraft;
        _weapons = weapons;
        _combat = combat;
        _effects = effects;
        var kick = 0f;
        if (effects != null)
            foreach (var cue in effects.Cues)
            {
                var age = FighterEffects.Age(effects, cue, _timing.CurTime);
                if (age < 0) continue;
                if (cue.Kind == FighterEffectKind.Hit && age < 1.3f) kick += 6 * (1 - age / 1.3f);
                if (cue.Kind == FighterEffectKind.Gau && age < cue.Duration) kick += .8f;
            }
        var time = (float) _timing.CurTime.TotalSeconds;
        ship.EffectOffset = new Vector2(MathF.Sin(time * 87), MathF.Cos(time * 109)) * kick * Scale;
    }

    private float Scale => Math.Max(.5f, _ship!.PixelSize.Y / 335f);
    private Vector2 Point(float x, float y) => _ship!.Point(x, y) - GlobalPixelPosition;
    private Vector2 Mount(int slot) => _weapons != null && slot >= 0 && slot < _weapons.Hardpoints.Count
        ? _ship!.MountPosition(_weapons.Hardpoints[slot]) - GlobalPixelPosition : Point(.5f, .3f);
    private static float Noise(float seed) => (MathF.Sin(seed * 127.1f) * 43758.5453f) - MathF.Floor(MathF.Sin(seed * 127.1f) * 43758.5453f);

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_ship == null || _aircraft is not { } aircraft) return;
        _particles.Clear();
        var now = _timing.CurTime;
        var time = (float) now.TotalSeconds;
        var scale = Scale;
        var throttle = aircraft.Flying ? Math.Clamp(aircraft.Speed / aircraft.MaximumSpeed, .2f, 1) : .1f;
        for (var engine = 0; engine < 2; engine++)
        {
            var origin = Point(engine == 0 ? .455f : .545f, .91f);
            var length = (8 + throttle * 36) * scale;
            var flicker = .9f + .1f * MathF.Sin(time * 43 + engine * 8);
            _particles.Mote(origin + new Vector2(0, length * .42f), new Vector2(9 * scale, length) * flicker,
                Flame.WithAlpha(.48f));
            _particles.Mote(origin + new Vector2(0, length * .25f), new Vector2(4 * scale, length * .72f),
                Color.FromHex("#DCEFFF").WithAlpha(.9f));
            for (var i = 0; i < 7; i++)
            {
                var phase = (time * 3 + i / 7f) % 1;
                var position = origin + new Vector2(MathF.Sin(phase * 12 + engine) * phase * 4, phase * length / scale) * scale;
                _particles.Mote(position, new Vector2(4 + phase * 4) * scale, Flame.WithAlpha((1 - phase) * .25f));
            }
        }
        if (aircraft.Flying && aircraft.Height > FighterFlight.CloudTop && aircraft.Speed > 18)
            for (var wing = 0; wing < 2; wing++)
            for (var i = 0; i < 18; i++)
            {
                var phase = (time * .7f + i / 18f) % 1;
                var position = Point(wing == 0 ? .08f : .92f, .64f) + new Vector2(-aircraft.Bank * phase * 40, phase * 160) * scale;
                _particles.Mote(position, new Vector2(4 + phase * 11, 14) * scale, Color.White.WithAlpha((1 - phase) * .25f), smoke: true, seed: i);
            }
        if (aircraft.ForcedRetreat)
            DrawDamageSmoke(time, scale);
        if (_effects is { } effects)
            foreach (var cue in effects.Cues)
            {
                if (!FighterEffects.Active(effects, cue, now)) continue;
                var age = FighterEffects.Age(effects, cue, now);
                switch (cue.Kind)
                {
                    case FighterEffectKind.Gau: DrawGau(cue, age, scale); break;
                    case FighterEffectKind.Rocket:
                    case FighterEffectKind.Missile: DrawRelease(cue, age, scale); break;
                    case FighterEffectKind.Interceptor: DrawRelease(cue, age, scale, true); break;
                    case FighterEffectKind.Flares: DrawFlares(cue, age, scale); break;
                    case FighterEffectKind.Hit: DrawHit(handle, age, scale); break;
                    case FighterEffectKind.Evaded:
                        var start = Point(.65f, .62f);
                        var end = start + new Vector2(160 * age, 110 * age + 85 * age * age) * scale;
                        var evadedDirection = Vector2.Normalize(new Vector2(1, .7f + age));
                        var evadedAlpha = Math.Max(0, 1 - age / 2);
                        if (_combat is { IncomingPlasma: true }) Plasma(end, evadedDirection, evadedAlpha, scale);
                        else Missile(end, evadedDirection, evadedAlpha, scale);
                        break;
                }
            }
        if (_combat is { Incoming: true } combat)
        {
            var duration = Math.Max(.1, (combat.IncomingAt - combat.IncomingStartedAt).TotalSeconds);
            var progress = Math.Clamp(1 - (float) ((combat.IncomingAt - now).TotalSeconds / duration), 0, 1);
            var direction = ScreenDirection(combat.IncomingDirection);
            var target = Point(.65f, .62f);
            var distance = Math.Max(PixelSize.X, PixelSize.Y) * .85f * (1 - progress);
            var position = target + direction * distance;
            if (combat.IncomingFromGround && !combat.IncomingPlasma)
            {
                var side = new Vector2(-direction.Y, direction.X);
                position += side * MathF.Sin(progress * 18) * 24 * (1 - progress) * scale;
                for (var i = 1; i <= 22; i++)
                {
                    var tail = position + direction * i * 9 * scale + side * MathF.Sin(progress * 18 - i * .25f) * i * scale;
                    _particles.Mote(tail, new Vector2(3 + i * .65f) * scale,
                        Smoke.WithAlpha((1 - i / 23f) * .48f), smoke: true, seed: i);
                }
                _particles.Mote(position, new Vector2(18) * scale, Flame.WithAlpha(.4f));
            }
            if (combat.IncomingPlasma)
                Plasma(position, -direction, 1, scale * (.45f + progress * 1.2f));
            else
                Missile(position, -direction, 1, scale * (combat.IncomingFromGround ? .45f + progress * 1.2f : 1));
            var pulse = .08f + .08f * (.5f + .5f * MathF.Sin(time * 8));
            var warning = combat.IncomingPlasma ? PlasmaColor : Flame;
            handle.DrawRect(new UIBox2(0, 0, PixelSize.X, 4 * scale), warning.WithAlpha(pulse * 3));
            handle.DrawRect(new UIBox2(0, 0, 4 * scale, PixelSize.Y), warning.WithAlpha(pulse * 3));
        }
        _particles.Draw(handle, time);
    }

    private Vector2 ScreenDirection(Vector2 world)
    {
        var forward = FighterFlight.Forward(_aircraft!.Heading);
        var direction = new Vector2(Vector2.Dot(world, new Vector2(forward.Y, -forward.X)), -Vector2.Dot(world, forward));
        return direction.LengthSquared() > .001f ? Vector2.Normalize(direction) : Vector2.UnitX;
    }

    private void DrawGau(FighterEffectCue cue, float age, float scale)
    {
        var origin = Mount(cue.Index);
        var cycle = age * 18;
        var pulse = cycle - MathF.Floor(cycle);
        _particles.Mote(origin - new Vector2(0, 7) * scale, new Vector2(9, 20) * scale,
            Flame.WithAlpha(.5f + .5f * (1 - pulse)));
        _particles.Mote(origin, new Vector2(4, 9) * scale, Color.White);
        for (var i = 0; i < 5; i++)
        {
            var phase = (cycle / 3 + i / 5f) % 1;
            var position = origin + new Vector2((Noise(cue.Sequence + i) - .5f) * phase * 18, -phase * 330) * scale;
            _particles.Trail(position, position + new Vector2(0, 22) * scale, 2 * scale, Hot.WithAlpha(1 - phase));
        }
    }

    private void DrawRelease(FighterEffectCue cue, float age, float scale, bool interceptor = false)
    {
        if (age > 2) return;
        var origin = interceptor ? Point(.3f, .68f) : Mount(cue.Index);
        var direction = interceptor ? ScreenDirection(cue.Direction) : new Vector2(0, -1);
        var speed = cue.Kind == FighterEffectKind.Rocket ? 450 : 290;
        var position = origin + direction * (age * 40 + age * age * speed) * scale;
        for (var i = 0; i < 16; i++)
        {
            var tailAge = age - i * .035f;
            if (tailAge < 0) continue;
            var tail = origin + direction * (tailAge * 40 + tailAge * tailAge * speed) * scale;
            _particles.Mote(tail, new Vector2(5 + i * .65f) * scale, Smoke.WithAlpha((1 - i / 16f) * .5f), smoke: true, seed: i);
        }
        Missile(position, direction, Math.Clamp(2 - age, 0, 1), scale);
        if (age < .3f) _particles.Mote(origin, new Vector2(20) * scale, Flame.WithAlpha(1 - age / .3f));
    }

    private void Missile(Vector2 position, Vector2 direction, float alpha, float scale)
    {
        var tail = position - direction * 32 * scale;
        _particles.Trail(tail, position, 6 * scale, Flame.WithAlpha(alpha * .8f));
        _particles.Trail(position - direction * 12 * scale, position, 2.4f * scale, Color.White.WithAlpha(alpha));
        _particles.Mote(tail, new Vector2(9) * scale, Flame.WithAlpha(alpha * .6f));
    }

    private void Plasma(Vector2 position, Vector2 direction, float alpha, float scale)
    {
        for (var i = 1; i <= 14; i++)
            _particles.Mote(position - direction * i * 5 * scale, new Vector2(12 - i * .6f) * scale,
                PlasmaColor.WithAlpha(alpha * (1 - i / 15f) * .65f));
        _particles.Mote(position, new Vector2(22) * scale, PlasmaColor.WithAlpha(alpha * .75f));
        _particles.Mote(position, new Vector2(8) * scale, Color.White.WithAlpha(alpha));
    }

    private void DrawFlares(FighterEffectCue cue, float age, float scale)
    {
        for (var i = 0; i < 12; i++)
        {
            var sign = i % 2 == 0 ? -1 : 1;
            var t = age - i / 2 * .06f;
            if (t < 0) continue;
            var origin = Point(sign < 0 ? .38f : .62f, .72f);
            var velocity = new Vector2(sign * (90 + i / 2 * 21), -55 + i / 2 * 19) * scale;
            var position = origin + velocity * t + new Vector2(0, t * t * 50) * scale;
            var alpha = Math.Clamp(1 - t / 3.6f, 0, 1);
            for (var j = 1; j <= 8; j++)
            {
                var tailTime = Math.Max(0, t - j * .065f);
                var tail = origin + velocity * tailTime + new Vector2(0, tailTime * tailTime * 50) * scale;
                _particles.Mote(tail, new Vector2(4 + j) * scale, Smoke.WithAlpha(alpha * (1 - j / 9f) * .5f), smoke: true, seed: i + j);
            }
            _particles.Mote(position, new Vector2(22) * scale, Flame.WithAlpha(alpha * .85f));
            _particles.Mote(position, new Vector2(6) * scale, Hot.WithAlpha(alpha));
            _particles.Mote(position, new Vector2(2.5f) * scale, Color.White.WithAlpha(alpha));
        }
    }

    private void DrawHit(DrawingHandleScreen handle, float age, float scale)
    {
        var origin = Point(.65f, .62f);
        if (age < .7f)
        {
            _particles.Mote(origin, new Vector2(55 + age * 65) * scale, Flame.WithAlpha((1 - age / .7f) * .9f));
            _particles.Mote(origin, new Vector2(25) * scale, Color.White.WithAlpha(Math.Max(0, 1 - age * 4)));
        }
        for (var i = 0; i < 28; i++)
        {
            var angle = i * 2.4f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var position = origin + (direction * age * (30 + Noise(i) * 110) + new Vector2(0, age * age * 30)) * scale;
            _particles.Trail(position, position - direction * 9 * scale, scale * 1.5f,
                Hot.WithAlpha(Math.Max(0, 1 - age / (1.1f + Noise(i)))));
        }
        if (age < 1)
        {
            var color = Flame.WithAlpha((1 - age) * .4f);
            handle.DrawRect(new UIBox2(0, 0, PixelSize.X, 9 * scale), color);
            handle.DrawRect(new UIBox2(0, 0, 9 * scale, PixelSize.Y), color);
        }
    }

    private void DrawDamageSmoke(float time, float scale)
    {
        var origin = Point(.65f, .66f);
        for (var i = 0; i < 24; i++)
        {
            var age = (time * .65f + i / 24f) % 1;
            var position = origin + new Vector2(MathF.Sin(i * 5 + age * 3) * 18 * age, age * 185) * scale;
            _particles.Mote(position, new Vector2(10 + age * 38) * scale,
                Color.FromHex("#22262B").WithAlpha((1 - age) * .8f), smoke: true, seed: i);
        }
        _particles.Mote(origin, new Vector2(14, 23) * scale, Flame.WithAlpha(.35f + .1f * MathF.Sin(time * 18)));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _particles.Dispose();
        base.Dispose(disposing);
    }
}
