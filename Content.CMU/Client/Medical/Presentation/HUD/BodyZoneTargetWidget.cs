using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Medical.Presentation.HUD;

public sealed class BodyZoneTargetWidget : Control
{
    public event Action<TargetBodyZone>? ZoneClicked;
    public Func<TargetBodyZone?>? GetSelectedZone;

    private const int LayoutWidth = 32;
    private const int LayoutHeight = 32;
    private const int DisplaySize = 96;
    private const string BaseState = "zone_sel";

    private static readonly ResPath RsiPath =
        new("/Textures/_CMU14/Medical/Presentation/HUD/targetdoll.rsi");

    private static readonly ZoneButton[] ZoneButtons =
    {
        new(TargetBodyZone.Head, new UIBox2(10, 2, 21, 10), new[] { "head", "eyes", "mouth" }),
        new(TargetBodyZone.RightArm, new UIBox2(6, 10, 11, 18), new[] { "rightarm" }),
        new(TargetBodyZone.Chest, new UIBox2(11, 9, 20, 18), new[] { "torso" }),
        new(TargetBodyZone.LeftArm, new UIBox2(20, 10, 25, 18), new[] { "leftarm" }),
        new(TargetBodyZone.RightHand, new UIBox2(6, 18, 11, 24), new[] { "righthand" }),
        new(TargetBodyZone.GroinPelvis, new UIBox2(10, 18, 21, 20), new[] { "groin" }),
        new(TargetBodyZone.LeftHand, new UIBox2(20, 18, 25, 24), new[] { "lefthand" }),
        new(TargetBodyZone.RightLeg, new UIBox2(10, 20, 16, 28), new[] { "rightleg" }),
        new(TargetBodyZone.LeftLeg, new UIBox2(16, 20, 22, 28), new[] { "leftleg" }),
        new(TargetBodyZone.RightFoot, new UIBox2(8, 28, 16, 32), new[] { "rightfoot" }),
        new(TargetBodyZone.LeftFoot, new UIBox2(16, 28, 24, 32), new[] { "leftfoot" }),
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
            foreach (var state in button.States)
            {
                LoadState(rsi, state);
                LoadState(rsi, $"{state}_hover");
            }
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
        _hovered = ZoneAt(args.RelativePixelPosition);
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

        if (ZoneAt(args.RelativePixelPosition) is { } zone)
        {
            ZoneClicked?.Invoke(zone);
            args.Handle();
        }
    }

    private TargetBodyZone? ZoneAt(Vector2 relativePixelPos)
    {
        if (PixelSize.X <= 0 || PixelSize.Y <= 0)
            return null;

        var x = relativePixelPos.X * LayoutWidth / PixelSize.X;
        var y = relativePixelPos.Y * LayoutHeight / PixelSize.Y;

        foreach (var button in ZoneButtons)
        {
            var rect = button.Rect;
            if (x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom)
                return button.Zone;
        }

        return null;
    }

    private void LoadState(RSI rsi, string state)
    {
        if (rsi.TryGetState(state, out var texture))
            _textures[state] = texture.Frame0;
    }

    private void DrawButton(DrawingHandleScreen handle, ZoneButton button, TargetBodyZone? selected)
    {
        var hover = button.Zone == _hovered;
        if (!hover && button.Zone != selected)
            return;

        foreach (var state in button.States)
            DrawState(handle, hover ? $"{state}_hover" : state);
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

    private readonly record struct ZoneButton(
        TargetBodyZone Zone,
        UIBox2 Rect,
        string[] States);
}
