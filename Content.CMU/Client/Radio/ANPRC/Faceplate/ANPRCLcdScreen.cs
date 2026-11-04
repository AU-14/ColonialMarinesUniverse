using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     The set's display glass. Draws the recessed bezel, the phosphor ground and the pixel grid
///     underneath whatever the panel puts on it, with the CRT haze over the top. Anything added to
///     <see cref="Contents"/> is drawn on the glass.
/// </summary>
public sealed class ANPRCLcdScreen : Control
{
    private readonly ScanlineOverlay _scanlines;

    public readonly BoxContainer Contents;

    /// <summary>The set is powered. An unpowered screen is dead glass, not a dark theme.</summary>
    public bool Lit { get; set; }

    public ANPRCBacklight Backlight { get; set; } = ANPRCBacklight.Night;

    /// <summary>
    ///     Seconds the glass has been lit, which every moving part of the CRT haze is phased off.
    ///     Held here rather than read off the game clock so the tube stops dead with the set
    ///     instead of carrying on rolling behind dark glass.
    /// </summary>
    private float _time;

    /// <summary>
    ///     The glass is now the whole control surface - the pages live on it rather than on a deck
    ///     below - so it is sized like a display instead of a status strip.
    /// </summary>
    public const float DefaultHeight = 330f;

    public ANPRCLcdScreen()
    {
        Contents = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            Margin = new Thickness(9, 7),
            // the readouts measure themselves rather than being given fixed widths, so the glass
            // is what guarantees nothing is ever painted out over the bezel and onto the chassis
            RectClipContent = true,
        };

        AddChild(Contents);

        _scanlines = new ScanlineOverlay();
        AddChild(_scanlines);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _scanlines.Lit = Lit;
        _scanlines.Backlight = Backlight;

        if (!Lit)
            return;

        // wrapped well short of float precision loss, at a period long enough that none of the
        // cycles below visibly jump when it comes round
        _time = (_time + args.DeltaSeconds) % 3600f;
        _scanlines.Time = _time;
    }

    internal static UIBox2 Inset(UIBox2 box, float amount)
    {
        return new UIBox2(
            box.Left + amount,
            box.Top + amount,
            MathF.Max(box.Left + amount, box.Right - amount),
            MathF.Max(box.Top + amount, box.Bottom - amount));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var box = PixelSizeBox;

        if (box.Width <= 0 || box.Height <= 0)
            return;

        var palette = ANPRCPanelStyle.Palette(Backlight);
        var scale = UIScale;

        // bezel: the screen is sunk into the chassis, not painted onto it
        handle.DrawRect(box, ANPRCPanelStyle.BezelOuter);
        handle.DrawRect(Inset(box, 2f * scale), ANPRCPanelStyle.BezelInner);

        var glass = Inset(box, 4f * scale);

        // an unpowered set is just glass with the chassis behind it
        handle.DrawRect(glass, Lit ? palette.Back : Color.FromHex("#0B0E0A"));

        if (!Lit)
            return;

        // the dot matrix the characters sit on. cheap, and it is most of what stops the
        // readout looking like a text box
        var pitch = MathF.Max(2f, 3f * scale);

        for (var y = glass.Top + pitch * 0.66f; y < glass.Bottom; y += pitch)
        {
            handle.DrawRect(new UIBox2(glass.Left, y, glass.Right, y + scale), palette.Grid.WithAlpha(0.5f));
        }

        // a soft bloom down the top of the glass, the way a backlit panel lights unevenly
        var bloom = MathF.Min(glass.Height * 0.45f, 46f * scale);

        for (var i = 0; i < 6; i++)
        {
            var top = glass.Top + bloom * (i / 6f);
            var bottom = glass.Top + bloom * ((i + 1) / 6f);
            var alpha = 0.05f * (1f - i / 6f);

            handle.DrawRect(new UIBox2(glass.Left, top, glass.Right, bottom), palette.Bright.WithAlpha(alpha));
        }
    }

    /// <summary>
    ///     The CRT haze over the glass. A separate control because it has to draw after the
    ///     readouts rather than under them.
    ///
    ///     Deliberately almost still. The first pass drifted the scanline comb, rolled a retrace
    ///     bar, modulated the whole raster on a mains hum and slipped the vertical hold every few
    ///     seconds; together they read as a flickering screen rather than as a tube, and a
    ///     full-field brightness pulse at mains rate is worth avoiding on its own account. What
    ///     is left is a static comb, one very slow and very faint sheen, and a reflection that
    ///     wanders - enough that the glass is not a flat rectangle, little enough that nothing
    ///     competes with text an operator has to read under fire.
    ///
    ///     Set <see cref="Motion"/> to 0 to still it completely.
    /// </summary>
    private sealed class ScanlineOverlay : Control
    {
        /// <summary>
        ///     Scales everything that moves. 0 stops the glass dead, 1 is the calm default; there
        ///     is no reason to go much above that, and the parts that used to flicker are gone
        ///     rather than turned down.
        /// </summary>
        private const float Motion = 1f;

        // one unhurried pass of the sheen up the glass. long enough that it reads as a reflection
        // moving rather than as something on the display doing it
        private const float SheenSeconds = 14f;

        public bool Lit;
        public ANPRCBacklight Backlight = ANPRCBacklight.Night;
        public float Time;

        public ScanlineOverlay()
        {
            MouseFilter = MouseFilterMode.Ignore;
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize) => Vector2.Zero;

        protected override void Draw(DrawingHandleScreen handle)
        {
            var box = PixelSizeBox;

            if (box.Width <= 0 || box.Height <= 0)
                return;

            var scale = UIScale;
            var glass = Inset(box, 4f * scale);

            // the scanline comb runs at twice the dot-matrix pitch the glass draws underneath it.
            // At the same pitch the two combs beat against each other as the moving one drifts,
            // and the readout crawls with moire instead of sitting still to be read
            var pitch = MathF.Max(2f, 3f * scale) * 2f;

            // dead glass: comb still there, because a switched-off tube is still a tube, but
            // nothing on it moves
            if (!Lit)
            {
                for (var y = glass.Top; y < glass.Bottom; y += pitch)
                {
                    handle.DrawRect(
                        new UIBox2(glass.Left, y, glass.Right, y + scale),
                        Color.Black.WithAlpha(0.16f));
                }

                handle.DrawRect(glass, Color.Black.WithAlpha(0.45f));
                Glare(handle, glass, scale, 0f);

                return;
            }

            var palette = ANPRCPanelStyle.Palette(Backlight);

            // the comb, and it stays put. Drifting it made the readout crawl underneath, which is
            // the opposite of what a scanline is for
            for (var y = glass.Top; y < glass.Bottom; y += pitch)
            {
                handle.DrawRect(
                    new UIBox2(glass.Left, y, glass.Right, MathF.Min(glass.Bottom, y + scale)),
                    Color.Black.WithAlpha(0.16f));
            }

            // one broad, faint sheen easing up the glass. Wide and shallow on purpose: a narrow
            // bright band reads as a flash going past, a wide dim one reads as a reflection
            if (Motion > 0f)
            {
                var sheenHeight = MathF.Max(24f * scale, glass.Height * 0.55f);
                var travel = glass.Height + sheenHeight * 2f;
                var centre = glass.Bottom + sheenHeight - Time / (SheenSeconds / Motion) % 1f * travel;

                const int bands = 6;

                for (var i = 0; i < bands; i++)
                {
                    var from = centre - sheenHeight * 0.5f + sheenHeight * (i / (float) bands);
                    var to = centre - sheenHeight * 0.5f + sheenHeight * ((i + 1) / (float) bands);

                    var top = MathF.Max(glass.Top, from);
                    var bottom = MathF.Min(glass.Bottom, to);

                    if (bottom <= top)
                        continue;

                    // triangular falloff, so the sheen has no edge to catch the eye
                    var distance = MathF.Abs(i + 0.5f - bands * 0.5f) / (bands * 0.5f);
                    var alpha = (1f - distance) * 0.014f;

                    handle.DrawRect(new UIBox2(glass.Left, top, glass.Right, bottom), palette.Bright.WithAlpha(alpha));
                }
            }

            Glare(handle, glass, scale, Time * Motion);
        }

        /// <summary>
        ///     The reflection off the front of the glass. It wanders a little, because a set on a
        ///     man's back is never quite still under whatever is lighting it.
        /// </summary>
        private static void Glare(DrawingHandleScreen handle, UIBox2 glass, float scale, float time)
        {
            var wander = MathF.Sin(time * 0.16f) * glass.Height * 0.03f;
            var glareTop = glass.Top + glass.Height * 0.08f + wander;

            if (glareTop < glass.Top || glareTop + 2f * scale > glass.Bottom)
                return;

            handle.DrawRect(
                new UIBox2(glass.Left, glareTop, glass.Right, glareTop + 2f * scale),
                Color.White.WithAlpha(0.035f));
        }
    }
}
