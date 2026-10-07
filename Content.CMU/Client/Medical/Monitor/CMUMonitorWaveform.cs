using System.Numerics;
using Content.Shared.CMU14.Medical.Monitor;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Medical.Monitor;

public enum CMUMonitorWaveKind : byte
{
    Ecg,
    Pleth,
    Capnography,
}

/// <summary>
/// A sweeping trace like a real monitor's: the line is drawn left to right over the last few seconds with a
/// small erase gap in front of the sweep. The ECG and pleth draw a complex at each recorded beat, the same beats
/// that play the QRS beep; the other shapes are computed from the rhythm and rates.
/// </summary>
public sealed class CMUMonitorWaveform : Control
{
    private const float SweepSeconds = 4f;
    private const float GapPixels = 10f;

    private readonly Font _font = new VectorFont(IoCManager.Resolve<IResourceCache>()
        .GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 10);

    public CMUMonitorWaveKind Kind;
    public Color TraceColor = Color.Lime;
    public string Label = string.Empty;
    public string? Message;
    public CMUMonitorRhythm Rhythm = CMUMonitorRhythm.None;
    public int HeartRate;
    public int RespiratoryRate;

    /// <summary>0-1 scale for the trace height, e.g. a weak pleth on a poorly perfused patient.</summary>
    public float Amplitude = 1f;

    /// <summary>
    /// Recent beats in real time, shared with the monitor's beep.
    /// </summary>
    public IReadOnlyList<TimeSpan>? Beats;

    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly TimeSpan _origin;

    public CMUMonitorWaveform()
    {
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
        _origin = _timing.RealTime;
    }

    private float Seconds(TimeSpan time)
    {
        return (float) (time - _origin).TotalSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = PixelSize;
        var width = (float) size.X;
        var height = (float) size.Y;
        if (width < 4 || height < 4)
            return;

        // Faint grid, like the screen's graticule.
        var grid = TraceColor.WithAlpha(0.07f);
        for (var x = 0f; x < width; x += width / 8f)
            handle.DrawLine(new Vector2(x, 0), new Vector2(x, height), grid);
        handle.DrawLine(new Vector2(0, height / 2f), new Vector2(width, height / 2f), grid);

        handle.DrawString(_font, new Vector2(4, 2), Label, UIScale, TraceColor.WithAlpha(0.8f));

        if (Message != null)
        {
            var dims = handle.GetDimensions(_font, Message, UIScale * 1.4f);
            handle.DrawString(_font, new Vector2((width - dims.X) / 2f, (height - dims.Y) / 2f), Message,
                UIScale * 1.4f, TraceColor);
            return;
        }

        var time = Seconds(_timing.RealTime);
        var sweep = time % SweepSeconds / SweepSeconds * width;
        var mid = height * (Kind == CMUMonitorWaveKind.Ecg ? 0.6f : 0.75f);
        var scale = height * (Kind == CMUMonitorWaveKind.Ecg ? 0.5f : 0.6f) * Amplitude;

        Vector2? previous = null;
        const float step = 2f;
        for (var x = 0f; x <= width; x += step)
        {
            // How long ago the sweep passed this column; columns just ahead of it are from the previous pass
            // and the few right in front of it are blanked.
            var behind = sweep - x;
            if (behind < 0)
                behind += width;

            if (behind > width - GapPixels)
            {
                previous = null;
                continue;
            }

            var t = time - behind / width * SweepSeconds;
            var point = new Vector2(x, mid - Sample(t) * scale);

            if (previous is { } prev)
                handle.DrawLine(prev, point, TraceColor);

            previous = point;
        }
    }

    private float Sample(float t)
    {
        return Kind switch
        {
            CMUMonitorWaveKind.Ecg => Ecg(t),
            CMUMonitorWaveKind.Pleth => Pleth(t),
            _ => Capnography(t),
        };
    }

    private float Ecg(float t)
    {
        switch (Rhythm)
        {
            case CMUMonitorRhythm.VentricularFibrillation:
                var envelope = 0.6f + 0.4f * MathF.Sin(t * 1.1f);
                return 0.45f * envelope * MathF.Sin(MathF.Tau * 4.6f * t + 1.3f * MathF.Sin(0.7f * t)) +
                       0.15f * MathF.Sin(MathF.Tau * 7.3f * t + 0.5f);
            case CMUMonitorRhythm.Asystole:
                return 0.02f * MathF.Sin(t * 1.3f);
            case CMUMonitorRhythm.None:
                return 0f;
        }

        var compress = Compress();
        var hasP = Rhythm != CMUMonitorRhythm.Irregular;

        // An irregular rhythm has no P waves and a fibrillating baseline.
        var value = hasP ? 0f : 0.03f * MathF.Sin(t * 41f) + 0.02f * MathF.Sin(t * 67f + 1f);

        if (Beats == null)
            return value;

        foreach (var beat in Beats)
        {
            var rel = t - Seconds(beat);
            if (rel < -0.3f || rel > 0.6f)
                continue;

            if (hasP)
                value += 0.12f * Gauss(rel, -0.12f * compress, 0.025f * compress);

            value -= 0.12f * Gauss(rel, -0.025f * compress, 0.008f * compress);
            value += 1.0f * Gauss(rel, 0f, 0.01f * compress);
            value -= 0.25f * Gauss(rel, 0.025f * compress, 0.01f * compress);
            value += 0.28f * Gauss(rel, 0.22f * compress, 0.045f * compress);
        }

        return value;
    }

    private float Pleth(float t)
    {
        if (Beats == null)
            return 0f;

        // The pulse reaches the finger a moment after each beat.
        var compress = Compress();
        var value = 0f;
        foreach (var beat in Beats)
        {
            var rel = t - Seconds(beat);
            if (rel < 0f || rel > 1f)
                continue;

            value += 0.9f * Gauss(rel, 0.2f * compress, 0.08f * compress);
            value += 0.3f * Gauss(rel, 0.42f * compress, 0.06f * compress);
        }

        return value;
    }

    /// <summary>
    /// Complexes narrow a little as the heart speeds up.
    /// </summary>
    private float Compress()
    {
        var period = 60f / Math.Max(HeartRate, 20);
        return MathF.Min(1f, period / 0.8f);
    }

    private float Capnography(float t)
    {
        if (RespiratoryRate <= 0)
            return 0f;

        var period = 60f / RespiratoryRate;
        var u = t % period / period;

        // Exhalation: a quick upstroke, a gently rising plateau, then a sharp drop on inhalation.
        if (u < 0.1f)
            return 0f;
        if (u < 0.16f)
            return (u - 0.1f) / 0.06f * 0.8f;
        if (u < 0.55f)
            return 0.8f + (u - 0.16f) * 0.4f;
        if (u < 0.6f)
            return (0.96f) * (1f - (u - 0.55f) / 0.05f);
        return 0f;
    }

    private static float Gauss(float x, float mu, float sigma)
    {
        var d = (x - mu) / sigma;
        return MathF.Exp(-0.5f * d * d);
    }
}
