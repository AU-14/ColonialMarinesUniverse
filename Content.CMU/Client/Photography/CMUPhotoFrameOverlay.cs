using System.Numerics;
using Content.Shared.CMU14.Photography;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.CMU14.Photography;

/// <summary>
/// Outlines the square a held camera will photograph around the tile under the mouse; red when out of range.
/// </summary>
public sealed class CMUPhotoFrameOverlay : Overlay
{
    private static readonly Color InRange = Color.White.WithAlpha(0.85f);
    private static readonly Color OutOfRange = Color.FromHex("#FF5050").WithAlpha(0.85f);

    private readonly IEntityManager _entities;
    private readonly IEyeManager _eye = IoCManager.Resolve<IEyeManager>();
    private readonly IInputManager _input = IoCManager.Resolve<IInputManager>();
    private readonly SharedTransformSystem _transform;
    private readonly SharedMapSystem _map;

    /// <summary>Hidden while a photo is being captured, so the outline is not in the picture.</summary>
    public bool Hidden;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public CMUPhotoFrameOverlay(IEntityManager entities)
    {
        _entities = entities;
        _transform = entities.System<SharedTransformSystem>();
        _map = entities.System<SharedMapSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return !Hidden && _entities.System<CMUPhotoClientSystem>().HeldCamera() != null;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_entities.System<CMUPhotoClientSystem>().HeldCamera() is not { } camera)
            return;

        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mouse.MapId != args.MapId
            || !_map.TryFindGridAt(mouse, out var gridUid, out var grid))
            return;

        var tile = _map.WorldToTile(gridUid, grid, mouse.Position);
        var half = (camera.Comp.Size - 1) / 2;
        var box = new Box2(tile.X - half, tile.Y - half, tile.X + half + 1, tile.Y + half + 1);

        var user = _entities.System<CMUPhotoClientSystem>().HeldCameraUser();
        var colour = InRange;
        if (user is { } holder)
        {
            var centre = Vector2.Transform(box.Center, _transform.GetWorldMatrix(gridUid));
            var from = _transform.GetWorldPosition(holder);
            if ((centre - from).Length() > camera.Comp.Range)
                colour = OutOfRange;
        }

        var handle = args.WorldHandle;
        handle.SetTransform(_transform.GetWorldMatrix(gridUid));
        handle.DrawRect(box, colour.WithAlpha(0.08f));
        handle.DrawRect(box, colour, false);

        // Corner brackets, like a viewfinder.
        var arm = MathF.Min(0.4f, box.Width / 4f);
        foreach (var (corner, dx, dy) in new[]
                 {
                     (box.BottomLeft, 1f, 1f), (box.BottomRight, -1f, 1f),
                     (box.TopLeft, 1f, -1f), (box.TopRight, -1f, -1f),
                 })
        {
            handle.DrawLine(corner, corner + new Vector2(dx * arm, 0), colour);
            handle.DrawLine(corner, corner + new Vector2(0, dy * arm), colour);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
