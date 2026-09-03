using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Requisitions;

/// <summary>
/// A short-lived item icon that arcs from the catalog into the shipment manifest.
/// </summary>
internal sealed class RequisitionsItemFlyout : LayeredTextureRect
{
    private const float ArcHeight = 44f;
    private const float Duration = 0.42f;
    private const float FadeStart = 0.72f;

    private readonly Vector2 _end;
    private readonly Vector2 _start;
    private readonly RequisitionsTrailKind _trailKind;
    private readonly Color _trailColor;
    private readonly Vector2 _size;
    private float _elapsed;
    private float _trailClock;
    private bool _finished;

    public event Action? Landed;

    public RequisitionsItemFlyout(
        List<Texture> textures,
        Vector2 start,
        Vector2 end,
        Vector2 size,
        RequisitionsTrailKind trailKind = RequisitionsTrailKind.Phosphor,
        Color? trailColor = null)
    {
        _start = start;
        _end = end;
        _size = size;
        _trailKind = trailKind;
        _trailColor = trailColor ?? RequisitionsTerminalTheme.Manifest.Accent;

        Textures = textures;
        SetSize = size;
        Stretch = TextureRect.StretchMode.KeepAspectCentered;
        MouseFilter = MouseFilterMode.Ignore;
        LayoutContainer.SetPosition(this, start);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_finished)
            return;

        _elapsed += args.DeltaSeconds;
        var progress = Math.Clamp(_elapsed / Duration, 0f, 1f);
        var eased = 1f - MathF.Pow(1f - progress, 3f);
        var position = Vector2.Lerp(_start, _end, eased);
        position.Y -= MathF.Sin(progress * MathF.PI) * ArcHeight;
        LayoutContainer.SetPosition(this, position);

        _trailClock += args.DeltaSeconds;
        if (_trailClock >= 0.09f && Parent is { } parent)
        {
            _trailClock = 0;
            var textures = Textures.ToList();
            var afterimage = new RequisitionsAfterimage(textures, position, _size, _trailColor);
            var particle = new RequisitionsTrailParticle(position + _size / 2f, _trailKind, _trailColor);
            UserInterfaceManager.DeferAction(() =>
            {
                if (!parent.Disposed)
                {
                    parent.AddChild(afterimage);
                    afterimage.SetPositionInParent(Math.Max(0, GetPositionInParent()));
                    if (_trailKind != RequisitionsTrailKind.Phosphor)
                        parent.AddChild(particle);
                }
            });
        }

        if (progress >= FadeStart)
        {
            var alpha = 1f - (progress - FadeStart) / (1f - FadeStart);
            Modulate = Color.White.WithAlpha(alpha);
        }

        if (progress < 1f)
            return;

        _finished = true;
        Visible = false;
        Landed?.Invoke();
        UserInterfaceManager.DeferAction(Orphan);
    }
}
