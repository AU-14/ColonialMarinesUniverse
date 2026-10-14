using Content.Client._RMC14.Vehicle;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Vehicle.Components;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Plays an open vehicle's wheel animation backwards while it reverses. RMC's wheel visuals only play
/// it forwards; the overlay's wheels and the rims copy this layer's time, so they turn back with it.
/// </summary>
public sealed class CMUVehicleWheelReverseVisualSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, float> _times = new();
    private readonly List<EntityUid> _gone = new();

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(SpriteSystem));
        UpdatesAfter.Add(typeof(VehicleWheelVisualizerSystem));
        UpdatesBefore.Add(typeof(CMUVehicleOverlayVisualSystem));
        UpdatesBefore.Add(typeof(CMUVehicleRimVisualSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        _gone.Clear();
        foreach (var uid in _times.Keys)
        {
            if (TerminatingOrDeleted(uid))
                _gone.Add(uid);
        }

        foreach (var uid in _gone)
        {
            _times.Remove(uid);
        }

        var query = EntityQueryEnumerator<CMUOpenVehicleComponent, GridVehicleMoverComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var mover, out var sprite))
        {
            if (mover.CurrentSpeed > -0.01f ||
                !_sprite.LayerMapTryGet((uid, sprite), VehicleWheelLayers.Wheels, out var index, false) ||
                sprite[index] is not SpriteComponent.Layer layer ||
                !layer.AutoAnimated ||
                layer.ActualRsi is not { } rsi ||
                !rsi.TryGetState(layer.State, out var state) ||
                !state.IsAnimated)
            {
                _times.Remove(uid);
                continue;
            }

            // The sprite system has just stepped it forwards; step back from where it was instead.
            var period = state.TotalDelay;
            var time = _times.TryGetValue(uid, out var last) ? last : layer.AnimationTime % period;
            time = ((time - frameTime) % period + period) % period;
            _times[uid] = time;
            _sprite.LayerSetAnimationTime((uid, sprite), index, time);
        }
    }
}
