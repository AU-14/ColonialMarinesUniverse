using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Requisitions;

public enum RequisitionsTrailKind
{
    Phosphor,
    MedicalPulse,
    AmmoTracer,
    EngineeringSparks,
}

public sealed class RequisitionsRollingLabel : Label
{
    private int _displayed;
    private int _target;
    private bool _initialized;
    private string _prefix = string.Empty;

    public void SetTarget(int value, string prefix)
    {
        _prefix = prefix;
        _target = value;
        if (!_initialized)
        {
            _displayed = value;
            _initialized = true;
        }
        UpdateText();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_displayed == _target)
            return;

        var difference = _target - _displayed;
        var step = Math.Max(1, (int) (Math.Abs(difference) * Math.Min(1f, args.DeltaSeconds * 12f)));
        _displayed += Math.Sign(difference) * Math.Min(Math.Abs(difference), step);
        UpdateText();
    }

    private void UpdateText()
    {
        Text = $"{_prefix} ${_displayed:N0}";
    }
}

public sealed class RequisitionsTurntablePreview : LayeredTextureRect
{
    private float _time;

    public RequisitionsTurntablePreview()
    {
        Stretch = TextureRect.StretchMode.KeepAspectCentered;
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += args.DeltaSeconds;
        var width = 30f + MathF.Abs(MathF.Cos(_time * 1.75f)) * 62f;
        var height = 82f + MathF.Sin(_time * 2.4f) * 4f;
        SetSize = new Vector2(width, height);
        LayoutContainer.SetPosition(this, new Vector2(60f - width / 2f, 5f + MathF.Sin(_time * 2.4f) * 2f));
    }
}

public sealed class RequisitionsScanLine : Control
{
    public Color Color = Color.White;

    public RequisitionsScanLine()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        handle.DrawRect(PixelSizeBox, Color);
    }
}

internal sealed class RequisitionsGhostRoute : Control
{
    public Vector2 Start;
    public Vector2 End;
    public Color Color = Color.White;
    private float _phase;

    public RequisitionsGhostRoute()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _phase = (_phase + args.DeltaSeconds * 24f) % 12f;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        for (var i = 0; i < 38; i++)
        {
            var t = (i * 12f + _phase) / (38f * 12f);
            if (t > 1f)
                continue;
            var point = Vector2.Lerp(Start, End, t);
            point.Y -= MathF.Sin(t * MathF.PI) * 54f;
            handle.DrawRect(new UIBox2(point.X, point.Y, point.X + 3, point.Y + 3), Color.WithAlpha(0.18f + t * 0.5f));
        }
    }
}

internal sealed class RequisitionsAfterimage : LayeredTextureRect
{
    private float _life;

    public RequisitionsAfterimage(List<Texture> textures, Vector2 position, Vector2 size, Color color)
    {
        Textures = textures;
        SetSize = size;
        Stretch = TextureRect.StretchMode.KeepAspectCentered;
        Modulate = color.WithAlpha(0.32f);
        MouseFilter = MouseFilterMode.Ignore;
        LayoutContainer.SetPosition(this, position);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _life += args.DeltaSeconds;
        Modulate = Modulate.WithAlpha(Math.Max(0, 0.32f * (1f - _life / 0.24f)));
        if (_life < 0.24f)
            return;
        UserInterfaceManager.DeferAction(Orphan);
    }
}

internal sealed class RequisitionsTrailParticle : Control
{
    private readonly RequisitionsTrailKind _kind;
    private readonly Color _color;
    private float _life;

    public RequisitionsTrailParticle(Vector2 position, RequisitionsTrailKind kind, Color color)
    {
        _kind = kind;
        _color = color;
        SetSize = new Vector2(28, 28);
        MouseFilter = MouseFilterMode.Ignore;
        LayoutContainer.SetPosition(this, position - new Vector2(14));
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _life += args.DeltaSeconds;
        if (_life >= 0.32f)
            UserInterfaceManager.DeferAction(Orphan);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var fade = Math.Max(0f, 1f - _life / 0.32f);
        var color = _color.WithAlpha(fade * 0.55f);
        switch (_kind)
        {
            case RequisitionsTrailKind.MedicalPulse:
                var extent = 4f + _life * 24f;
                handle.DrawRect(new UIBox2(14 - extent, 13, 14 + extent, 15), color);
                handle.DrawRect(new UIBox2(13, 14 - extent, 15, 14 + extent), color);
                break;
            case RequisitionsTrailKind.AmmoTracer:
                handle.DrawRect(new UIBox2(1, 12, 27, 15), color);
                break;
            case RequisitionsTrailKind.EngineeringSparks:
                handle.DrawRect(new UIBox2(4, 4 + _life * 20, 8, 8 + _life * 20), color);
                handle.DrawRect(new UIBox2(18, 8 + _life * 12, 21, 11 + _life * 12), color);
                handle.DrawRect(new UIBox2(11, 16 + _life * 8, 14, 19 + _life * 8), color);
                break;
        }
    }
}

internal sealed class RequisitionsConveyorCrate : PanelContainer
{
    private readonly Vector2 _start;
    private readonly Vector2 _end;
    private readonly float _delay;
    private float _elapsed;

    public RequisitionsConveyorCrate(Vector2 start, Vector2 end, int number, float delay)
    {
        _start = start;
        _end = end;
        _delay = delay;
        SetSize = new Vector2(180, 54);
        PanelOverride = RequisitionsTerminalTheme.Manifest.Panel(RequisitionsTerminalTheme.Manifest.SurfaceRaised, corners: true);
        MouseFilter = MouseFilterMode.Ignore;
        AddChild(new Label
        {
            Text = Loc.GetString("cmu-asrs-conveyor-crate", ("number", number.ToString("00"))),
            Margin = new Thickness(10, 15),
            FontColorOverride = RequisitionsTerminalTheme.Manifest.TextBright,
        });
        LayoutContainer.SetPosition(this, start);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _elapsed += args.DeltaSeconds;
        if (_elapsed < _delay)
        {
            Visible = false;
            return;
        }

        Visible = true;
        var progress = Math.Clamp((_elapsed - _delay) / 0.75f, 0f, 1f);
        var eased = progress * progress * (3f - 2f * progress);
        var position = Vector2.Lerp(_start, _end, eased);
        position.Y += MathF.Sin(progress * MathF.PI * 8f) * 2f;
        LayoutContainer.SetPosition(this, position);
        Modulate = Color.White.WithAlpha(1f - Math.Max(0f, progress - 0.72f) / 0.28f);
        if (progress >= 1f)
            UserInterfaceManager.DeferAction(Orphan);
    }
}

internal sealed class RequisitionsSealStrip : Control
{
    private readonly bool _sealed;
    private float _progress;

    public RequisitionsSealStrip(bool sealedCrate)
    {
        _sealed = sealedCrate;
        MinHeight = 22;
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _progress = Math.Min(1f, _progress + args.DeltaSeconds * (_sealed ? 2.8f : 7f));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var theme = RequisitionsTerminalTheme.Manifest;
        var lidWidth = Width * _progress;
        handle.DrawRect(new UIBox2(0, 0, lidWidth, 4), theme.Accent.WithAlpha(0.35f));
        if (!_sealed || _progress < 0.72f)
            return;

        var alpha = Math.Clamp((_progress - 0.72f) / 0.28f, 0f, 1f);
        var x = 4f;
        var index = 0;
        while (x < Width - 68)
        {
            var bar = index++ % 3 == 0 ? 4f : 2f;
            handle.DrawRect(new UIBox2(x, 8, x + bar, 19), theme.TextBright.WithAlpha(alpha * 0.72f));
            x += bar + 2f;
        }
        handle.DrawRect(new UIBox2(Math.Max(0, Width - 62), 8, Width, 19), theme.Accent.WithAlpha(alpha * 0.28f));
    }
}

internal sealed class RequisitionsSealStamp : Label
{
    private float _progress;

    public RequisitionsSealStamp(string code)
    {
        Text = Loc.GetString("cmu-asrs-seal-stamp", ("code", code));
        Align = AlignMode.Right;
        FontColorOverride = RequisitionsTerminalTheme.Manifest.Accent;
        Modulate = Color.White.WithAlpha(0);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _progress = Math.Min(1f, _progress + args.DeltaSeconds * 3f);
        Modulate = Color.White.WithAlpha(Math.Clamp((_progress - 0.45f) / 0.55f, 0f, 1f));
    }
}
