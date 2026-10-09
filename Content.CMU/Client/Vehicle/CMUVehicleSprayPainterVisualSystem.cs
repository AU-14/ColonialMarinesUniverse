using Content.Shared.CMU14.Vehicle.Jeep;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Tints the paint in a vehicle spray painter's cup with the colour it is set to.
/// </summary>
public sealed class CMUVehicleSprayPainterVisualSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUVehicleSprayPainterComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var painter, out var sprite))
        {
            if (_sprite.LayerMapTryGet((uid, sprite), "paint", out var layer, false) &&
                sprite[layer].Color != painter.Color)
            {
                _sprite.LayerSetColor((uid, sprite), layer, painter.Color);
            }
        }
    }
}
