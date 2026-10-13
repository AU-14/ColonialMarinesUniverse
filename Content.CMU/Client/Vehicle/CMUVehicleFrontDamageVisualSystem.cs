using Content.Shared.CMU14.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Shows a vehicle's crumpled front: its front damage layers, on the vehicle and on its overlay entity,
/// set to the state for its level and hidden while the front is straight.
/// </summary>
public sealed class CMUVehicleFrontDamageVisualSystem : EntitySystem
{
    [Dependency] private CMUVehicleFrontDamageSystem _frontDamage = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUVehicleFrontDamageComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var front, out var sprite))
        {
            var level = _frontDamage.GetLevel((uid, front));
            SetLayer((uid, sprite), front.Layer, front.State, level);
            if (TryComp(uid, out CMUVehicleOverlayComponent? overlay) &&
                TryComp(overlay.Overlay, out SpriteComponent? overlaySprite))
            {
                SetLayer((overlay.Overlay.Value, overlaySprite), front.Layer, front.OverlayState, level);
            }
        }
    }

    private void SetLayer(Entity<SpriteComponent> ent, string key, string state, int level)
    {
        if (!_sprite.LayerMapTryGet(ent.AsNullable(), key, out var index, false))
            return;

        _sprite.LayerSetVisible(ent.AsNullable(), index, level > 0);
        if (level > 0 && _sprite.LayerGetRsiState(ent.AsNullable(), index) != state + level)
            _sprite.LayerSetRsiState(ent.AsNullable(), index, state + level);
    }
}
