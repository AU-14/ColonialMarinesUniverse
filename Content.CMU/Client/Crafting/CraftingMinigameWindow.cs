// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System;
using System.Numerics;
using Content.Shared.CMU14.Crafting;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Crafting;

// The colors of the crafting window, so that the games look like part of it.
public static class CraftPalette
{
    public static readonly Color Panel = Color.FromHex("#12161cdd");
    public static readonly Color Border = Color.FromHex("#4a5360");
    public static readonly Color Card = Color.FromHex("#27303b");
    public static readonly Color Dark = Color.FromHex("#1b2129");
    public static readonly Color Inset = Color.FromHex("#0d1116");
    public static readonly Color Gold = Color.FromHex("#e0b25a");
    public static readonly Color Text = Color.FromHex("#f2f4f7");
    public static readonly Color Muted = Color.FromHex("#9aa3ad");
    public static readonly Color Good = Color.FromHex("#6ac47c");
    public static readonly Color Bad = Color.FromHex("#d9503f");
}

// The shapes the games are drawn with: all of them are lines, circles and bars, so that no picture is needed.
internal static class MiniDraw
{
    public static void Line(DrawingHandleScreen h, Vector2 a, Vector2 b, float width, Color color)
    {
        var d = b - a;
        var length = d.Length();
        if (length < 0.01f)
            return;

        var n = new Vector2(-d.Y, d.X) / length * (width / 2f);
        h.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, new[] { a + n, a - n, b + n, b - n }, color);
    }

    // A line with round joints, so that a curve made of short pieces looks like one stroke.
    public static void Stroke(DrawingHandleScreen h, Vector2[] points, int count, float width, Color color)
    {
        for (var i = 0; i + 1 < count; i++)
            Line(h, points[i], points[i + 1], width, color);

        for (var i = 0; i < count; i++)
            h.DrawCircle(points[i], width / 2f, color);
    }

    public static void Pill(DrawingHandleScreen h, UIBox2 box, Color color)
    {
        var radius = Math.Min(box.Width, box.Height) / 2f;
        if (box.Width >= box.Height)
        {
            h.DrawRect(new UIBox2(box.Left + radius, box.Top, box.Right - radius, box.Bottom), color);
            h.DrawCircle(new Vector2(box.Left + radius, box.Center.Y), radius, color);
            h.DrawCircle(new Vector2(box.Right - radius, box.Center.Y), radius, color);
        }
        else
        {
            h.DrawRect(new UIBox2(box.Left, box.Top + radius, box.Right, box.Bottom - radius), color);
            h.DrawCircle(new Vector2(box.Center.X, box.Top + radius), radius, color);
            h.DrawCircle(new Vector2(box.Center.X, box.Bottom - radius), radius, color);
        }
    }

    public static void Glow(DrawingHandleScreen h, Vector2 center, float radius, Color color)
    {
        for (var i = 4; i >= 1; i--)
            h.DrawCircle(center, radius * (0.45f + 0.18f * i), color.WithAlpha(color.A * 0.12f));
    }

    public static void Ring(DrawingHandleScreen h, Vector2 center, float radius, float width, Color color)
    {
        const int pieces = 40;
        var previous = center + new Vector2(radius, 0);
        for (var i = 1; i <= pieces; i++)
        {
            var angle = MathF.Tau * i / pieces;
            var next = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            Line(h, previous, next, width, color);
            previous = next;
        }
    }

    public static void Triangle(DrawingHandleScreen h, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        h.DrawPrimitives(DrawPrimitiveTopology.TriangleList, new[] { a, b, c }, color);
    }
}

// The play field of a game. It is drawn in a fixed size of its own and scaled to fit, so that the game is the same however large the window is, and everything is counted in
// that size too (a test can play a game without a screen).
public abstract class MinigameField : Control
{
    public static readonly Vector2 Design = new(560, 200);

    protected float Clock;

    // Set by the window when the countdown is over.
    public bool Running;

    public bool Finished { get; protected set; }

    // How well it has gone so far, from 0 to 1; when the game is finished, how well it went.
    public abstract float Score { get; }

    // What a player is told about how to play.
    public abstract string Hint { get; }

    // A word about the last move, with its color.
    public event Action<string, Color>? OnFeedback;

    protected MinigameField()
    {
        MouseFilter = MouseFilterMode.Pass;
        MinSize = new Vector2(560, 200);
        RectClipContent = true;
    }

    protected void Say(string word, Color color) => OnFeedback?.Invoke(word, color);

    public void Animate(float dt) => Clock += dt;

    public abstract void Advance(float dt);

    // The button of the mouse went down (the rhythm), is held (the hold game)...
    public virtual void Press()
    {
    }

    public virtual bool Held { get; set; }

    protected float FieldScale => PixelSize.X <= 0 || PixelSize.Y <= 0 ? 1f : Math.Min(PixelSize.X / Design.X, PixelSize.Y / Design.Y);

    protected Vector2 Origin => PixelSize.X <= 0 ? Vector2.Zero : (new Vector2(PixelSize.X, PixelSize.Y) - Design * FieldScale) / 2f;

    protected Vector2 P(float x, float y) => Origin + new Vector2(x, y) * FieldScale;

    protected float S(float size) => size * FieldScale;

    // Where the mouse is, in the size the game is counted in.
    protected Vector2 MouseInDesign()
    {
        var pixel = UserInterfaceManager.MousePositionScaled.Position * UIScale - GlobalPixelPosition;
        return (pixel - Origin) / FieldScale;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);
        handle.DrawRect(box, CraftPalette.Inset);
        handle.DrawRect(box, CraftPalette.Dark, filled: false);

        // A faint grid, so that the field is not an empty black.
        var grid = CraftPalette.Border.WithAlpha(0.12f);
        for (var x = 0; x <= 560; x += 40)
            handle.DrawLine(P(x, 0), P(x, 200), grid);
        for (var y = 0; y <= 200; y += 40)
            handle.DrawLine(P(0, y), P(560, y), grid);

        DrawGame(handle);

        if (!Running && !Finished)
            handle.DrawRect(box, Color.Black.WithAlpha(0.28f));
    }

    protected abstract void DrawGame(DrawingHandleScreen handle);
}

// A marker sweeps over a bar, and the player presses when it is in the zone: a few times, the zone is somewhere else each time and the marker quicker.
public sealed class RhythmField : MinigameField
{
    private const float BarLeft = 40f;
    private const float BarWidth = 480f;

    private readonly float[] _centers;
    private readonly float[] _quality;
    private readonly float _half;
    private readonly int _steps;

    private int _beat;
    private float _marker;
    private int _direction = 1;
    private float _beatTime;
    private float _pause;
    private float _flash;
    private float _flashAt;
    private Color _flashColor = Color.White;

    public RhythmField(int seed, int steps, float ease)
    {
        _steps = Math.Clamp(steps, 1, 8);
        _half = 0.075f * ease;
        _centers = new float[_steps];
        _quality = new float[_steps];

        var random = new Random(seed);
        var previous = 0.5f;
        for (var i = 0; i < _steps; i++)
        {
            float c;
            var tries = 0;
            do
            {
                c = 0.18f + 0.64f * random.NextSingle();
            }
            while (Math.Abs(c - previous) < 0.22f && ++tries < 20);

            _centers[i] = c;
            _quality[i] = -1f;
            previous = c;
        }

        _marker = 0f;
    }

    public override string Hint => Loc.GetString("au14-craft-minigame-rhythm-hint");

    public float ZoneCenter => _centers[Math.Min(_beat, _steps - 1)];

    public float ZoneHalf => _half;

    public float Marker => _marker;

    public int Beat => _beat;

    public int Steps => _steps;

    private float Speed => 0.85f + 0.14f * _beat;

    public override float Score
    {
        get
        {
            var total = 0f;
            for (var i = 0; i < _steps; i++)
                total += Math.Max(0f, _quality[i]);

            return total / _steps;
        }
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
                if (_beat + 1 >= _steps)
                {
                    Finished = true;
                    return;
                }

                _beat++;
                _beatTime = 0f;
                _marker = _direction > 0 ? 0f : 1f;
            }

            return;
        }

        _beatTime += dt;
        _marker += _direction * Speed * dt;
        if (_marker >= 1f)
        {
            _marker = 1f;
            _direction = -1;
        }
        else if (_marker <= 0f)
        {
            _marker = 0f;
            _direction = 1;
        }

        // Two passes of the bar and no press: the beat is missed.
        if (_beatTime > 4.5f / Speed)
            Resolve(0f);
    }

    public override void Press()
    {
        if (!Running || Finished || _pause > 0f)
            return;

        var distance = Math.Abs(_marker - ZoneCenter);
        Resolve(distance <= _half ? 0.5f + 0.5f * (1f - distance / _half) : 0f);
    }

    private void Resolve(float quality)
    {
        _quality[_beat] = quality;
        _pause = 0.5f;
        _flash = 1f;
        _flashAt = _marker;

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

    protected override void DrawGame(DrawingHandleScreen h)
    {
        const float top = 74f;
        const float height = 52f;

        // The bar.
        MiniDraw.Pill(h, new UIBox2(P(BarLeft - 6, top - 5), P(BarLeft + BarWidth + 6, top + height + 5)), CraftPalette.Border);
        MiniDraw.Pill(h, new UIBox2(P(BarLeft, top), P(BarLeft + BarWidth, top + height)), CraftPalette.Dark);

        // The zone, brighter when the marker is in it, with a core that is the best place to press.
        var center = BarLeft + ZoneCenter * BarWidth;
        var half = _half * BarWidth;
        var inside = Math.Abs(_marker - ZoneCenter) <= _half && _pause <= 0f;
        var zoneAlpha = inside ? 0.55f : 0.32f;
        h.DrawRect(new UIBox2(P(center - half, top + 3), P(center + half, top + height - 3)), CraftPalette.Good.WithAlpha(zoneAlpha));
        h.DrawRect(new UIBox2(P(center - half * 0.3f, top + 3), P(center + half * 0.3f, top + height - 3)), CraftPalette.Good.WithAlpha(inside ? 0.9f : 0.5f));
        h.DrawRect(new UIBox2(P(center - half, top + 3), P(center + half, top + height - 3)), CraftPalette.Good.WithAlpha(0.8f), filled: false);

        // The marker, with a glow and a pointer at each end.
        var x = BarLeft + _marker * BarWidth;
        var glow = P(x, top + height / 2f);
        MiniDraw.Glow(h, glow, S(26), inside ? CraftPalette.Good : CraftPalette.Gold);
        MiniDraw.Line(h, P(x, top - 12), P(x, top + height + 12), S(4), CraftPalette.Text);
        MiniDraw.Triangle(h, P(x - 8, top - 22), P(x + 8, top - 22), P(x, top - 8), CraftPalette.Gold);
        MiniDraw.Triangle(h, P(x - 8, top + height + 22), P(x + 8, top + height + 22), P(x, top + height + 8), CraftPalette.Gold);

        // The ripple of the last press.
        if (_flash > 0f)
        {
            var at = P(BarLeft + _flashAt * BarWidth, top + height / 2f);
            MiniDraw.Ring(h, at, S(14 + 40 * (1f - _flash)), S(3), _flashColor.WithAlpha(_flash));
        }

        // The presses: one dot for each, filled by how well it went; the one to come is a gold ring that breathes.
        var gap = 30f;
        var start = 280f - gap * (_steps - 1) / 2f;
        for (var i = 0; i < _steps; i++)
        {
            var dot = P(start + gap * i, 168);
            if (_quality[i] >= 0f)
            {
                var color = _quality[i] >= 0.9f ? CraftPalette.Good : _quality[i] > 0f ? CraftPalette.Gold : CraftPalette.Bad;
                h.DrawCircle(dot, S(9), color);
            }
            else
            {
                h.DrawCircle(dot, S(9), CraftPalette.Dark);
                var breathe = i == _beat ? 0.55f + 0.45f * MathF.Sin(Clock * 5f) : 0.3f;
                MiniDraw.Ring(h, dot, S(9), S(2), (i == _beat ? CraftPalette.Gold : CraftPalette.Border).WithAlpha(breathe));
            }
        }
    }
}

// A marker falls, and rises while the mouse button is held: it has to be kept in a zone that drifts up and down. The time in the zone is the score.
public sealed class HoldField : MinigameField
{
    private const float BarTop = 12f;
    private const float BarHeight = 176f;

    private readonly float _duration;
    private readonly float _half;
    private readonly float _phaseA;
    private readonly float _phaseB;

    private float _time;
    private float _inside;
    private float _position;
    private float _speed;

    public HoldField(int seed, float duration, float ease)
    {
        _duration = Math.Max(1f, duration);
        _half = 0.14f * ease;

        var random = new Random(seed);
        _phaseA = random.NextSingle() * MathF.Tau;
        _phaseB = random.NextSingle() * MathF.Tau;

        // The game starts with the marker in the zone, so that the first seconds are not spent climbing to it.
        _position = ZoneAt(0f);
    }

    public override string Hint => Loc.GetString("au14-craft-minigame-hold-hint");

    public float Position => _position;

    public float ZoneCenter => ZoneAt(_time);

    public float ZoneHalf => _half;

    private float ZoneAt(float time) => Math.Clamp(0.5f + 0.27f * MathF.Sin(0.75f * time + _phaseA) + 0.1f * MathF.Sin(1.7f * time + _phaseB), _half, 1f - _half);

    public override float Score => Finished ? _inside / _duration : _time <= 0.001f ? 0f : _inside / _time;

    public override void Advance(float dt)
    {
        if (Finished)
            return;

        _time += dt;

        // The marker is pushed up while the button is held and down otherwise, and the air slows it.
        _speed += (Held ? 4.0f : -3.0f) * dt;
        _speed *= Math.Max(0f, 1f - 2.2f * dt);
        _position += _speed * dt;
        if (_position <= 0f)
        {
            _position = 0f;
            _speed = Math.Max(0f, _speed);
        }
        else if (_position >= 1f)
        {
            _position = 1f;
            _speed = Math.Min(0f, _speed);
        }

        if (Math.Abs(_position - ZoneAt(_time)) <= _half)
            _inside += dt;

        if (_time >= _duration)
            Finished = true;
    }

    protected override void DrawGame(DrawingHandleScreen h)
    {
        const float x = 280f;

        // The bar.
        MiniDraw.Pill(h, new UIBox2(P(x - 31, BarTop - 4), P(x + 31, BarTop + BarHeight + 4)), CraftPalette.Border);
        MiniDraw.Pill(h, new UIBox2(P(x - 27, BarTop), P(x + 27, BarTop + BarHeight)), CraftPalette.Dark);

        // The zone.
        var zone = ZoneAt(_time);
        var zoneTop = BarTop + BarHeight * (1f - (zone + _half));
        var zoneBottom = BarTop + BarHeight * (1f - (zone - _half));
        var inside = Math.Abs(_position - zone) <= _half;
        h.DrawRect(new UIBox2(P(x - 24, zoneTop), P(x + 24, zoneBottom)), CraftPalette.Good.WithAlpha(inside ? 0.6f : 0.33f));
        h.DrawRect(new UIBox2(P(x - 24, zoneTop), P(x + 24, zoneBottom)), CraftPalette.Good.WithAlpha(0.85f), filled: false);

        // The marker.
        var y = BarTop + BarHeight * (1f - _position);
        var at = P(x, y);
        MiniDraw.Glow(h, at, S(24), inside ? CraftPalette.Good : CraftPalette.Gold);
        h.DrawCircle(at, S(11), inside ? CraftPalette.Good : CraftPalette.Gold);
        h.DrawCircle(at, S(6), CraftPalette.Text);
        MiniDraw.Line(h, P(x - 34, y), P(x - 20, y), S(3), CraftPalette.Text.WithAlpha(0.8f));
        MiniDraw.Line(h, P(x + 20, y), P(x + 34, y), S(3), CraftPalette.Text.WithAlpha(0.8f));

        // The time left on the left, the share kept in the zone so far on the right.
        var left = 1f - Math.Clamp(_time / _duration, 0f, 1f);
        MiniDraw.Pill(h, new UIBox2(P(150, BarTop), P(162, BarTop + BarHeight)), CraftPalette.Dark);
        if (left > 0.02f)
            MiniDraw.Pill(h, new UIBox2(P(150, BarTop + BarHeight * (1f - left)), P(162, BarTop + BarHeight)), CraftPalette.Gold.WithAlpha(0.85f));

        MiniDraw.Pill(h, new UIBox2(P(398, BarTop), P(410, BarTop + BarHeight)), CraftPalette.Dark);
        var kept = Math.Clamp(Score, 0f, 1f);
        if (kept > 0.02f)
            MiniDraw.Pill(h, new UIBox2(P(398, BarTop + BarHeight * (1f - kept)), P(410, BarTop + BarHeight)), (kept >= CraftMinigames.GoodFrom ? CraftPalette.Good : CraftPalette.Gold).WithAlpha(0.9f));

        // The mark of the best result on that bar.
        var mark = BarTop + BarHeight * (1f - CraftMinigames.GoodFrom);
        MiniDraw.Line(h, P(392, mark), P(416, mark), S(2), CraftPalette.Good.WithAlpha(0.8f));
    }
}

// A dot runs along a winding track, and the cursor has to go with it. The track is made from the number of the recipe, so that every recipe has a track of its own.
public sealed class TraceField : MinigameField
{
    private const float Left = 40f;
    private const float Width = 480f;
    private const int Pieces = 120;

    private readonly float _duration;
    private readonly float _radius;
    private readonly float _a1;
    private readonly float _a2;
    private readonly float _f1;
    private readonly float _f2;
    private readonly float _p1;
    private readonly float _p2;
    private readonly Vector2[] _points = new Vector2[Pieces + 1];

    private float _time;
    private float _sum;
    private Vector2 _cursor = new(Left, 100f);
    private bool _near;

    // Whether the mouse is looked at: a test that plays without a mouse turns it off and puts the cursor itself.
    public bool PollMouse = true;

    public TraceField(int seed, float duration, float ease)
    {
        _duration = Math.Max(1f, duration);
        _radius = 24f * ease;

        var random = new Random(seed);
        _a1 = 0.2f + 0.08f * random.NextSingle();
        _a2 = 0.05f + 0.05f * random.NextSingle();
        _f1 = 0.8f + 0.5f * random.NextSingle();
        _f2 = 1.7f + 0.9f * random.NextSingle();
        _p1 = random.NextSingle() * MathF.Tau;
        _p2 = random.NextSingle() * MathF.Tau;

        for (var i = 0; i <= Pieces; i++)
            _points[i] = PointAt(i / (float) Pieces);
    }

    public override string Hint => Loc.GetString("au14-craft-minigame-trace-hint");

    public float Radius => _radius;

    // Where the dot is on the track at this share (0 to 1) of the way.
    public Vector2 PointAt(float u)
    {
        var y = 0.5f + _a1 * MathF.Sin(MathF.Tau * _f1 * u + _p1) + _a2 * MathF.Sin(MathF.Tau * _f2 * u + _p2);
        return new Vector2(Left + Width * u, Math.Clamp(y, 0.14f, 0.86f) * Design.Y);
    }

    public Vector2 Dot => PointAt(Math.Clamp(_time / _duration, 0f, 1f));

    public void SetCursor(Vector2 designPosition) => _cursor = designPosition;

    public override float Score => Finished ? _sum / _duration : _time <= 0.001f ? 0f : _sum / _time;

    public override void Advance(float dt)
    {
        if (Finished)
            return;

        if (PollMouse)
            _cursor = MouseInDesign();

        _time += dt;

        // Full credit within the width of the track, less the farther the cursor is from it, none at all when it is lost.
        var distance = (_cursor - Dot).Length();
        var credit = Math.Clamp(1f - (distance - _radius) / (0.8f * _radius), 0f, 1f);
        _near = distance <= _radius;
        _sum += credit * dt;

        if (_time >= _duration)
            Finished = true;
    }

    protected override void DrawGame(DrawingHandleScreen h)
    {
        var screen = new Vector2[Pieces + 1];
        for (var i = 0; i <= Pieces; i++)
            screen[i] = P(_points[i].X, _points[i].Y);

        // The track: a wide band with an edge, and a thin line along its middle.
        MiniDraw.Stroke(h, screen, Pieces + 1, S(_radius * 2f + 6), CraftPalette.Border);
        MiniDraw.Stroke(h, screen, Pieces + 1, S(_radius * 2f), CraftPalette.Dark);
        MiniDraw.Stroke(h, screen, Pieces + 1, S(2), CraftPalette.Border.WithAlpha(0.6f));

        // The part the dot has run already is lit.
        var done = Math.Clamp((int) MathF.Round(Pieces * _time / _duration), 0, Pieces);
        if (done >= 1)
            MiniDraw.Stroke(h, screen, done + 1, S(5), CraftPalette.Gold.WithAlpha(0.9f));

        // The dot, with its glow.
        var dot = P(Dot.X, Dot.Y);
        MiniDraw.Glow(h, dot, S(_radius), CraftPalette.Gold);
        h.DrawCircle(dot, S(9), CraftPalette.Gold);
        h.DrawCircle(dot, S(4), CraftPalette.Text);

        // The cursor: a ring that is green with the dot and red when it has lost it.
        var cursor = P(_cursor.X, _cursor.Y);
        var color = _near || !Running ? CraftPalette.Good : CraftPalette.Bad;
        MiniDraw.Ring(h, cursor, S(11), S(3), color);
        h.DrawCircle(cursor, S(2.5f), color);

        // The time left, as a thin line along the bottom.
        var left = 1f - Math.Clamp(_time / _duration, 0f, 1f);
        h.DrawRect(new UIBox2(P(Left, 192), P(Left + Width, 196)), CraftPalette.Dark);
        h.DrawRect(new UIBox2(P(Left, 192), P(Left + Width * left, 196)), CraftPalette.Gold.WithAlpha(0.85f));
    }
}

// The window of a game: the same dark panel with the gold edge as the crafting window, the name of what is being made, how to play, the field and the word of the last move.
// A countdown, then the game, then a moment of the result, and the result goes to the server.
public sealed class CraftingMinigameWindow : BaseWindow
{
    private const float CountdownTime = 1.5f;
    private const float ResultTime = 1.4f;

    private readonly Label _status;
    private readonly Label _score;

    private float _phaseTime;
    private bool _reported;

    public enum PhaseKind : byte
    {
        Countdown,
        Playing,
        Result,
    }

    public MinigameField Field { get; }

    public CraftMinigameKind Kind { get; }

    public string RecipeId { get; }

    public PhaseKind Phase { get; private set; } = PhaseKind.Countdown;

    // A test that plays the game by hand drives the clock itself.
    public bool ManualClock;

    // The game is over and this is how it went.
    public event Action<float>? OnFinished;

    // The window was closed before the game was over.
    public event Action? OnAborted;

    public CraftingMinigameWindow(string recipeId, string title, CraftMinigameKind kind, int seed, int steps, float duration, float ease, int stage = 0, int stages = 1)
    {
        RecipeId = recipeId;
        Kind = kind;
        Resizable = false;
        MinSize = new Vector2(612, 0);
        MouseFilter = MouseFilterMode.Stop;

        Field = kind switch
        {
            CraftMinigameKind.Hold => new HoldField(seed, duration, ease),
            CraftMinigameKind.Trace => new TraceField(seed, duration, ease),
            CraftMinigameKind.Polish => new PolishField(seed, duration, ease),
            CraftMinigameKind.Center => new CenterField(seed, steps, ease),
            CraftMinigameKind.Drill => new DrillField(seed, duration, ease),
            CraftMinigameKind.Stitch => new StitchField(seed, steps, ease),
            _ => new RhythmField(seed, steps, ease),
        };

        _status = new Label { HorizontalExpand = true, HorizontalAlignment = HAlignment.Center, FontColorOverride = CraftPalette.Gold };
        _score = new Label { HorizontalAlignment = HAlignment.Right, FontColorOverride = CraftPalette.Muted, MinWidth = 70 };
        Field.OnFeedback += (word, color) =>
        {
            _status.Text = word;
            _status.FontColorOverride = color;
        };

        var heading = new Label { Text = title, HorizontalExpand = true, FontColorOverride = CraftPalette.Text };
        var kindName = Loc.GetString(KindName(kind));
        var game = new Label
        {
            // Several games of one recipe are numbered: which this is, and how many there are.
            Text = stages > 1 ? Loc.GetString("au14-craft-minigame-stage", ("name", kindName), ("stage", stage + 1), ("stages", stages)) : kindName,
            FontColorOverride = CraftPalette.Gold,
        };
        var close = new Button { Text = "✕", MinWidth = 28 };
        close.OnPressed += _ => Close();

        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        header.AddChild(heading);
        header.AddChild(game);
        header.AddChild(close);

        var hint = new Label { Text = Field.Hint, HorizontalAlignment = HAlignment.Center, FontColorOverride = CraftPalette.Muted };

        var footer = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        footer.AddChild(new Control { MinWidth = 70 });
        footer.AddChild(_status);
        footer.AddChild(_score);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6, Margin = new Thickness(8, 6, 8, 8) };
        root.AddChild(header);
        root.AddChild(new PanelContainer { PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#ffffff18") }, MinHeight = 1 });
        root.AddChild(hint);
        root.AddChild(Field);
        root.AddChild(footer);

        AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = CraftPalette.Panel,
                BorderColor = CraftPalette.Border,
                BorderThickness = new Thickness(1),
            },
            MouseFilter = MouseFilterMode.Pass,
            Children = { root },
        });

        SetCountdownWord();
    }

    private static string KindName(CraftMinigameKind kind) => kind switch
    {
        CraftMinigameKind.Hold => "au14-craft-minigame-hold",
        CraftMinigameKind.Trace => "au14-craft-minigame-trace",
        CraftMinigameKind.Polish => "au14-craft-minigame-polish",
        CraftMinigameKind.Center => "au14-craft-minigame-center",
        CraftMinigameKind.Drill => "au14-craft-minigame-drill",
        CraftMinigameKind.Stitch => "au14-craft-minigame-stitch",
        _ => "au14-craft-minigame-rhythm",
    };

    public bool Reported => _reported;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!ManualClock)
            Advance(args.DeltaSeconds);
    }

    // One step of time for the whole window: the countdown, the game, the result.
    public void Advance(float dt)
    {
        Field.Animate(dt);
        _phaseTime += dt;

        switch (Phase)
        {
            case PhaseKind.Countdown:
                SetCountdownWord();
                if (_phaseTime >= CountdownTime)
                {
                    Phase = PhaseKind.Playing;
                    _phaseTime = 0f;
                    Field.Running = true;
                    _status.Text = Loc.GetString("au14-craft-minigame-go");
                    _status.FontColorOverride = CraftPalette.Good;
                }

                break;

            case PhaseKind.Playing:
                Field.Advance(dt);
                _score.Text = $"{(int) Math.Round(Field.Score * 100)}%";
                if (Field.Finished)
                {
                    Phase = PhaseKind.Result;
                    _phaseTime = 0f;
                    ShowResult();
                }

                break;

            case PhaseKind.Result:
                if (_phaseTime >= ResultTime)
                    Report();

                break;
        }
    }

    private void SetCountdownWord()
    {
        var left = (int) Math.Ceiling(CountdownTime - _phaseTime);
        _status.Text = Loc.GetString("au14-craft-minigame-ready", ("seconds", Math.Max(1, left)));
        _status.FontColorOverride = CraftPalette.Muted;
    }

    private void ShowResult()
    {
        var score = Field.Score;
        var outcome = CraftMinigames.OutcomeOf(score);
        _score.Text = $"{(int) Math.Round(score * 100)}%";
        _status.Text = Loc.GetString(outcome switch
        {
            CraftOutcome.Failed => "au14-craft-minigame-result-failed",
            CraftOutcome.Good => "au14-craft-minigame-result-good",
            _ => "au14-craft-minigame-result-normal",
        });
        _status.FontColorOverride = outcome switch
        {
            CraftOutcome.Failed => CraftPalette.Bad,
            CraftOutcome.Good => CraftPalette.Good,
            _ => CraftPalette.Gold,
        };
    }

    // Sends the result and closes; once.
    private void Report()
    {
        if (_reported)
            return;

        _reported = true;
        OnFinished?.Invoke(Field.Score);
        Close();
    }

    // Closes without saying anything to the server: the window is replaced by another or the game is shut down, and nobody gave the craft up.
    public void CloseQuietly()
    {
        _reported = true;
        Close();
    }

    public override void Close()
    {
        if (!_reported)
        {
            _reported = true;
            OnAborted?.Invoke();
        }

        base.Close();
    }

    // The window is taken by its top strip only: the rest is the field, where the mouse plays.
    protected override DragMode GetDragModeFor(Vector2 relativeMousePos) => relativeMousePos.Y < 34 ? DragMode.Move : DragMode.None;

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            Close();
            args.Handle();
            return;
        }

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            Field.Held = true;
            Field.Press();
            args.Handle();
            return;
        }

        base.KeyBindDown(args);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            Field.Held = false;
            args.Handle();
            return;
        }

        base.KeyBindUp(args);
    }
}
