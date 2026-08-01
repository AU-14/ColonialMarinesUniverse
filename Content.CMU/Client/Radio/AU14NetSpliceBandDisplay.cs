using System.Numerics;
using Content.Shared._AU14.Radio;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._AU14.Radio;

public sealed class AU14NetSpliceBandDisplay : Control
{
    private static readonly Color Background = Color.FromHex("#07130A");
    private static readonly Color Grid = Color.FromHex("#123021");
    private static readonly Color GridMajor = Color.FromHex("#1B4630");
    private static readonly Color NoiseFloor = Color.FromHex("#1A4A2C");
    private static readonly Color Sweep = Color.FromHex("#2F8F55");
    private static readonly Color DeadMark = Color.FromHex("#2A5C3A");
    private static readonly Color WeakSignal = Color.FromHex("#3E9E5E");
    private static readonly Color StrongSignal = Color.FromHex("#8FE86B");
    private static readonly Color LockLine = Color.FromHex("#57D67A");
    private static readonly Color Cursor = Color.FromHex("#D8F2C4");
    private static readonly Color Scanline = Color.FromHex("#000000");

    // one noise sample per band position, re-rolled a few times a second
    private const int NoiseSamples = 128;
    private const float NoiseRerollInterval = 0.08f;
    private const float SweepSeconds = 2.4f;

    [Dependency] private IRobustRandom _random = default!;

    private readonly float[] _noise = new float[NoiseSamples];

    private float _noiseTimer;
    private float _sweepTimer;
    private float _pulse;

    private int _bandSize = 100;
    private int _cursor = 1;
    private List<AU14NetSpliceReading> _readings = new();
    private List<int> _locked = new();

    public AU14NetSpliceBandDisplay()
    {
        IoCManager.InjectDependencies(this);
        MinSize = new Vector2(0, 150);
        HorizontalExpand = true;
        RollNoise();
    }

    public void SetBand(int bandSize, List<AU14NetSpliceReading> readings, List<int> locked)
    {
        _bandSize = Math.Max(1, bandSize);
        _readings = readings;
        _locked = locked;
    }

    public void SetCursor(int position)
    {
        _cursor = position;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _sweepTimer = (_sweepTimer + args.DeltaSeconds) % SweepSeconds;
        _pulse += args.DeltaSeconds;
        _noiseTimer += args.DeltaSeconds;

        if (_noiseTimer < NoiseRerollInterval)
            return;

        _noiseTimer = 0f;
        RollNoise();
    }

    private void RollNoise()
    {
        for (var i = 0; i < NoiseSamples; i++)
            _noise[i] = _random.NextFloat();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var width = PixelWidth;
        var height = PixelHeight;

        if (width <= 0 || height <= 0)
            return;

        handle.DrawRect(PixelSizeBox, Background);

        var floorY = height - 12f;
        var barMax = floorY - 10f;

        DrawGrid(handle, width, floorY);
        DrawNoiseFloor(handle, width, floorY);
        DrawSweep(handle, width, floorY);
        DrawLocks(handle, width, floorY);
        DrawReadings(handle, width, floorY, barMax);
        DrawCursor(handle, width, height, floorY);
        DrawScanlines(handle, width, height);
    }

    private void DrawGrid(DrawingHandleScreen handle, float width, float floorY)
    {
        for (var tick = 10; tick < _bandSize; tick += 10)
        {
            var x = ToPixel(tick, width);
            handle.DrawRect(new UIBox2(x, 0f, x + 1f, floorY), tick % 50 == 0 ? GridMajor : Grid);
        }

        handle.DrawRect(new UIBox2(0f, floorY, width, floorY + 1f), GridMajor);
    }

    /// <summary>Receiver hash along the baseline. Purely cosmetic - it never encodes a carrier.</summary>
    private void DrawNoiseFloor(DrawingHandleScreen handle, float width, float floorY)
    {
        var step = MathF.Max(2f, width / NoiseSamples);

        for (var i = 0; i < NoiseSamples; i++)
        {
            var x = i * step;

            if (x > width)
                break;

            var spike = 2f + _noise[i] * 5f;
            handle.DrawRect(new UIBox2(x, floorY - spike, x + step - 1f, floorY), NoiseFloor);
        }
    }

    /// <summary>A receiver sweep running the band on a loop, with a short trail behind it.</summary>
    private void DrawSweep(DrawingHandleScreen handle, float width, float floorY)
    {
        var progress = _sweepTimer / SweepSeconds;
        var sweepX = progress * width;
        var trail = width * 0.12f;

        for (var i = 0; i < 6; i++)
        {
            var offset = trail * (i / 6f);
            var x = sweepX - offset;

            if (x < 0f)
                continue;

            var fade = (1f - i / 6f) * 0.4f;
            handle.DrawRect(new UIBox2(x - 1f, 0f, x, floorY), Sweep.WithAlpha(fade));
        }

        if (sweepX <= width)
            handle.DrawRect(new UIBox2(sweepX - 1f, 0f, sweepX + 1f, floorY), Sweep.WithAlpha(0.65f));
    }

    private void DrawLocks(DrawingHandleScreen handle, float width, float floorY)
    {
        // locked carriers breathe slightly, so a finished stage still reads as live hardware
        var glow = 0.55f + 0.25f * MathF.Sin(_pulse * 2.2f);

        foreach (var locked in _locked)
        {
            var x = ToPixel(locked, width);

            handle.DrawRect(new UIBox2(x - 3f, 0f, x + 4f, floorY), LockLine.WithAlpha(glow * 0.22f));
            handle.DrawRect(new UIBox2(x - 1f, 0f, x + 2f, floorY), LockLine.WithAlpha(glow));
            handle.DrawRect(new UIBox2(x - 4f, floorY - 3f, x + 5f, floorY), LockLine);
        }
    }

    private void DrawReadings(DrawingHandleScreen handle, float width, float floorY, float barMax)
    {
        foreach (var reading in _readings)
        {
            var x = ToPixel(reading.Position, width);

            if (reading.Strength <= 0)
            {
                // a dead probe still rules that stretch of band out, so it stays on the scope
                handle.DrawRect(new UIBox2(x - 1f, floorY - 5f, x + 2f, floorY), DeadMark);
                continue;
            }

            var fraction = Math.Clamp(reading.Strength / 100f, 0f, 1f);
            var top = floorY - barMax * fraction;
            var color = Color.InterpolateBetween(WeakSignal, StrongSignal, fraction);

            handle.DrawRect(new UIBox2(x - 3f, top + 2f, x + 4f, floorY), color.WithAlpha(0.18f));
            handle.DrawRect(new UIBox2(x - 1f, top, x + 2f, floorY), color);
            handle.DrawRect(new UIBox2(x - 3f, top, x + 4f, top + 2f), StrongSignal);
        }
    }

    private void DrawCursor(DrawingHandleScreen handle, float width, float height, float floorY)
    {
        var x = ToPixel(_cursor, width);

        handle.DrawRect(new UIBox2(x - 1f, 0f, x + 1f, height), Cursor.WithAlpha(0.85f));

        // caret at the top so the tuned position is readable at a glance while the sweep is moving
        for (var i = 0; i < 5; i++)
            handle.DrawRect(new UIBox2(x - (5f - i), i, x + (6f - i), i + 1f), Cursor);

        handle.DrawRect(new UIBox2(x - 4f, floorY - 2f, x + 5f, floorY + 1f), Cursor);
    }

    /// <summary>Phosphor scanlines over the top of everything, purely for the CRT look.</summary>
    private void DrawScanlines(DrawingHandleScreen handle, float width, float height)
    {
        for (var y = 0f; y < height; y += 3f)
            handle.DrawRect(new UIBox2(0f, y, width, y + 1f), Scanline.WithAlpha(0.18f));
    }

    private float ToPixel(int position, float width)
    {
        var fraction = (position - 1f) / Math.Max(1f, _bandSize - 1f);

        return MathF.Round(fraction * (width - 10f)) + 5f;
    }
}
