using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Requisitions;

/// <summary>
/// Lightweight, root-level ASRS CRT pass. It deliberately avoids the old render-texture shader.
/// </summary>
public sealed class RequisitionsCrtOverlay : Control
{
    private float _sweep;
    private float _glitch;
    private float _activity;
    private Color _accent = Color.Green;
    private RequisitionsTerminalStyle _style;

    public RequisitionsCrtOverlay()
    {
        MouseFilter = MouseFilterMode.Ignore;
        CanKeyboardFocus = false;
    }

    public void SetProfile(RequisitionsTerminalStyle style, Color accent)
    {
        _style = style;
        _accent = accent;
    }

    public void TriggerActivity()
    {
        _activity = 0.28f;
    }

    public void TriggerGlitch()
    {
        _glitch = 0.24f;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _sweep = (_sweep + args.DeltaSeconds * 42f) % Math.Max(1f, Height);
        _glitch = Math.Max(0, _glitch - args.DeltaSeconds);
        _activity = Math.Max(0, _activity - args.DeltaSeconds);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (!StyleNano.CrtUiEnabled || Width <= 0 || Height <= 0)
            return;

        var rect = PixelSizeBox;
        DrawScanlines(handle, rect);
        switch (_style)
        {
            case RequisitionsTerminalStyle.WeylandAmber:
                DrawManifestGrid(handle, rect);
                break;
            case RequisitionsTerminalStyle.ColonialCyan:
                DrawTelemetry(handle, rect);
                break;
            case RequisitionsTerminalStyle.UppRedline:
                DrawTargeting(handle, rect);
                break;
            case RequisitionsTerminalStyle.FieldMono:
                DrawFieldSync(handle, rect);
                break;
            default:
                DrawLoadingGate(handle, rect);
                break;
        }

        const float edge = 9;
        var shade = Color.Black.WithAlpha(0.22f);
        handle.DrawRect(new UIBox2(rect.Left, rect.Top, rect.Right, edge), shade);
        handle.DrawRect(new UIBox2(rect.Left, rect.Bottom - edge, rect.Right, rect.Bottom), shade);
        handle.DrawRect(new UIBox2(rect.Left, rect.Top, edge, rect.Bottom), shade);
        handle.DrawRect(new UIBox2(rect.Right - edge, rect.Top, rect.Right, rect.Bottom), shade);

        if (_glitch <= 0)
        {
            if (_activity > 0)
                handle.DrawRect(rect, _accent.WithAlpha(_activity * 0.08f));
            return;
        }

        var band = (_glitch * 997f) % Math.Max(1f, rect.Bottom - 12);
        handle.DrawRect(new UIBox2(rect.Left, band, rect.Right, band + 6), _accent.WithAlpha(0.22f));
    }

    private void DrawScanlines(DrawingHandleScreen handle, UIBox2 rect)
    {
        var spacing = _style == RequisitionsTerminalStyle.FieldMono ? 8f : 6f;
        for (var y = 3f; y < rect.Bottom; y += spacing)
            handle.DrawRect(new UIBox2(rect.Left, y, rect.Right, y + 1), Color.Black.WithAlpha(0.065f));
    }

    private void DrawLoadingGate(DrawingHandleScreen handle, UIBox2 rect)
    {
        var x = (_sweep * 1.7f) % Math.Max(1f, rect.Right);
        handle.DrawRect(new UIBox2(x, rect.Top, Math.Min(rect.Right, x + 2), rect.Bottom), _accent.WithAlpha(0.045f));
        for (var tooth = 14f; tooth < rect.Right; tooth += 28f)
            handle.DrawRect(new UIBox2(tooth, rect.Bottom - 8, tooth + 10, rect.Bottom - 5), _accent.WithAlpha(0.16f));
    }

    private void DrawManifestGrid(DrawingHandleScreen handle, UIBox2 rect)
    {
        for (var x = 72f; x < rect.Right; x += 96f)
            handle.DrawRect(new UIBox2(x, rect.Top, x + 1, rect.Bottom), _accent.WithAlpha(0.025f));
        for (var y = 64f; y < rect.Bottom; y += 64f)
            handle.DrawRect(new UIBox2(rect.Left, y, rect.Right, y + 1), _accent.WithAlpha(0.025f));
        for (var y = 18f; y < rect.Bottom; y += 24f)
        {
            handle.DrawRect(new UIBox2(rect.Left + 4, y, rect.Left + 7, y + 3), _accent.WithAlpha(0.18f));
            handle.DrawRect(new UIBox2(rect.Right - 7, y, rect.Right - 4, y + 3), _accent.WithAlpha(0.18f));
        }
        handle.DrawRect(new UIBox2(rect.Left, _sweep, rect.Right, Math.Min(rect.Bottom, _sweep + 18)), _accent.WithAlpha(0.025f));
    }

    private void DrawTelemetry(DrawingHandleScreen handle, UIBox2 rect)
    {
        var offset = (_sweep * 2f) % 36f;
        for (var x = -offset; x < rect.Right; x += 36f)
        {
            handle.DrawRect(new UIBox2(x, rect.Top + 5, x + 18, rect.Top + 7), _accent.WithAlpha(0.18f));
            handle.DrawRect(new UIBox2(rect.Right - x - 18, rect.Bottom - 7, rect.Right - x, rect.Bottom - 5), _accent.WithAlpha(0.12f));
        }
        handle.DrawRect(new UIBox2(rect.Left, _sweep, rect.Right, Math.Min(rect.Bottom, _sweep + 1)), _accent.WithAlpha(0.09f));
    }

    private void DrawTargeting(DrawingHandleScreen handle, UIBox2 rect)
    {
        var centerX = rect.Width * 0.5f;
        var centerY = rect.Height * 0.5f;
        var pulse = 14f + MathF.Sin(_sweep * 0.04f) * 4f;
        handle.DrawRect(new UIBox2(centerX - pulse, centerY, centerX - 4, centerY + 1), _accent.WithAlpha(0.1f));
        handle.DrawRect(new UIBox2(centerX + 4, centerY, centerX + pulse, centerY + 1), _accent.WithAlpha(0.1f));
        handle.DrawRect(new UIBox2(centerX, centerY - pulse, centerX + 1, centerY - 4), _accent.WithAlpha(0.1f));
        handle.DrawRect(new UIBox2(centerX, centerY + 4, centerX + 1, centerY + pulse), _accent.WithAlpha(0.1f));
        handle.DrawRect(new UIBox2(rect.Left, _sweep, rect.Right, Math.Min(rect.Bottom, _sweep + 2)), _accent.WithAlpha(0.075f));
    }

    private void DrawFieldSync(DrawingHandleScreen handle, UIBox2 rect)
    {
        var y = (_sweep * 1.35f) % Math.Max(1f, rect.Bottom);
        handle.DrawRect(new UIBox2(rect.Left, y, rect.Right, Math.Min(rect.Bottom, y + 2)), Color.White.WithAlpha(0.045f));
        handle.DrawRect(new UIBox2(rect.Left, Math.Min(rect.Bottom, y + 4), rect.Right, Math.Min(rect.Bottom, y + 5)), Color.Black.WithAlpha(0.12f));
    }
}
