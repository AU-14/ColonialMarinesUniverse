using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.ParaDrop;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// A vehicle crate drawn over everything while it comes down by parachute, and what it leaves once
/// opened: the walls falling over the vehicle, gone once down, and the floor beneath it, with the walls
/// lying flat round it from then on.
/// </summary>
public sealed class CMUVehicleCrateRemainsVisualSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const string Falling = "opening";
    private const string Floor = "floor";
    private const string Flat = "open";

    public override void FrameUpdate(float frameTime)
    {
        var crates = EntityQueryEnumerator<CMUVehicleCrateComponent, SpriteComponent>();
        while (crates.MoveNext(out var uid, out _, out var sprite))
        {
            var depth = (int) (HasComp<ParaDroppingComponent>(uid) ? DrawDepth.Effects : DrawDepth.Objects);
            if (sprite.DrawDepth != depth)
                _sprite.SetDrawDepth((uid, sprite), depth);
        }

        var query = EntityQueryEnumerator<CMUVehicleCrateRemainsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var remains, out var sprite))
        {
            if (!_sprite.LayerMapTryGet((uid, sprite), CMUVehicleCrateVisualLayers.Walls, out var layer, false))
                continue;

            var elapsed = _timing.CurTime - remains.OpenedAt;
            var falling = elapsed < remains.Fall;
            if (remains.Falling)
            {
                _sprite.LayerSetVisible((uid, sprite), layer, falling);
                if (falling && _sprite.LayerGetRsiState((uid, sprite), layer) != Falling)
                {
                    _sprite.LayerSetRsiState((uid, sprite), layer, Falling);
                    _sprite.LayerSetAnimationTime((uid, sprite), layer, (float) elapsed.TotalSeconds);
                }

                continue;
            }

            var state = falling ? Floor : Flat;
            if (_sprite.LayerGetRsiState((uid, sprite), layer) != state)
                _sprite.LayerSetRsiState((uid, sprite), layer, state);
        }
    }
}
