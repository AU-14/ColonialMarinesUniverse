using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.Hands;

public sealed partial class ShowHandItemOverlay
{
    private readonly Action _cmuDrawHeldItem;
    private (DrawingHandleScreen Handle, EntityUid Entity, Vector2 Position, Vector2 Scale, SpriteComponent Sprite)? _cmuHeldItemDraw;

    private void CMUDrawHeldItem()
    {
        // RenderInRenderTarget invokes this before returning. The caller restores the
        // previous context so nested draws cannot overwrite it or retain a sprite afterward.
        var draw = _cmuHeldItemDraw!.Value;
        draw.Handle.DrawEntity(draw.Entity, draw.Position, draw.Scale, Angle.Zero, Angle.Zero, Direction.South, draw.Sprite);
    }
}
