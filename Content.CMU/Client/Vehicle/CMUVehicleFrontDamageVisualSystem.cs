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
    private readonly Dictionary<(string Prefix, int Level), string> _stateNames = new();

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUVehicleFrontDamageComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var front, out var sprite))
        {
            // Out of view: nothing to draw until it comes back.
            if ((MetaData(uid).Flags & MetaDataFlags.Detached) != 0)
                continue;

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
        if (level <= 0)
            return;

        if (!_stateNames.TryGetValue((state, level), out var name))
        {
            if (_stateNames.Count >= 256)
                _stateNames.Clear();
            name = state + level;
            _stateNames.Add((state, level), name);
        }

        if (_sprite.LayerGetRsiState(ent.AsNullable(), index) != name)
            _sprite.LayerSetRsiState(ent.AsNullable(), index, name);
    }
}
