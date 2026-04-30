using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._CMU14.Medical.BodyPart;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Medical.HUD;

public sealed class BodyZoneTargetWidget : Control
{
    public event Action<TargetBodyZone>? ZoneClicked;
    public Func<TargetBodyZone?>? GetSelectedZone;

    private const int DollSize = 32;
    private const int DollScale = 2;

    private static readonly ResPath RsiPath =
        new("/Textures/_CMU14/Medical/HUD/targetdoll.rsi");

    private static readonly Dictionary<TargetBodyZone, string[]> ZoneParts = new()
    {
        [TargetBodyZone.Head] = new[] { "head", "eyes", "mouth" },
        [TargetBodyZone.Chest] = new[] { "torso" },
        [TargetBodyZone.GroinPelvis] = new[] { "groin" },
        [TargetBodyZone.LeftArm] = new[] { "leftarm", "lefthand" },
        [TargetBodyZone.RightArm] = new[] { "rightarm", "righthand" },
        [TargetBodyZone.LeftLeg] = new[] { "leftleg", "leftfoot" },
        [TargetBodyZone.RightLeg] = new[] { "rightleg", "rightfoot" },
    };

    // Hit-test rectangles in 32x32 doll space. Right/Bottom are exclusive.
    private static readonly (TargetBodyZone Zone, UIBox2 Rect)[] ZoneRects =
    {
        (TargetBodyZone.Head,        new UIBox2( 8,  0, 23,  8)),
        (TargetBodyZone.Chest,       new UIBox2(11,  8, 20, 18)),
        (TargetBodyZone.GroinPelvis, new UIBox2(11, 18, 20, 22)),
        (TargetBodyZone.LeftArm,     new UIBox2(20,  9, 24, 21)),
        (TargetBodyZone.RightArm,    new UIBox2( 7,  9, 11, 21)),
        (TargetBodyZone.LeftLeg,     new UIBox2(16, 20, 24, 30)),
        (TargetBodyZone.RightLeg,    new UIBox2( 8, 20, 16, 30)),
    };

    private readonly Dictionary<string, Texture> _parts = new();

    private TargetBodyZone? _hovered;

    public BodyZoneTargetWidget()
    {
        HorizontalAlignment = HAlignment.Right;
        VerticalAlignment = VAlignment.Bottom;

        var resCache = IoCManager.Resolve<IResourceCache>();
        var rsi = resCache.GetResource<RSIResource>(RsiPath).RSI;

        foreach (var partList in ZoneParts.Values)
        {
            foreach (var part in partList)
            {
                if (rsi.TryGetState(part, out var s))
                    _parts[part] = s.Frame0;
                if (rsi.TryGetState(part + "_hover", out var sh))
                    _parts[part + "_hover"] = sh.Frame0;
            }
        }

        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(DollSize * DollScale, DollSize * DollScale);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var rect = new UIBox2(Vector2.Zero, PixelSize);

        var selected = GetSelectedZone?.Invoke();

        foreach (var (zone, parts) in ZoneParts)
        {
            var active = zone == selected || zone == _hovered;
            foreach (var part in parts)
            {
                var key = active ? part + "_hover" : part;
                if (!_parts.TryGetValue(key, out var tex))
                    continue;
                handle.DrawTextureRect(tex, rect);
            }
        }
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

    private TargetBodyZone? ZoneAt(Vector2 relativePos)
    {
        if (Size.X <= 0 || Size.Y <= 0)
            return null;

        var dollX = relativePos.X * DollSize / Size.X;
        var dollY = relativePos.Y * DollSize / Size.Y;

        foreach (var (zone, r) in ZoneRects)
        {
            if (dollX >= r.Left && dollX < r.Right && dollY >= r.Top && dollY < r.Bottom)
                return zone;
        }
        return null;
    }
}
