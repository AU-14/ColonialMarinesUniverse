using System;
using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     Scanlines drawn straight over whatever is beneath, with no render target involved.
/// </summary>
/// <remarks>
///     <para>
///     The cheap half of <see cref="CrtScreenControl"/>, for surfaces that want the raster but must
///     not be captured. Scanlines only darken pixels, so unlike the roll bar they need no copy of the
///     source to sample at an offset - and that is the whole point here. Capturing a live, scrolling,
///     constantly-rebuilt subtree such as the chat means its clipping and scroll offsets have to
///     survive a re-entrant render pass, and a stale or misaligned copy is drawn opaque over the real
///     thing, so it fails by showing the player old text rather than by throwing.
///     </para>
///     <para>
///     Nothing here animates. The shader drifts its phase with <c>TIME</c>; over a block of text
///     being read that is motion for its own sake, and the lines are indistinguishable standing
///     still.
///     </para>
/// </remarks>
public sealed class CrtScanlineOverlay : Control
{
    /// <summary>
    ///     Peak darkening at full intensity. Matches the coefficient the shader's scanline term uses,
    ///     so a surface wearing this and a surface wearing the full pass read as the same tube.
    /// </summary>
    private const float Darkening = 0.85f;

    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public CrtScanlineOverlay()
    {
        IoCManager.InjectDependencies(this);

        MouseFilter = MouseFilterMode.Ignore;
        CanKeyboardFocus = false;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        // Checked here every frame rather than trusted to callers, for the same reason
        // CrtScreenControl checks it: this is a CRT-theme effect and must never reach the base UI.
        if (!StyleNano.CrtUiEnabled)
            return;

        var intensity = _cfg.GetCVar(CCVars.CMUCrtEffectIntensity);
        if (intensity <= 0f)
            return;

        var size = PixelSize;
        if (size.X <= 0 || size.Y <= 0)
            return;

        // Below 2 the line and the gap stop resolving separately and the whole thing reads as a flat
        // darkening - the same floor the shader's `pitch` carries.
        var pitch = MathF.Max(_cfg.GetCVar(CCVars.CMUCrtEffectPitch), 2f);
        var color = Color.Black.WithAlpha(Math.Clamp(Darkening * intensity, 0f, 1f));

        for (var y = 0f; y < size.Y; y += pitch)
        {
            handle.DrawRect(
                new UIBox2(0, y, size.X, y + 1),
                color);
        }
    }
}
