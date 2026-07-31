using System.Numerics;
using Content.Shared._CMU14.Dropship.TacticalLand;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client._CMU14.Dropship.TacticalLand;

public sealed class DropshipTacticalLandOverlay : Overlay
{
    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _player;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private static readonly Color FootprintClearFill = new(0.05f, 0.85f, 0.55f, 0.08f);
    private static readonly Color FootprintBlockedFill = new(0.95f, 0.15f, 0.12f, 0.08f);
    private static readonly Color GridClear = new(0.25f, 0.95f, 0.70f, 0.16f);
    private static readonly Color GridBlocked = new(1.00f, 0.25f, 0.20f, 0.18f);
    private static readonly Color BlockedFill = new(1.00f, 0.08f, 0.10f, 0.32f);
    private static readonly Color BlockedEdge = new(1.00f, 0.20f, 0.22f, 0.92f);
    private static readonly Color BlockedCross = new(1.00f, 0.80f, 0.72f, 0.78f);
    private static readonly Color PerimeterClear = new(0.18f, 1.00f, 0.62f, 0.92f);
    private static readonly Color PerimeterBlocked = new(1.00f, 0.22f, 0.18f, 0.92f);
    private static readonly Color CenterClear = new(0.70f, 1.00f, 0.85f, 0.95f);
    private static readonly Color CenterBlocked = new(1.00f, 0.70f, 0.62f, 0.95f);

    public DropshipTacticalLandOverlay(IEntityManager entMan, IPlayerManager player)
    {
        _entMan = entMan;
        _player = player;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var local = _player.LocalEntity;
        if (local is not { } localPlayer)
            return;

        if (!_entMan.TryGetComponent(localPlayer, out EyeComponent? eyeComp) || eyeComp.Target is not { } eyeUid)
            return;

        if (!_entMan.TryGetComponent(eyeUid, out DropshipPilotEyeComponent? pilotEye))
            return;

        if (!_entMan.TryGetComponent(eyeUid, out TransformComponent? eyeXform))
            return;

        var transform = _entMan.System<SharedTransformSystem>();
        var eyeWorld = transform.GetWorldPosition(eyeXform);

        var w = pilotEye.Footprint.X;
        var h = pilotEye.Footprint.Y;
        var halfW = w / 2;
        var halfH = h / 2;

        var snapX = MathF.Floor(eyeWorld.X) + 0.5f;
        var snapY = MathF.Floor(eyeWorld.Y) + 0.5f;

        var blocked = new HashSet<Vector2i>(pilotEye.BlockedTiles);
        var handle = args.WorldHandle;
        var clear = pilotEye.ClearForLanding;

        var footprint = new Box2(
            snapX - halfW - 0.5f,
            snapY - halfH - 0.5f,
            snapX + halfW + 0.5f,
            snapY + halfH + 0.5f);

        handle.DrawRect(footprint, clear ? FootprintClearFill : FootprintBlockedFill);
        DrawGrid(handle, footprint, clear ? GridClear : GridBlocked);
        DrawBlockedTiles(handle, blocked, snapX, snapY);

        var perimeter = clear ? PerimeterClear : PerimeterBlocked;
        handle.DrawRect(footprint, perimeter, false);
        handle.DrawRect(footprint.Enlarged(0.08f), perimeter.WithAlpha(0.38f), false);

        DrawCornerBrackets(handle, footprint, perimeter);
        DrawCenterReticle(handle, new Vector2(snapX, snapY), clear ? CenterClear : CenterBlocked);
    }

    private static void DrawGrid(DrawingHandleWorld handle, Box2 footprint, Color color)
    {
        for (var x = MathF.Ceiling(footprint.Left) + 1; x < footprint.Right; x++)
            handle.DrawLine(new Vector2(x, footprint.Bottom), new Vector2(x, footprint.Top), color);

        for (var y = MathF.Ceiling(footprint.Bottom) + 1; y < footprint.Top; y++)
            handle.DrawLine(new Vector2(footprint.Left, y), new Vector2(footprint.Right, y), color);
    }

    private static void DrawBlockedTiles(DrawingHandleWorld handle, HashSet<Vector2i> blocked, float snapX, float snapY)
    {
        foreach (var offset in blocked)
        {
            var cx = snapX + offset.X;
            var cy = snapY + offset.Y;
            var rect = new Box2(cx - 0.5f, cy - 0.5f, cx + 0.5f, cy + 0.5f);
            var inset = rect.Enlarged(-0.12f);

            handle.DrawRect(rect, BlockedFill);
            handle.DrawRect(rect, BlockedEdge, false);
            handle.DrawLine(inset.BottomLeft, inset.TopRight, BlockedCross);
            handle.DrawLine(inset.TopLeft, inset.BottomRight, BlockedCross);
        }
    }

    private static void DrawCornerBrackets(DrawingHandleWorld handle, Box2 box, Color color)
    {
        const float length = 1.6f;
        const float inset = 0.04f;

        var left = box.Left + inset;
        var right = box.Right - inset;
        var bottom = box.Bottom + inset;
        var top = box.Top - inset;

        handle.DrawLine(new Vector2(left, bottom), new Vector2(left + length, bottom), color);
        handle.DrawLine(new Vector2(left, bottom), new Vector2(left, bottom + length), color);

        handle.DrawLine(new Vector2(right, bottom), new Vector2(right - length, bottom), color);
        handle.DrawLine(new Vector2(right, bottom), new Vector2(right, bottom + length), color);

        handle.DrawLine(new Vector2(left, top), new Vector2(left + length, top), color);
        handle.DrawLine(new Vector2(left, top), new Vector2(left, top - length), color);

        handle.DrawLine(new Vector2(right, top), new Vector2(right - length, top), color);
        handle.DrawLine(new Vector2(right, top), new Vector2(right, top - length), color);
    }

    private static void DrawCenterReticle(DrawingHandleWorld handle, Vector2 center, Color color)
    {
        handle.DrawCircle(center, 0.22f, color, false);
        handle.DrawLine(center + new Vector2(-0.55f, 0f), center + new Vector2(-0.28f, 0f), color);
        handle.DrawLine(center + new Vector2(0.28f, 0f), center + new Vector2(0.55f, 0f), color);
        handle.DrawLine(center + new Vector2(0f, -0.55f), center + new Vector2(0f, -0.28f), color);
        handle.DrawLine(center + new Vector2(0f, 0.28f), center + new Vector2(0f, 0.55f), color);
    }
}
