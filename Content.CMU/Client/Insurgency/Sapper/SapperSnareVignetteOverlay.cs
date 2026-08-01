using System;
using Content.Shared._AU14.Insurgency.Sapper;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._AU14.Insurgency.Sapper;

/// <summary>
///     A pulsing dark-green screen-edge vignette shown while the local player is caught in a snare. Same
///     edge-glow technique as the z-level cave-in warning (<c>StructuralWarningOverlay</c>), but dark green
///     and much heavier so it noticeably crowds the player's vision while they hang in the trap.
/// </summary>
public sealed class SapperSnareVignetteOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _player;
    private readonly IGameTiming _timing;

    public SapperSnareVignetteOverlay()
    {
        IoCManager.InjectDependencies(this);
        _entMan = IoCManager.Resolve<IEntityManager>();
        _player = IoCManager.Resolve<IPlayerManager>();
        _timing = IoCManager.Resolve<IGameTiming>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _player.LocalEntity is { } player && _entMan.HasComponent<SapperSnaredComponent>(player);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;

        float left = bounds.Left;
        float top = bounds.Top;
        float right = bounds.Right;
        float bottom = bounds.Bottom;
        var w = right - left;
        var h = bottom - top;

        // Heavier and deeper than the cave-in warning: stronger max alpha and a vignette that reaches much
        // further toward the centre, so the world view is genuinely harder to read while snared.
        var pulse = 0.5f + 0.5f * MathF.Sin((float) _timing.CurTime.TotalSeconds * 3f);
        var maxAlpha = 0.30f + 0.35f * pulse;

        const int layers = 24;
        var depth = MathF.Min(w, h) * 0.42f;
        var band = depth / layers;

        for (var i = 0; i < layers; i++)
        {
            var edgeT = 1f - i / (float) layers; // 1 at the edge, ~0 toward the centre
            var alpha = maxAlpha * edgeT * edgeT;
            var color = new Color(0.03f, 0.20f, 0.05f, alpha); // dark green
            var off = i * band;

            handle.DrawRect(new UIBox2(left, top + off, right, top + off + band), color);
            handle.DrawRect(new UIBox2(left, bottom - off - band, right, bottom - off), color);
            handle.DrawRect(new UIBox2(left + off, top, left + off + band, bottom), color);
            handle.DrawRect(new UIBox2(right - off - band, top, right - off, bottom), color);
        }
    }
}
