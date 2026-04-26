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

    private static readonly Color ClearFill = new(0.20f, 0.85f, 0.35f, 0.10f);
    private static readonly Color BlockedFill = new(0.95f, 0.10f, 0.15f, 0.40f);
    private static readonly Color BlockedEdge = new(1.00f, 0.20f, 0.25f, 0.95f);
    private static readonly Color BlockedEdgeInner = new(1.00f, 0.55f, 0.55f, 0.85f);
    private static readonly Color PerimeterClear = new(0.35f, 0.95f, 0.45f, 0.85f);
    private static readonly Color PerimeterBlocked = new(0.95f, 0.20f, 0.25f, 0.90f);

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

        for (var dx = -halfW; dx <= halfW; dx++)
        {
            for (var dy = -halfH; dy <= halfH; dy++)
            {
                var cx = snapX + dx;
                var cy = snapY + dy;
                var rect = new Box2(cx - 0.5f, cy - 0.5f, cx + 0.5f, cy + 0.5f);

                if (blocked.Contains(new Vector2i(dx, dy)))
                {
                    handle.DrawRect(rect, BlockedFill);
                    handle.DrawRect(rect, BlockedEdge, false);
                    handle.DrawRect(rect.Enlarged(-0.08f), BlockedEdgeInner, false);
                }
                else
                {
                    handle.DrawRect(rect, ClearFill);
                }
            }
        }

        var outer = new Box2(
            snapX - halfW - 0.5f,
            snapY - halfH - 0.5f,
            snapX + halfW + 0.5f,
            snapY + halfH + 0.5f);
        handle.DrawRect(outer, pilotEye.ClearForLanding ? PerimeterClear : PerimeterBlocked, false);
    }
}
