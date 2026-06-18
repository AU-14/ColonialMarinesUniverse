using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._CMU14.Medical.Targeting;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Medical.Presentation;

public sealed class BodyZoneTargetWidget : Control
{
    public event Action<TargetBodyZone>? ZoneClicked;
    public Func<TargetBodyZone?>? GetSelectedZone;

    private const int LayoutWidth = 32;
    private const int LayoutHeight = 32;
    private const int DisplaySize = 96;
    private const string BaseState = "zone_sel";

    private static readonly ResPath RsiPath =
        new("/Textures/_CMU14/Medical/HUD/targetdoll.rsi");

    private static readonly ZoneButton[] ZoneButtons =
    {
        new(TargetBodyZone.Head, "head", new UIBox2(11, 0, 21, 8)),
        new(TargetBodyZone.RightArm, "rightarm", new UIBox2(5, 7, 13, 18)),
        new(TargetBodyZone.Chest, "torso", new UIBox2(10, 7, 22, 15)),
        new(TargetBodyZone.LeftArm, "leftarm", new UIBox2(19, 7, 27, 18)),
        new(TargetBodyZone.RightHand, "righthand", new UIBox2(1, 13, 10, 22)),
        new(TargetBodyZone.GroinPelvis, "groin", new UIBox2(11, 14, 21, 20)),
        new(TargetBodyZone.LeftHand, "lefthand", new UIBox2(22, 13, 31, 22)),
        new(TargetBodyZone.RightLeg, "rightleg", new UIBox2(8, 18, 16, 29)),
        new(TargetBodyZone.LeftLeg, "leftleg", new UIBox2(16, 18, 24, 29)),
        new(TargetBodyZone.RightFoot, "rightfoot", new UIBox2(5, 27, 15, 32)),
        new(TargetBodyZone.LeftFoot, "leftfoot", new UIBox2(17, 27, 27, 32)),
    };

    private readonly Dictionary<string, Texture> _textures = new();
    private TargetBodyZone? _hovered;

    public BodyZoneTargetWidget()
    {
        HorizontalAlignment = HAlignment.Right;
        VerticalAlignment = VAlignment.Bottom;

        var resCache = IoCManager.Resolve<IResourceCache>();
        var rsi = resCache.GetResource<RSIResource>(RsiPath).RSI;

        LoadState(rsi, BaseState);
        foreach (var button in ZoneButtons)
        {
            LoadState(rsi, button.State);
            LoadState(rsi, $"{button.State}_hover");
        }

        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(DisplaySize, DisplaySize);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        DrawState(handle, BaseState);

        var selected = GetSelectedZone?.Invoke();
        foreach (var button in ZoneButtons)
            DrawButton(handle, button, selected);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        _hovered = ZoneAt(args.RelativePosition);
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hovered = null;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (ZoneAt(args.RelativePosition) is { } zone)
        {
            ZoneClicked?.Invoke(zone);
            args.Handle();
        }
    }

    private void LoadState(RSI rsi, string state)
    {
        if (rsi.TryGetState(state, out var texture))
            _textures[state] = texture.Frame0;
    }

    private void DrawButton(DrawingHandleScreen handle, ZoneButton button, TargetBodyZone? selected)
    {
        if (button.Zone == _hovered)
        {
            DrawState(handle, $"{button.State}_hover");
            return;
        }

        if (button.Zone == selected)
            DrawState(handle, button.State);
    }

    private void DrawState(DrawingHandleScreen handle, string state)
    {
        if (!_textures.TryGetValue(state, out var texture))
            return;

        handle.DrawTextureRect(texture, ScaleRect(new UIBox2(0, 0, LayoutWidth, LayoutHeight)));
    }

    private UIBox2 ScaleRect(UIBox2 rect)
    {
        var scaleX = PixelSize.X / LayoutWidth;
        var scaleY = PixelSize.Y / LayoutHeight;
        return new UIBox2(
            rect.Left * scaleX,
            rect.Top * scaleY,
            rect.Right * scaleX,
            rect.Bottom * scaleY);
    }

    private TargetBodyZone? ZoneAt(Vector2 relativePos)
    {
        if (Size.X <= 0 || Size.Y <= 0)
            return null;

        var x = relativePos.X * LayoutWidth / Size.X;
        var y = relativePos.Y * LayoutHeight / Size.Y;

        foreach (var button in ZoneButtons)
        {
            var rect = button.Rect;
            if (x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom)
                return button.Zone;
        }

        return null;
    }

    private readonly record struct ZoneButton(
        TargetBodyZone Zone,
        string State,
        UIBox2 Rect);
}
