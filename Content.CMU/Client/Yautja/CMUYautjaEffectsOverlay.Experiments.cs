using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Yautja;

public partial class CMUYautjaEffectsOverlay
{
    private void DrawExperiment(DrawingHandleWorld h, Vector2 at, float p, CMUYautjaExperimentPrototype recipe)
    {
        // Eight seconds: draw power in, release at 2.4s, hold the result, then fold it back into the device.
        _experimentRecipe = recipe;
        _experimentTime = p * CMUYautjaExperimentPrototype.EffectDuration;
        var charge = CMUYautjaPresentation.Segment(p, 0, CMUYautjaExperimentPrototype.ReleaseAt);
        var release = CMUYautjaPresentation.Segment(p, CMUYautjaExperimentPrototype.ReleaseAt, .86f);
        var collapse = 1 - CMUYautjaPresentation.Segment(p, CMUYautjaExperimentPrototype.CollapseAt, 1);
        var fade = CMUYautjaPresentation.Segment(p, 0, .08f) * collapse;
        var burst = p < .3f ? 0 : Math.Max(0, 1 - (p - .3f) / .10f);
        var color = recipe.Color.WithAlpha(fade);
        var rise = at + new Vector2(0, (12 + charge * 30) * collapse) / 32;
        var t = p * MathF.Tau;
        DrawExperimentMedium(h, at, rise, p, fade);
        Glow(h, at, 26 + charge * 22, color, fade * (.22f + burst * .45f));
        Glow(h, rise, 24 + charge * 18, color, fade * charge * .3f);

        // The same three-pronged power signature anchors every projection to the physical bracer.
        for (var i = 0; i < 3; i++)
        {
            var angle = i * MathF.Tau / 3 - MathF.PI / 2;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * .45f);
            var tip = at + direction * (14 + 20 * charge) / 32;
            ExperimentTrace(h, at + direction * 10 / 32, tip, color.WithAlpha(fade * .55f));
            ExperimentMote(h, tip, new Vector2(2f / 32), color);
            if (p < .3f)
            {
                var spark = at + direction * (1 - (charge * 3 + i / 3f) % 1) * 34 / 32;
                Glow(h, spark, 4, color, fade * .8f);
            }
        }
        ExperimentTrace(h, at, rise, color.WithAlpha(fade * charge * .18f));
        switch (recipe.Visual)
        {
            case CMUYautjaExperimentVisual.Scan:
                // A rising wireframe specimen volume, sliced by a travelling scanner sheet.
                for (var i = 0; i < 5; i++)
                    ExperimentPanel(h, at + new Vector2(0, i * 12 * charge) / 32,
                        28 * charge, 10, color.WithAlpha(fade * .22f));
                for (var i = -1; i <= 1; i += 2)
                    ExperimentTrace(h, at + new Vector2(i * 28, 0) / 32,
                        at + new Vector2(i * 28, 48 * charge) / 32, color.WithAlpha(fade * .5f));
                var scan = at + new Vector2(0, (release * 3 % 1) * 48 * charge) / 32;
                ExperimentPanel(h, scan, 30 * charge, 11, color);
                Glow(h, scan, 36, color, fade * .35f);
                ExperimentGlyphs(h, rise + new Vector2(1.2f, 0), p, color, .8f);
                break;
            case CMUYautjaExperimentVisual.Glyphs:
                // Three columns of alien characters assemble into a suspended translation slab.
                for (var i = -1; i <= 1; i++)
                {
                    var column = rise + new Vector2(i * 27 * charge, MathF.Sin(t + i) * 5) / 32;
                    ExperimentGlyphs(h, column, p + i * .07f, color, 1.7f);
                    ExperimentTrace(h, at, column - new Vector2(0, .5f), color.WithAlpha(fade * .2f));
                }
                ExperimentPanel(h, rise, 46 * charge, 20, color.WithAlpha(fade * .4f));
                break;
            case CMUYautjaExperimentVisual.Solvent:
                // The biological seal peels into dissolving strips over a boiling reaction pool.
                ExperimentRing(h, at, 31 * charge, t, color.WithAlpha(fade * .5f), 24, .32f);
                for (var i = 0; i < 20; i++)
                {
                    var life = (release * 2 + Random(i)) % 1;
                    var point = at + new Vector2((Random(i + 9) - .5f) * 54 * charge,
                        life * 46 + MathF.Sin(t + i) * 4) / 32;
                    ExperimentRing(h, point, 1 + life * 4, i, color.WithAlpha(fade * (1 - life)), 6);
                    ExperimentTrace(h, point, point + new Vector2(MathF.Sin(t * 2 + i) * 7, -10 * (1 - life)) / 32,
                        color.WithAlpha(fade * (1 - life) * .5f));
                }
                ExperimentMist(h, at, release, 54, color);
                break;
            case CMUYautjaExperimentVisual.Coolant:
                // Frost branches race across the floor, then ice needles grow and break away.
                for (var i = 0; i < 8; i++)
                {
                    var axis = new Vector2(MathF.Cos(i * MathF.Tau / 8), MathF.Sin(i * MathF.Tau / 8) * .55f);
                    var tip = at + axis * (12 + charge * 48) / 32;
                    ExperimentTrace(h, at, tip, color);
                    for (var j = 1; j <= 3; j++)
                    {
                        var node = Vector2.Lerp(at, tip, j / 4f);
                        var branch = new Vector2(-axis.Y, axis.X) * 7 * charge / 32;
                        ExperimentTrace(h, node, node - axis * .15f + branch, color.WithAlpha(fade * .6f));
                        ExperimentTrace(h, node, node - axis * .15f - branch, color.WithAlpha(fade * .6f));
                    }
                    var crystal = tip + new Vector2(0, 9 * charge) / 32;
                    ExperimentDiamond(h, crystal, 4 * charge, 14 * charge, color);
                }
                ExperimentMist(h, at, release, 85, color);
                ExperimentDebris(h, rise, release, Color.White.WithAlpha(fade), 20, 48, 65);
                break;
            case CMUYautjaExperimentVisual.Bridge:
                for (var side = -1; side <= 1; side += 2)
                {
                    var terminal = rise + new Vector2(side * 38 * charge, 0) / 32;
                    ExperimentPanel(h, terminal, 9, 16, color);
                    ExperimentLightning(h, at, terminal, p, color.WithAlpha(fade * .7f), side + 3);
                    ExperimentLightning(h, terminal, rise, p, Color.White.WithAlpha(fade * charge), side + 12);
                }
                Glow(h, rise, 20 + burst * 32, color, fade * .75f);
                ExperimentDebris(h, rise, release, color, 30, 58);
                break;
            case CMUYautjaExperimentVisual.Capacitor:
                ExperimentDiamond(h, rise, 14 * charge, 28 * charge, color);
                for (var i = 0; i < 3; i++)
                    ExperimentRing(h, rise, 20 + i * 10 * charge, t * (i % 2 == 0 ? 1 : -1),
                        color.WithAlpha(fade * .65f), 32, .25f + i * .23f);
                ExperimentArcs(h, rise, p, color, 7, 54 * charge);
                Glow(h, rise, 65, color, fade * (.3f + burst * .5f));
                break;
            case CMUYautjaExperimentVisual.Prism:
                ExperimentDiamond(h, rise, 16 * charge, 26 * charge, Color.White.WithAlpha(fade));
                for (var i = 0; i < 3; i++)
                {
                    var tint = (i == 0 ? Color.FromHex("#87CDFF") : i == 1 ? Color.FromHex("#FF482B") : Color.FromHex("#FFB256")).WithAlpha(fade);
                    var tip = rise + new Vector2((i - 1) * 57, 34 - Math.Abs(i - 1) * 12) * charge / 32;
                    ExperimentTrace(h, rise, tip, tint);
                    ExperimentDiamond(h, tip, 5, 9, tint);
                    Glow(h, tip, 21, tint, fade * .6f);
                    ExperimentTrace(h, tip, at, tint.WithAlpha(fade * .18f));
                }
                break;
            case CMUYautjaExperimentVisual.Veil:
                _experimentHunter ??= Art("CMU14/Yautja/body.rsi", "full");
                DrawExperimentProjection(h, _experimentHunter, rise, 1.35f, 0, color.WithAlpha(fade * (.35f - release * .3f)));
                for (var i = 0; i < 11; i++)
                {
                    var y = i * 5 - 25;
                    var width = (22 + MathF.Sin(i * .8f + t) * 5) * charge;
                    ExperimentTrace(h, rise + new Vector2(-width, y) / 32, rise + new Vector2(width, y) / 32,
                        color.WithAlpha(fade * (.08f + .22f * MathF.Pow(MathF.Sin(t * 2 + i), 2))));
                }
                ExperimentPanel(h, rise, 32 * charge, 32, color.WithAlpha(fade * .4f));
                break;
            case CMUYautjaExperimentVisual.Echo:
                for (var band = 0; band < 3; band++)
                    ExperimentWave(h, rise + new Vector2(0, (band - 1) * 14) / 32, p + band * .1f,
                        color.WithAlpha(fade * (band == 1 ? 1 : .35f)), 52 * charge, 15);
                for (var side = -1; side <= 1; side += 2)
                    ExperimentGlyphs(h, rise + new Vector2(side * 1.9f * charge, 0), p, color, .7f);
                break;
            case CMUYautjaExperimentVisual.Radar:
                var radius = 72 * charge;
                ExperimentRing(h, at, radius, 0, color.WithAlpha(fade * .5f), 48, .65f);
                ExperimentRing(h, at, radius * .5f, 0, color.WithAlpha(fade * .2f), 32, .65f);
                for (var i = 0; i < 8; i++)
                {
                    var angle = t * 3 - i * .055f;
                    ExperimentTrace(h, at, at + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * .65f) * radius / 32,
                        color.WithAlpha(fade * (1 - i / 8f) * .45f));
                }
                for (var i = 0; i < 5; i++)
                {
                    var blip = at + new Vector2(Random(i) - .5f, (Random(i + 9) - .5f) * .65f) * radius / 18;
                    ExperimentDiamond(h, blip, 4, 5, color);
                    ExperimentTrace(h, blip, blip + new Vector2(0, .35f), color.WithAlpha(fade * .4f));
                }
                break;
            case CMUYautjaExperimentVisual.Pulse:
                // A double helix untwists into a beating diagnostic trace.
                for (var i = 0; i < 16; i++)
                {
                    var y = i * 4 - 28;
                    var x = MathF.Sin(i * .5f + t * 3) * 18 * charge;
                    var left = rise + new Vector2(x, y) / 32;
                    var right = rise + new Vector2(-x, y) / 32;
                    ExperimentTrace(h, left, right, color.WithAlpha(fade * .3f));
                    ExperimentMote(h, left, new Vector2(2f / 32), color);
                    ExperimentMote(h, right, new Vector2(2f / 32), Color.White.WithAlpha(fade * .7f));
                }
                ExperimentWave(h, rise, p, color, 54 * charge, 18);
                break;
            case CMUYautjaExperimentVisual.Catalyst:
                for (var i = 0; i < 30; i++)
                {
                    var life = (release + i / 30f) % 1;
                    var angle = i * 2.4f + t * 2;
                    var point = at + new Vector2(MathF.Cos(angle) * (30 - life * 24) * charge,
                        life * 68 * charge + MathF.Sin(angle) * 7) / 32;
                    ExperimentMote(h, point, new Vector2(2f / 32), color.WithAlpha(fade * (1 - life * .6f)));
                }
                ExperimentDiamond(h, rise, 11 * charge, 19 * charge, color);
                Glow(h, rise, 38, color, fade * .6f);
                break;
            case CMUYautjaExperimentVisual.Decoy:
                _experimentHunter ??= Art("CMU14/Yautja/body.rsi", "full");
                // Ghost copies converge into a full-sized hunter; no new sprite is substituted for the body.
                for (var i = -1; i <= 1; i++)
                    DrawExperimentProjection(h, _experimentHunter, rise + new Vector2(i * (1 - charge) * 1.5f, 0), 1.5f, 0,
                        color.WithAlpha(fade * (i == 0 ? .7f : .14f)));
                for (var i = 0; i < 7; i++)
                {
                    var y = ((release * 2 + i / 7f) % 1) * 54 - 27;
                    ExperimentTrace(h, rise + new Vector2(-22, y) / 32, rise + new Vector2(22, y) / 32,
                        Color.White.WithAlpha(fade * .18f));
                }
                ExperimentTrace(h, at - new Vector2(.5f, 0), rise - new Vector2(.6f, .65f), color.WithAlpha(fade * .4f));
                ExperimentTrace(h, at + new Vector2(.5f, 0), rise + new Vector2(.6f, -.65f), color.WithAlpha(fade * .4f));
                break;
            case CMUYautjaExperimentVisual.Tether:
                for (var i = 0; i < 4; i++)
                {
                    var angle = i * MathF.PI / 2 + t * .35f;
                    var node = rise + new Vector2(MathF.Cos(angle) * 60, MathF.Sin(angle) * 34) * charge / 32;
                    ExperimentTrace(h, rise, node, color.WithAlpha(fade * .45f));
                    ExperimentPanel(h, node, 10, 10, color);
                    var packet = Vector2.Lerp(rise, node, (release * 4 + i / 4f) % 1);
                    Glow(h, packet, 6, color, fade);
                    ExperimentGlyphs(h, node, p + i * .03f, color, .55f);
                }
                break;
            case CMUYautjaExperimentVisual.Orbit:
                ExperimentRing(h, rise, 54 * charge, 0, color.WithAlpha(fade * .35f), 48, .42f);
                for (var i = 7; i >= 0; i--)
                {
                    var angle = t * 4 - i * .12f;
                    var position = rise + new Vector2(MathF.Cos(angle) * 54, MathF.Sin(angle) * 23) * charge / 32;
                    DrawExperimentProjection(h, _disc, position, i == 0 ? .85f : .65f, angle * 3,
                        color.WithAlpha(fade * (i == 0 ? 1 : (1 - i / 8f) * .15f)));
                    if (i == 0)
                    {
                        ExperimentTrace(h, at, position, color.WithAlpha(fade * .4f));
                        Glow(h, position, 16, color, fade * .5f);
                    }
                }
                break;
            case CMUYautjaExperimentVisual.Plasma:
                for (var i = -1; i <= 1; i++)
                {
                    var end = at + new Vector2(i * 30, 74 - Math.Abs(i) * 18 + MathF.Sin(t * 5 + i) * 5) * charge * collapse / 32;
                    ExperimentLightning(h, at, end, p, color, i + 20);
                    ExperimentTrace(h, at, end, Color.White.WithAlpha(fade * .8f));
                    Glow(h, end, 22, color, fade * .85f);
                }
                ExperimentDebris(h, rise, release, color, 48, 85, 70);
                ExperimentRing(h, at, 55 * release, 0, color.WithAlpha(fade * (1 - release)), 32, .4f);
                break;
            case CMUYautjaExperimentVisual.Flash:
                ExperimentDiamond(h, rise, 12 * charge, 22 * charge, color);
                if (p >= .3f)
                {
                    Glow(h, rise, 100, color, burst * .85f);
                    ExperimentRing(h, rise, 10 + release * 105, 0, color.WithAlpha(fade * (1 - release)), 48);
                    ExperimentTrace(h, rise - new Vector2(3 * burst, 0), rise + new Vector2(3 * burst, 0),
                        Color.White.WithAlpha(burst));
                    ExperimentDebris(h, rise, release, color, 32, 90, 15);
                }
                break;
            case CMUYautjaExperimentVisual.Static:
                ExperimentArcs(h, rise, p, color, 10, 65 * charge);
                for (var i = 0; i < 3; i++)
                    ExperimentGlyphs(h, rise + new Vector2((Random(i + (int) (p * 24)) - .5f) * 24,
                        (i - 1) * 24) / 32, p + i * .06f, color, 1.5f);
                Glow(h, rise, 40, color, fade * .5f);
                break;
            case CMUYautjaExperimentVisual.Containment:
                // A tall geometric cage turns inside out instead of drawing three interchangeable circles.
                for (var i = 0; i < 3; i++)
                {
                    var center = rise + new Vector2(0, (i - 1) * 25 * charge) / 32;
                    ExperimentRing(h, center, 34 * charge, t * .5f + i * release, color, 6, .4f);
                    if (i == 2)
                        continue;
                    for (var j = 0; j < 6; j++)
                    {
                        var angle = t * .5f + i * release + j * MathF.Tau / 6;
                        var from = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * .4f) * 34 * charge / 32;
                        var to = center + new Vector2(0, 25 * charge) / 32 +
                            new Vector2(MathF.Cos(angle + release), MathF.Sin(angle + release) * .4f) * 34 * charge / 32;
                        ExperimentTrace(h, from, to, color.WithAlpha(fade * .5f));
                    }
                }
                Glow(h, rise, 48, color, fade * .4f);
                break;
            case CMUYautjaExperimentVisual.Root:
                ExperimentGlyphs(h, rise, p, color, 2.4f);
                for (var i = 0; i < 5; i++)
                {
                    var angle = i * MathF.Tau / 5 + t * .15f;
                    var node = rise + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * .75f) * 57 * charge / 32;
                    ExperimentDiamond(h, node, 8, 12, color);
                    ExperimentTrace(h, node, rise, color.WithAlpha(fade * .3f));
                    ExperimentGlyphs(h, node, p + i * .04f, color, .65f);
                }
                ExperimentArcs(h, at, p, color, 5, 60 * charge);
                Glow(h, rise, 75, color, fade * (.3f + burst * .4f));
                break;
        }
    }

    private void ExperimentDebris(DrawingHandleWorld h, Vector2 at, float p, Color color,
        int count, float reach, float gravity = 25)
    {
        if (p <= 0 || p >= 1)
            return;
        for (var i = 0; i < count; i++)
        {
            var angle = Random(i + 20) * MathF.Tau;
            var velocity = new Vector2(MathF.Cos(angle), -MathF.Sin(angle));
            var position = at + (velocity * (reach * (.3f + Random(i + 80)) * p) -
                                 new Vector2(0, gravity * p * p)) / 32;
            ExperimentTrace(h, position, position - velocity * (2f / 32), color.WithAlpha(color.A * (1 - p)));
        }
    }

    private void ExperimentMist(DrawingHandleWorld h, Vector2 at, float p, float reach, Color color)
    {
        if (p <= 0 || p >= 1)
            return;
        DrawExperimentField(h, at + new Vector2(0, p * 18) / 32,
            new Vector2(20 + p * reach * 1.6f, 18 + p * reach * .6f), 1,
            color.WithAlpha(color.A * (1 - p) * .65f));
    }
    private void ExperimentPanel(DrawingHandleWorld h, Vector2 at, float width, float height, Color color)
    {
        var corner = new Vector2(width, height) / 32;
        ExperimentTrace(h, at - corner, at + new Vector2(corner.X, -corner.Y), color);
        ExperimentTrace(h, at + corner, at + new Vector2(-corner.X, corner.Y), color);
        ExperimentTrace(h, at - corner, at + new Vector2(-corner.X, corner.Y), color);
        ExperimentTrace(h, at + corner, at + new Vector2(corner.X, -corner.Y), color);
    }

    private void ExperimentDiamond(DrawingHandleWorld h, Vector2 at, float width, float height, Color color)
    {
        var top = at + new Vector2(0, height) / 32;
        var bottom = at - new Vector2(0, height) / 32;
        var left = at - new Vector2(width, 0) / 32;
        var right = at + new Vector2(width, 0) / 32;
        ExperimentTrace(h, top, left, color);
        ExperimentTrace(h, left, bottom, color);
        ExperimentTrace(h, bottom, right, color);
        ExperimentTrace(h, right, top, color);
        ExperimentTrace(h, top, bottom, color.WithAlpha(color.A * .3f));
    }

    private void ExperimentWave(DrawingHandleWorld h, Vector2 at, float p, Color color, float width, float height)
    {
        var last = at - new Vector2(width / 32, 0);
        for (var i = 0; i <= 64; i++)
        {
            var y = MathF.Sin(i * 1.1f - p * 80) * MathF.Pow(Math.Max(0, MathF.Cos(i * .16f - p * 15)), 4) * height;
            var next = at + new Vector2((i / 32f - 1) * width, y) / 32;
            ExperimentTrace(h, last, next, color);
            last = next;
        }
    }

    private void ExperimentRing(DrawingHandleWorld h, Vector2 at, float radius, float rotation,
        Color color, int sides = 32, float flatten = 1)
    {
        var last = at + new Vector2(MathF.Cos(rotation), MathF.Sin(rotation) * flatten) * radius / 32;
        for (var i = 1; i <= sides; i++)
        {
            var angle = rotation + i * MathF.Tau / sides;
            var next = at + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * flatten) * radius / 32;
            ExperimentTrace(h, last, next, color);
            last = next;
        }
    }

    private void ExperimentGlyphs(DrawingHandleWorld h, Vector2 at, float p, Color color, float scale = 1)
    {
        for (var i = 0; i < 18; i++)
        {
            var point = at + new Vector2(i % 3 * 8 - 10, i / 3 * 5 - 13) * scale / 32;
            var seed = i + (int) (p * 9);
            ExperimentTrace(h, point, point + new Vector2(3, 0) * scale / 32, color);
            ExperimentTrace(h, point, point + new Vector2(0, 3) * scale / 32, color);
            if (Random(seed) > .35f)
                ExperimentTrace(h, point + new Vector2(3, 0) * scale / 32,
                    point + new Vector2(3, 3) * scale / 32, color);
            if (Random(seed + 45) > .5f)
                ExperimentTrace(h, point, point + new Vector2(3, 3) * scale / 32, color);
        }
    }

    private void ExperimentLightning(DrawingHandleWorld h, Vector2 from, Vector2 to, float p, Color color, int seed)
    {
        var delta = to - from;
        var perpendicular = new Vector2(-delta.Y, delta.X) * .13f;
        var last = from;
        for (var i = 1; i <= 9; i++)
        {
            var next = Vector2.Lerp(from, to, i / 9f);
            if (i < 9)
                next += perpendicular * (Random(seed * 19 + i + (int) (p * 48) * 31) - .5f);
            ExperimentTrace(h, last, next, color);
            if (_experimentRecipe.Material == CMUYautjaExperimentMaterial.Discharge && i is 3 or 6)
                ExperimentTrace(h, next, next + perpendicular * (seed % 2 == 0 ? 1 : -1) + delta * .1f,
                    color.WithAlpha(color.A * .35f));
            last = next;
        }
    }

    private void ExperimentArcs(DrawingHandleWorld h, Vector2 at, float p, Color color, int count, float reach)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = i * MathF.Tau / count + p;
            var tip = at + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach / 32;
            ExperimentLightning(h, at, tip, p, color, i);
        }
    }
}
