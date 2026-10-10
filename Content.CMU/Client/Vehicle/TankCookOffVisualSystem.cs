using Content.Shared._RMC14.Vehicle;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Vehicle;

/// <summary>Hide the mounted turret after ejection; retain visibility bookkeeping for entity/PVS cleanup.</summary>
public sealed partial class TankCookOffVisualSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private VehicleTurretSystem _turrets = default!;
    private readonly Dictionary<(EntityUid Uid, int Layer), bool> _hidden = new();
    private readonly HashSet<(EntityUid Uid, int Layer)> _current = new();
    private readonly List<(EntityUid Uid, int Layer)> _restore = new();
    private readonly Dictionary<EntityUid, Color> _charred = new();
    private readonly HashSet<EntityUid> _wrecks = new();
    private readonly List<EntityUid> _restoreColors = new();

    public override void Initialize() => UpdatesAfter.Add(typeof(VehicleTurretVisualSystem));

    public override void FrameUpdate(float frameTime)
    {
        _current.Clear();
        _wrecks.Clear();
        var tanks = EntityQueryEnumerator<ActiveTankCookOffComponent, SpriteComponent>();
        while (tanks.MoveNext(out var uid, out var active, out var sprite))
        {
            if (active.Ruptured)
            {
                _wrecks.Add(uid);
                if (_charred.TryAdd(uid, sprite.Color))
                {
                    var color = sprite.Color;
                    _sprites.SetColor((uid, sprite), new Color(color.R * .45f, color.G * .4f, color.B * .36f, color.A));
                }
            }
            if (active.Ruptured && _sprites.LayerMapTryGet((uid, sprite), "primary", out var layer, false))
                Hide(uid, sprite, layer);
        }
        var turrets = EntityQueryEnumerator<VehicleTurretVisualComponent, SpriteComponent>();
        while (turrets.MoveNext(out var uid, out var visual, out var sprite))
        {
            if (TryGetEntity(visual.Turret, out var turret) && turret is { } found &&
                _turrets.TryGetVehicle(found, out var vehicle) &&
                TryComp<ActiveTankCookOffComponent>(vehicle, out var active) && active.Ruptured)
                Hide(uid, sprite, 0);
        }
        _restore.Clear();
        foreach (var (key, visible) in _hidden)
        {
            if (_current.Contains(key)) continue;
            if (TryComp<SpriteComponent>(key.Uid, out var sprite))
                _sprites.LayerSetVisible((key.Uid, sprite), key.Layer, visible);
            _restore.Add(key);
        }
        foreach (var key in _restore) _hidden.Remove(key);
        _restoreColors.Clear();
        foreach (var (uid, color) in _charred)
        {
            if (_wrecks.Contains(uid)) continue;
            if (TryComp<SpriteComponent>(uid, out var sprite))
                _sprites.SetColor((uid, sprite), color);
            _restoreColors.Add(uid);
        }
        foreach (var uid in _restoreColors) _charred.Remove(uid);
    }

    private void Hide(EntityUid uid, SpriteComponent sprite, int layer)
    {
        var key = (uid, layer);
        _current.Add(key);
        _hidden.TryAdd(key, sprite[layer].Visible);
        _sprites.LayerSetVisible((uid, sprite), layer, false);
    }
}
