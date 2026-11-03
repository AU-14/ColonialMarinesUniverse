using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterStrikeOverlay
{
    private readonly IPrototypeManager _airframes = IoCManager.Resolve<IPrototypeManager>();

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
            var definition = _airframes.Index(visual.Airframe);
            var rotation = _transform.GetWorldRotation(uid);
            var p = FighterVtol.Progress(now, visual.StartedAt, visual.EndsAt);
            var active = visual.Outcome == FighterVtolOutcome.Active;
            var lift = CMUFighterTransition.Lift(p, visual.Landing, definition);
            var power = FighterVtolPresentation.Power(visual, definition, now);
            var dust = FighterVtolPresentation.Dust(visual.Surface);
            var density = visual.Surface == FighterVtolSurface.Deck ? .45f : 1;

            // Reconstruct each emission at its original position on the short
            // run; dust remains on the ground as the aircraft moves and climbs.
            for (var i = 0; i < 72; i++)
            {
                var life = 1.7f + i % 7 * .15f;
                var particleAge = (age + i * .071f) % life;
                var emittedAt = now - TimeSpan.FromSeconds(particleAge);
                if (emittedAt < visual.StartedAt || !active && emittedAt >= visual.FinishedAt) continue;
                var emitted = FighterVtol.Progress(emittedAt, visual.StartedAt, visual.EndsAt);
                var height = CMUFighterTransition.Lift(emitted, visual.Landing, definition);
                var strength = CMUFighterTransition.Thrust(emitted, visual.Landing, definition) * (1 - height);
                var phase = particleAge / life;
                var angle = i * 2.39996f;
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * .6f);
                var spread = definition.LiftWash > 0 ? .6f + particleAge * (1.2f + definition.LiftWash) : particleAge * .7f;
                var socket = definition.WheelOffsets[i % definition.WheelOffsets.Length];
                var local = FighterVtolPresentation.Roll(visual, definition, emittedAt) + FighterVtolPresentation.Socket(socket);
                local += direction * spread - new Vector2(0, particleAge * .5f);
                var size = .12f + phase * .7f;
                var alpha = MathF.Sin(phase * MathF.PI) * strength * density * (.14f + definition.LiftWash * .3f);
                _particles.Mote(origin + rotation.RotateVec(local), new Vector2(size * 1.5f, size * .75f),
                    dust.WithAlpha(alpha), angle, smoke: true, seed: i * .17f);
            }

            if (active && power > .005f)
            {
                var position = origin + rotation.RotateVec(FighterVtolPresentation.Roll(visual, definition, now));
                var opacity = 1 - FighterVtol.Smooth((lift - .55f) / .45f);
                for (var i = 0; i < definition.EngineOffsets.Length; i++)
                {
                    var socket = FighterVtolPresentation.Socket(definition.EngineOffsets[i]) * (1 + lift * .28f);
                    var nozzle = position + rotation.RotateVec(socket + new Vector2(0, lift * 2.5f));
                    DrawEnginePlume(nozzle, rotation, definition, age, power, opacity, i);
                }
            }

            var touchdownAt = visual.StartedAt + (visual.EndsAt - visual.StartedAt) * definition.TouchdownPoint;
            var touchdownAge = (float) (now - touchdownAt).TotalSeconds;
            if (visual.Landing && visual.Outcome != FighterVtolOutcome.Aborted && touchdownAge is >= 0 and < 2.5f)
            {
                var fade = 1 - touchdownAge / 2.5f;
                foreach (var socket in definition.WheelOffsets)
                for (var i = 0; i < 8; i++)
                {
                    var phase = i / 8f;
                    var local = FighterVtolPresentation.Socket(socket) +
                        new Vector2(MathF.Sin(i * 7) * touchdownAge * .5f, -touchdownAge * .8f - phase * .5f);
                    var size = .15f + touchdownAge * .45f + phase * .2f;
                    _particles.Mote(origin + rotation.RotateVec(local), new Vector2(size, size * .65f),
                        dust.WithAlpha(fade * density * .28f), smoke: true, seed: i * .37f);
                }
            }
            if (++drawn >= 8) break;
        }
    }

    private void DrawEnginePlume(Vector2 nozzle, Angle rotation, CMUFighterAirframePrototype definition,
        float age, float power, float opacity, int engine)
    {
        var length = definition.ExhaustLength * MathF.Pow(power, .65f) * (1 + MathF.Sin(age * 37 + engine) * .025f);
        var forward = rotation.RotateVec(-Vector2.UnitY);
        _particles.Mote(nozzle + forward * length * .15f, new Vector2(.3f, length * .45f),
            definition.ExhaustColor.WithAlpha(power * opacity * .18f), (float) rotation.Theta);
        for (var i = 0; i < 16; i++)
        {
            var phase = i / 16f;
            var taper = MathF.Pow(1 - phase, .9f);
            var sway = MathF.Sin(phase * 16 - age * 13 + engine) * phase * phase * .07f;
            var position = nozzle + rotation.RotateVec(new Vector2(sway, -phase * length));
            var width = definition.ExhaustWidth * taper * (1 + MathF.Sin(phase * 24 - age * 3) * .12f);
            var alpha = (1 - phase * .6f) * opacity * power;
            _particles.Mote(position, new Vector2(width, length / 14),
                definition.ExhaustColor.WithAlpha(alpha * .85f), (float) rotation.Theta);
            if (phase < .7f)
                _particles.Mote(position, new Vector2(width * .38f, length / 18),
                    Color.White.WithAlpha(alpha * (1 - phase / .7f)), (float) rotation.Theta);
        }
        for (var cell = 0; cell < definition.ExhaustCells; cell++)
        {
            var phase = (cell + .7f) / (definition.ExhaustCells + 1);
            _particles.Mote(nozzle + forward * (phase * length), new Vector2(definition.ExhaustWidth * .55f * (1 - phase), .055f),
                Color.White.WithAlpha((1 - phase) * power * opacity * .8f), (float) rotation.Theta);
        }
        for (var i = 0; i < 6; i++)
        {
            var phase = (age * .7f + i / 6f) % 1;
            var position = nozzle + rotation.RotateVec(new Vector2(MathF.Sin(i * 13) * phase * .12f, -length * (.65f + phase)));
            _particles.Mote(position, new Vector2(.1f + phase * .2f, .2f + phase * .4f),
                Color.FromHex("#575965").WithAlpha((1 - phase) * power * opacity * .13f), (float) rotation.Theta, smoke: true, seed: i);
        }
    }
}
