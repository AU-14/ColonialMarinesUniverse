// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.Crafting;

// Shapes and helpers for the games that the main file does not have.
internal static class MiniDrawMore
{
    public static float Wrap(float angle)
    {
        while (angle > MathF.PI)
            angle -= MathF.Tau;
        while (angle < -MathF.PI)
            angle += MathF.Tau;

        return angle;
    }
}

// Rub the surface with the cursor, back and forth, until it is clean; what is missed stays dull. The faster the stroke, the more it takes off, and a cursor that rests takes off nothing.
public sealed class PolishField : MinigameField
{
    private const int Cols = 28;
    private const int Rows = 10;
    private const float Cell = 16f;
    private const float Left = 56f;
    private const float Top = 20f;

    private readonly float[] _dirt = new float[Cols * Rows];
    private readonly float _startDirt;
    private readonly float _duration;
    private readonly float _radius;

    private float _time;
    private Vector2 _cursor = new(280, 100);
    private Vector2 _last = new(280, 100);
    private bool _hasLast;

    // Whether the mouse is looked at: a test that plays without a mouse turns it off and puts the cursor itself.
    public bool PollMouse = true;

    public PolishField(int seed, float duration, float ease)
    {
        _duration = Math.Max(2f, duration);
        _radius = 34f * ease;

        var random = new Random(seed);
        for (var i = 0; i < _dirt.Length; i++)
            _dirt[i] = 0.75f + 0.25f * random.NextSingle();

        foreach (var d in _dirt)
            _startDirt += d;
    }

    public override string Hint => Loc.GetString("au14-craft-minigame-polish-hint");

    public float Radius => _radius;

    public float Duration => _duration;

    public void SetCursor(Vector2 designPosition) => _cursor = designPosition;

    public override float Score
    {
        get
        {
            var total = 0f;
            foreach (var d in _dirt)
                total += d;

            // How much of the dirt there was at the start is gone: an untouched surface is nothing.
            return Math.Clamp(1f - total / _startDirt, 0f, 1f);
        }
    }

    public override void Advance(float dt)
    {
        if (Finished || dt <= 0f)
            return;

        if (PollMouse)
            _cursor = MouseInDesign();

        _time += dt;

        var speed = _hasLast ? (_cursor - _last).Length() / dt : 0f;
        _last = _cursor;
        _hasLast = true;

        // A cursor that rests rubs nothing: the surface is cleaned by the stroke.
        if (speed > 50f)
        {
            var power = Math.Min(1f, speed / 150f) * 5f * dt;
            for (var row = 0; row < Rows; row++)
            {
                for (var col = 0; col < Cols; col++)
                {
                    var center = new Vector2(Left + (col + 0.5f) * Cell, Top + (row + 0.5f) * Cell);
                    var distance = (center - _cursor).Length();
                    if (distance <= _radius)
                    {
                        var i = row * Cols + col;
                        _dirt[i] = Math.Max(0f, _dirt[i] - power * (1f - 0.4f * distance / _radius));
                    }
                }
            }
        }

        if (_time >= _duration)
            Finished = true;
    }

    protected override void DrawGame(DrawingHandleScreen h)
    {
        var shine = Color.FromHex("#aeb9c4");
        var dull = Color.FromHex("#3b342c");

        for (var row = 0; row < Rows; row++)
        {
            for (var col = 0; col < Cols; col++)
            {
                var d = _dirt[row * Cols + col];
                var color = Color.InterpolateBetween(shine, dull, Math.Clamp(d, 0f, 1f));
                h.DrawRect(new UIBox2(P(Left + col * Cell, Top + row * Cell), P(Left + (col + 1) * Cell - 0.5f, Top + (row + 1) * Cell - 0.5f)), color);
            }
        }

        h.DrawRect(new UIBox2(P(Left - 2, Top - 2), P(Left + Cols * Cell + 2, Top + Rows * Cell + 2)), CraftPalette.Border, filled: false);

        // The rag: a ring where the cursor is.
        var at = P(_cursor.X, _cursor.Y);
        MiniDraw.Glow(h, at, S(_radius), CraftPalette.Gold);
        MiniDraw.Ring(h, at, S(_radius), S(2), CraftPalette.Gold.WithAlpha(0.8f));

        var left = 1f - Math.Clamp(_time / _duration, 0f, 1f);
        h.DrawRect(new UIBox2(P(Left, 188), P(Left + Cols * Cell, 193)), CraftPalette.Dark);
        h.DrawRect(new UIBox2(P(Left, 188), P(Left + Cols * Cell * left, 193)), CraftPalette.Gold.WithAlpha(0.85f));
    }
}

// Two crosshairs circle round the same ring at different speeds, and the press is made when they lie on one another. A few rounds, each a little quicker.
public sealed class CenterField : MinigameField
{
    private const float Orbit = 70f;
    private static readonly Vector2 Middle = new(280, 100);

    private readonly int _steps;
    private readonly float _tolerance;
    private readonly float[] _quality;
    private readonly Random _random;

    private int _round;
    private float _roundTime;
    private float _phaseA;
    private float _phaseB;
    private float _pause;
    private float _flash;
    private Color _flashColor = Color.White;

    public CenterField(int seed, int steps, float ease)
    {
        _steps = Math.Clamp(steps, 1, 8);
        _tolerance = 0.2f * ease;
        _quality = new float[_steps];
        Array.Fill(_quality, -1f);
        _random = new Random(seed);
        NewRound();
    }

    public override string Hint => Loc.GetString("au14-craft-minigame-center-hint");

    public int Round => _round;

    public int Steps => _steps;

    public float Tolerance => _tolerance;

    private float OmegaA => 1.25f + 0.12f * _round;

    private float OmegaB => -(0.8f + 0.1f * _round);

    public float AngleA => _phaseA + OmegaA * _roundTime;

    public float AngleB => _phaseB + OmegaB * _roundTime;

    // How far the two are from each other, round the ring.
    public float Delta => MiniDrawMore.Wrap(AngleA - AngleB);

    public override float Score
    {
        get
        {
            var total = 0f;
            foreach (var q in _quality)
                total += Math.Max(0f, q);

            return total / _steps;
        }
    }

    private void NewRound()
    {
        _roundTime = 0f;
        _phaseA = _random.NextSingle() * MathF.Tau;
        _phaseB = _random.NextSingle() * MathF.Tau;
    }

    public override void Advance(float dt)
    {
        if (Finished)
            return;

        _flash = Math.Max(0f, _flash - dt * 2.2f);

        if (_pause > 0f)
        {
            _pause -= dt;
            if (_pause <= 0f)
            {
                if (_round + 1 >= _steps)
                {
                    Finished = true;
                    return;
                }

                _round++;
                NewRound();
            }

            return;
        }

        _roundTime += dt;

        // Seven seconds and no press: the round is lost.
        if (_roundTime > 7f)
            Resolve(0f);
    }

    public override void Press()
    {
        if (!Running || Finished || _pause > 0f)
            return;

        var distance = Math.Abs(Delta);
        Resolve(distance <= _tolerance ? 0.5f + 0.5f * (1f - distance / _tolerance) : 0f);
    }

    private void Resolve(float quality)
    {
        _quality[_round] = quality;
        _pause = 0.5f;
        _flash = 1f;

        if (quality >= 0.9f)
        {
            _flashColor = CraftPalette.Good;
            Say(Loc.GetString("au14-craft-minigame-perfect"), CraftPalette.Good);
        }
        else if (quality > 0f)
        {
            _flashColor = CraftPalette.Gold;
            Say(Loc.GetString("au14-craft-minigame-hit"), CraftPalette.Gold);
        }
        else
        {
            _flashColor = CraftPalette.Bad;
            Say(Loc.GetString("au14-craft-minigame-miss"), CraftPalette.Bad);
        }
    }

    private Vector2 At(float angle) => Middle + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Orbit;

    private void Cross(DrawingHandleScreen h, Vector2 design, Color color)
    {
        var at = P(design.X, design.Y);
        MiniDraw.Line(h, at - new Vector2(S(12), 0), at + new Vector2(S(12), 0), S(3), color);
        MiniDraw.Line(h, at - new Vector2(0, S(12)), at + new Vector2(0, S(12)), S(3), color);
        MiniDraw.Ring(h, at, S(8), S(2), color);
    }

    protected override void DrawGame(DrawingHandleScreen h)
    {
        var middle = P(Middle.X, Middle.Y);
        MiniDraw.Ring(h, middle, S(Orbit), S(10), CraftPalette.Dark);
        MiniDraw.Ring(h, middle, S(Orbit + 5), S(1.5f), CraftPalette.Border);
        MiniDraw.Ring(h, middle, S(Orbit - 5), S(1.5f), CraftPalette.Border);
        h.DrawCircle(middle, S(4), CraftPalette.Border);

        var close = Math.Abs(Delta) <= _tolerance && _pause <= 0f;
        var a = At(AngleA);
        var b = At(AngleB);

        if (close)
            MiniDraw.Glow(h, P(a.X, a.Y), S(28), CraftPalette.Good);

        Cross(h, a, close ? CraftPalette.Good : CraftPalette.Gold);
        Cross(h, b, close ? CraftPalette.Good : CraftPalette.Text);

        if (_flash > 0f)
            MiniDraw.Ring(h, P(a.X, a.Y), S(14 + 40 * (1f - _flash)), S(3), _flashColor.WithAlpha(_flash));

        // The rounds, as dots.
        var gap = 30f;
        var start = 280f - gap * (_steps - 1) / 2f;
        for (var i = 0; i < _steps; i++)
        {
            var dot = P(start + gap * i, 184);
            if (_quality[i] >= 0f)
            {
                var color = _quality[i] >= 0.9f ? CraftPalette.Good : _quality[i] > 0f ? CraftPalette.Gold : CraftPalette.Bad;
                h.DrawCircle(dot, S(8), color);
            }
            else
            {
                h.DrawCircle(dot, S(8), CraftPalette.Dark);
                MiniDraw.Ring(h, dot, S(8), S(2), (i == _round ? CraftPalette.Gold : CraftPalette.Border).WithAlpha(i == _round ? 0.55f + 0.45f * MathF.Sin(Clock * 5f) : 0.3f));
            }
        }
    }
}

// A crack runs round the bit, quicker and slower, nearer and farther; the cursor goes with it, and the time it is on the crack is the score.
public sealed class DrillField : MinigameField
{
    private static readonly Vector2 Middle = new(280, 100);

    private readonly float _duration;
    private readonly float _radius;
    private readonly float _a0;
    private readonly float _p1;
    private readonly float _p2;

    private float _time;
    private float _sum;
    private Vector2 _cursor = new(280, 100);
    private bool _near;

    // Whether the mouse is looked at: a test that plays without a mouse turns it off and puts the cursor itself.
    public bool PollMouse = true;

    public DrillField(int seed, float duration, float ease)
    {
        _duration = Math.Max(1f, duration);
        _radius = 30f * ease;

        var random = new Random(seed);
        _a0 = random.NextSingle() * MathF.Tau;
        _p1 = random.NextSingle() * MathF.Tau;
        _p2 = random.NextSingle() * MathF.Tau;
    }

    public override string Hint => Loc.GetString("au14-craft-minigame-drill-hint");

    public float Radius => _radius;

    public void SetCursor(Vector2 designPosition) => _cursor = designPosition;

    // Where the crack is at this time.
    public Vector2 CrackAt(float time)
    {
        var angle = _a0 + 0.9f * time + 0.6f * MathF.Sin(0.8f * time + _p1);
        var distance = 58f + 24f * MathF.Sin(1.3f * time + _p2);
        return Middle + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
    }

    public Vector2 Crack => CrackAt(_time);

    public override float Score => Finished ? _sum / _duration : _time <= 0.001f ? 0f : _sum / _time;

    public override void Advance(float dt)
    {
        if (Finished)
            return;

        if (PollMouse)
            _cursor = MouseInDesign();

        _time += dt;

        var distance = (_cursor - Crack).Length();
        var credit = Math.Clamp(1f - (distance - _radius) / (0.7f * _radius), 0f, 1f);
        _near = distance <= _radius;
        _sum += credit * dt;

        if (_time >= _duration)
            Finished = true;
    }

    protected override void DrawGame(DrawingHandleScreen h)
    {
        var middle = P(Middle.X, Middle.Y);

        // The rock and the way the crack runs in it.
        h.DrawCircle(middle, S(96), CraftPalette.Dark);
        MiniDraw.Ring(h, middle, S(96), S(3), CraftPalette.Border);
        MiniDraw.Ring(h, middle, S(58), S(1), CraftPalette.Border.WithAlpha(0.4f));

        // The trail of the crack, the last stretch of its way.
        var trail = new Vector2[24];
        for (var i = 0; i < trail.Length; i++)
        {
            var at = CrackAt(Math.Max(0f, _time - 0.04f * (trail.Length - 1 - i)));
            trail[i] = P(at.X, at.Y);
        }

        MiniDraw.Stroke(h, trail, trail.Length, S(3), CraftPalette.Gold.WithAlpha(0.7f));

        // The zone round the crack, and the crack in it.
        var crack = P(Crack.X, Crack.Y);
        MiniDraw.Glow(h, crack, S(_radius), CraftPalette.Gold);
        MiniDraw.Ring(h, crack, S(_radius), S(2), CraftPalette.Gold.WithAlpha(0.8f));
        h.DrawCircle(crack, S(5), CraftPalette.Text);

        // The bit: a spinning star in the middle.
        for (var i = 0; i < 3; i++)
        {
            var a = Clock * 9f + i * MathF.Tau / 3f;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a)) * S(16);
            MiniDraw.Line(h, middle - d, middle + d, S(4), CraftPalette.Muted);
        }

        h.DrawCircle(middle, S(5), CraftPalette.Gold);

        // The cursor: green with the crack, red when it has lost it.
        var cursor = P(_cursor.X, _cursor.Y);
        var color = _near || !Running ? CraftPalette.Good : CraftPalette.Bad;
        MiniDraw.Ring(h, cursor, S(11), S(3), color);
        h.DrawCircle(cursor, S(2.5f), color);

        var left = 1f - Math.Clamp(_time / _duration, 0f, 1f);
        h.DrawRect(new UIBox2(P(60, 194), P(500, 198)), CraftPalette.Dark);
        h.DrawRect(new UIBox2(P(60, 194), P(60 + 440 * left, 198)), CraftPalette.Gold.WithAlpha(0.85f));
    }
}

// A needle runs along a line of holes, and a press at each hole sews a stitch: the stitches of a seam are not equally far apart, so the beat is not even. A press where there is no
// hole is a stab in the cloth, and it spoils a little.
public sealed class StitchField : MinigameField
{
    private const float NeedleSpeed = 130f;
    private const float Start = 40f;

    private readonly int _count;
    private readonly float _tolerance;
    private readonly float[] _x;
    private readonly float[] _y;
    private readonly float[] _quality;

    private float _time;
    private int _wild;

    public StitchField(int seed, int steps, float ease)
    {
        _count = Math.Clamp(steps + 1, 4, 7);
        _tolerance = 12f * ease;
        _x = new float[_count];
        _y = new float[_count];
        _quality = new float[_count];
        Array.Fill(_quality, -1f);

        var random = new Random(seed);
        var weights = new float[_count - 1];
        var sum = 0f;
        for (var i = 0; i < weights.Length; i++)
        {
            weights[i] = 0.75f + 0.6f * random.NextSingle();
            sum += weights[i];
        }

        var phase = random.NextSingle() * MathF.Tau;
        var x = 75f;
        for (var i = 0; i < _count; i++)
        {
            _x[i] = x;
            _y[i] = 100f + 36f * MathF.Sin(i * 1.3f + phase);
            if (i < weights.Length)
                x += 420f * weights[i] / sum;
        }
    }

    public override string Hint => Loc.GetString("au14-craft-minigame-stitch-hint");

    public int Count => _count;

    public float NeedleX => Start + NeedleSpeed * _time;

    public float HoleX(int index) => _x[index];

    public int Wild => _wild;

    // The next hole that has not been sewn.
    public int Next
    {
        get
        {
            for (var i = 0; i < _count; i++)
            {
                if (_quality[i] < 0f)
                    return i;
            }

            return -1;
        }
    }

    public override float Score
    {
        get
        {
            var total = 0f;
            foreach (var q in _quality)
                total += Math.Max(0f, q);

            return Math.Max(0f, total / _count - 0.06f * _wild);
        }
    }

    public override void Advance(float dt)
    {
        if (Finished)
            return;

        _time += dt;

        // A hole that the needle has passed is a stitch that was not made.
        for (var i = 0; i < _count; i++)
        {
            if (_quality[i] < 0f && NeedleX - _x[i] > _tolerance)
            {
                _quality[i] = 0f;
                Say(Loc.GetString("au14-craft-minigame-miss"), CraftPalette.Bad);
            }
        }

        if (NeedleX > _x[_count - 1] + 45f)
            Finished = true;
    }

    public override void Press()
    {
        if (!Running || Finished)
            return;

        for (var i = 0; i < _count; i++)
        {
            var distance = Math.Abs(NeedleX - _x[i]);
            if (_quality[i] >= 0f || distance > _tolerance)
                continue;

            _quality[i] = 0.5f + 0.5f * (1f - distance / _tolerance);
            Say(_quality[i] >= 0.9f ? Loc.GetString("au14-craft-minigame-perfect") : Loc.GetString("au14-craft-minigame-hit"), _quality[i] >= 0.9f ? CraftPalette.Good : CraftPalette.Gold);
            return;
        }

        _wild++;
        Say(Loc.GetString("au14-craft-minigame-stab"), CraftPalette.Bad);
    }

    // The line of the seam between the holes, at any place along it.
    private float YAt(float x)
    {
        if (x <= _x[0])
            return _y[0];

        for (var i = 0; i + 1 < _count; i++)
        {
            if (x <= _x[i + 1])
                return _y[i] + (_y[i + 1] - _y[i]) * (x - _x[i]) / (_x[i + 1] - _x[i]);
        }

        return _y[_count - 1];
    }

    protected override void DrawGame(DrawingHandleScreen h)
    {
        // The cloth: a weave of faint lines.
        var weave = CraftPalette.Border.WithAlpha(0.18f);
        for (var x = 20; x <= 540; x += 8)
            h.DrawLine(P(x, 20), P(x, 180), weave);
        for (var y = 20; y <= 180; y += 8)
            h.DrawLine(P(20, y), P(540, y), weave);

        // The seam to be sewn, dashed; the thread, gold, between the holes that have been sewn in turn.
        for (var i = 0; i + 1 < _count; i++)
        {
            var a = P(_x[i], _y[i]);
            var b = P(_x[i + 1], _y[i + 1]);
            if (_quality[i] > 0f && _quality[i + 1] > 0f)
            {
                MiniDraw.Line(h, a, b, S(4), CraftPalette.Gold);
            }
            else
            {
                var steps = 6;
                for (var k = 0; k < steps; k += 2)
                    MiniDraw.Line(h, Vector2.Lerp(a, b, k / (float) steps), Vector2.Lerp(a, b, (k + 1) / (float) steps), S(2), CraftPalette.Muted.WithAlpha(0.7f));
            }
        }

        // The holes, with the window in which a press counts.
        for (var i = 0; i < _count; i++)
        {
            var hole = P(_x[i], _y[i]);
            var color = _quality[i] < 0f ? CraftPalette.Text : _quality[i] >= 0.9f ? CraftPalette.Good : _quality[i] > 0f ? CraftPalette.Gold : CraftPalette.Bad;
            if (_quality[i] < 0f)
                h.DrawRect(new UIBox2(P(_x[i] - _tolerance, _y[i] - 22), P(_x[i] + _tolerance, _y[i] + 22)), CraftPalette.Good.WithAlpha(0.10f));

            h.DrawCircle(hole, S(6), CraftPalette.Inset);
            MiniDraw.Ring(h, hole, S(6), S(2), color);
        }

        // The needle on the seam.
        var needleX = Math.Min(NeedleX, 540f);
        var seamY = YAt(needleX);
        var top = P(needleX, seamY - 26);
        var bottom = P(needleX, seamY + 10);
        MiniDraw.Glow(h, bottom, S(14), CraftPalette.Gold);
        MiniDraw.Line(h, top, bottom, S(3), CraftPalette.Text);
        MiniDraw.Ring(h, P(needleX, seamY - 20), S(3), S(1.5f), CraftPalette.Gold);
    }
}
