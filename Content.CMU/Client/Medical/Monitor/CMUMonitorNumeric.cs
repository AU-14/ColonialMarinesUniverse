using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Medical.Monitor;

/// <summary>
/// One of the monitor's numeric boxes: a small parameter label, a big value and a line of detail underneath.
/// </summary>
public sealed class CMUMonitorNumeric : Control
{
    private readonly Font _small = new VectorFont(IoCManager.Resolve<IResourceCache>()
        .GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 10);

    private readonly Font _big = new VectorFont(IoCManager.Resolve<IResourceCache>()
        .GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 26);

    public string Label = string.Empty;
    public string Value = "---";
    public string Unit = string.Empty;
    public string Detail = string.Empty;
    public Color Color = Color.Lime;

    /// <summary>Out of range: the value flashes red.</summary>
    public bool Alarm;

    private float _time;

    public CMUMonitorNumeric()
    {
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
        MinSize = new Vector2(120, 72);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += args.DeltaSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var width = (float) PixelSize.X;
        var height = (float) PixelSize.Y;
        var flashOn = _time % 1f < 0.5f;
        var color = Alarm && flashOn ? Color.FromHex("#FF3B30") : Color;

        if (Alarm && flashOn)
            handle.DrawRect(PixelSizeBox, Color.FromHex("#FF3B30").WithAlpha(0.12f));

        handle.DrawRect(PixelSizeBox, Color.WithAlpha(0.25f), false);

        var pad = 4f * UIScale;
        handle.DrawString(_small, new Vector2(pad, pad), Label, UIScale, color);
        if (Unit.Length > 0)
        {
            var unitSize = handle.GetDimensions(_small, Unit, UIScale);
            handle.DrawString(_small, new Vector2(width - unitSize.X - pad, pad), Unit, UIScale, color.WithAlpha(0.7f));
        }

        // The big value gets whatever space is left between the label and the detail line, so they never overlap.
        var top = pad + _small.GetLineHeight(UIScale);
        var bottom = height - pad * 0.5f;
        if (Detail.Length > 0)
        {
            var detailSize = handle.GetDimensions(_small, Detail, UIScale);
            bottom = height - detailSize.Y - pad * 0.5f;
            handle.DrawString(_small, new Vector2(pad, bottom), Detail, UIScale, color.WithAlpha(0.75f));
        }

        var bigScale = UIScale;
        var valueSize = handle.GetDimensions(_big, Value, bigScale);
        var fit = MathF.Min((width - pad * 2) / MathF.Max(valueSize.X, 1f), (bottom - top) / MathF.Max(valueSize.Y, 1f));
        if (fit < 1f)
        {
            bigScale *= MathF.Max(fit, 0.3f);
            valueSize = handle.GetDimensions(_big, Value, bigScale);
        }

        handle.DrawString(_big, new Vector2(width - valueSize.X - pad, top + (bottom - top - valueSize.Y) / 2f), Value,
            bigScale, color);
    }
}
