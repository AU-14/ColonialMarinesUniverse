using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Requisitions;

/// <summary>
/// Lightweight CRT pass. Neutral scanlines cover the terminal; green phosphor is clipped to the manifest.
/// </summary>
public sealed class RequisitionsCrtOverlay : Control
{
    private float _sweep;
    private float _glitch;
    private float _activity;
    private Color _accent = Color.Green;
    private Control? _manifest;

    public RequisitionsCrtOverlay()
    {
        MouseFilter = MouseFilterMode.Ignore;
        CanKeyboardFocus = false;
    }

    public void SetManifestTarget(Control manifest, Color accent)
    {
        _manifest = manifest;
        _accent = accent;
    }

    public void TriggerActivity() => _activity = 0.28f;
    public void TriggerGlitch() => _glitch = 0.24f;

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

        var screen = PixelSizeBox;
        for (var y = 3f; y < screen.Bottom; y += 6f)
            handle.DrawRect(new UIBox2(screen.Left, y, screen.Right, y + 1), Color.Black.WithAlpha(0.065f));

        const float edge = 9;
        var shade = Color.Black.WithAlpha(0.22f);
        handle.DrawRect(new UIBox2(screen.Left, screen.Top, screen.Right, edge), shade);
        handle.DrawRect(new UIBox2(screen.Left, screen.Bottom - edge, screen.Right, screen.Bottom), shade);
        handle.DrawRect(new UIBox2(screen.Left, screen.Top, edge, screen.Bottom), shade);
        handle.DrawRect(new UIBox2(screen.Right - edge, screen.Top, screen.Right, screen.Bottom), shade);

        if (_manifest is not { IsInsideTree: true })
            return;

        // DrawingHandleScreen works in physical pixels. GlobalPosition is UI-scaled and shifts the
        // manifest mask at non-1x UI scales, which makes the green pass spill into the catalog.
        var origin = _manifest.GlobalPixelPosition - GlobalPixelPosition;
        var manifest = new UIBox2(
            origin.X,
            origin.Y,
            origin.X + _manifest.PixelWidth,
            origin.Y + _manifest.PixelHeight);
        for (var x = manifest.Left + 72; x < manifest.Right; x += 96)
            handle.DrawRect(new UIBox2(x, manifest.Top, x + 1, manifest.Bottom), _accent.WithAlpha(0.025f));
        for (var y = manifest.Top + 64; y < manifest.Bottom; y += 64)
            handle.DrawRect(new UIBox2(manifest.Left, y, manifest.Right, y + 1), _accent.WithAlpha(0.025f));

        var sweep = manifest.Top + _sweep % Math.Max(1, manifest.Height);
        handle.DrawRect(new UIBox2(manifest.Left, sweep, manifest.Right, Math.Min(manifest.Bottom, sweep + 16)), _accent.WithAlpha(0.025f));
        if (_activity > 0)
            handle.DrawRect(manifest, _accent.WithAlpha(_activity * 0.08f));
        if (_glitch > 0)
        {
            var band = manifest.Top + (_glitch * 997f) % Math.Max(1f, manifest.Height - 12);
            handle.DrawRect(new UIBox2(manifest.Left, band, manifest.Right, band + 6), _accent.WithAlpha(0.22f));
        }
    }
}
