// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._AU14.ZLevelBuilding;

/// <summary>
/// Collapse feedback vignette (same screen-edge technique as <c>SapperSnareVignetteOverlay</c>):
/// - Nearby collapse: one dark-grey vignette that blinks in and back out over about a second.
/// - Cave collapsing ON you: a near-black vignette that covers almost the whole screen and flutters
///   rapidly, like blinking your eyes against falling dust, for as long as the server keeps extending
///   <see cref="BlackUntil"/> (it re-sends while the collapse is still burying your tile).
/// </summary>
public sealed class ZCollapseVignetteOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    /// <summary>When the current grey blink started; blink length is fixed at ~1s. Zero = never.</summary>
    public TimeSpan GreyStart = TimeSpan.Zero;

    /// <summary>The black engulfed effect runs until this time (extended by repeated server events).</summary>
    public TimeSpan BlackUntil = TimeSpan.Zero;

    // 🔧 TUNABLE: grey blink length / strength, black flutter speed / strength.
    private const float GreyDuration = 1.0f;
    private const float GreyMaxAlpha = 0.38f;
    private const float BlackFlutterHz = 5f;

    private readonly IGameTiming _timing;

    public ZCollapseVignetteOverlay()
    {
        _timing = IoCManager.Resolve<IGameTiming>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        // GreyStart of Zero means "never triggered" - subtracting from it overflowed TimeSpan.
        var now = _timing.CurTime;
        return now < BlackUntil ||
               (GreyStart > TimeSpan.Zero && now >= GreyStart && (now - GreyStart).TotalSeconds < GreyDuration);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;
        var now = _timing.CurTime;

        if (now < BlackUntil)
        {
            // Rapid flutter between mostly-open and nearly-shut, reaching all the way to the screen centre.
            var t = (float) now.TotalSeconds;
            var blink = 0.5f + 0.5f * MathF.Sin(t * MathF.Tau * BlackFlutterHz);
            // Pure black, capped alpha: near-1 compounded layer alpha over a bright scene read as grey haze.
            var maxAlpha = 0.4f + 0.45f * blink;
            DrawVignette(handle, bounds, Color.Black, maxAlpha, depthFraction: 0.5f, centerFloor: 0.35f);
            return;
        }

        // Grey: single smooth blink in-and-out over GreyDuration.
        var progress = (float) ((now - GreyStart).TotalSeconds / GreyDuration);
        var envelope = MathF.Sin(MathF.PI * Math.Clamp(progress, 0f, 1f));
        DrawVignette(handle, bounds, new Color(0.13f, 0.13f, 0.14f), GreyMaxAlpha * envelope, depthFraction: 0.35f, centerFloor: 0f);
    }

    /// <summary>Layered screen-edge vignette. depthFraction: how far toward the centre it reaches (0.5 = full).
    /// centerFloor: minimum alpha fraction kept at the innermost layer (0 = fades to nothing).</summary>
    private static void DrawVignette(DrawingHandleScreen handle, UIBox2i bounds, Color baseColor, float maxAlpha, float depthFraction, float centerFloor)
    {
        if (maxAlpha <= 0f)
            return;

        // Draw in the viewport's LOCAL space (origin 0,0): the screen handle is already viewport-relative,
        // so using bounds.Left/Top double-applied the viewport's screen offset (visible as a rightward shift
        // when the Separated-HUD centering margin moves the viewport).
        const float left = 0;
        const float top = 0;
        float right = bounds.Width;
        float bottom = bounds.Height;
        var w = right - left;
        var h = bottom - top;

        const int layers = 24;
        var depth = MathF.Min(w, h) * depthFraction;
        var band = depth / layers;

        for (var i = 0; i < layers; i++)
        {
            var edgeT = 1f - i / (float) layers; // 1 at the edge, ~0 toward the centre
            var falloff = centerFloor + (1f - centerFloor) * edgeT * edgeT;
            var color = baseColor.WithAlpha(maxAlpha * falloff);
            var off = i * band;

            handle.DrawRect(new UIBox2(left, top + off, right, top + off + band), color);
            handle.DrawRect(new UIBox2(left, bottom - off - band, right, bottom - off), color);
            handle.DrawRect(new UIBox2(left + off, top, left + off + band, bottom), color);
            handle.DrawRect(new UIBox2(right - off - band, top, right - off, bottom), color);
        }
    }
}
