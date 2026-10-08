using System.Linq;
using System.Numerics;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Vehicle.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Raises riders of high seats, like a jeep's gun pedestal, and bounces riders with the body.
/// </summary>
public sealed class CMUVehicleSeatVisualSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const float PixelsPerMeter = 32f;

    private readonly Dictionary<EntityUid, Vector2> _lifted = new();
    private readonly HashSet<EntityUid> _seated = new();
    private readonly List<EntityUid> _released = new();

    public override void FrameUpdate(float frameTime)
    {
        _seated.Clear();
        var seats = EntityQueryEnumerator<CMUVehicleSeatComponent, StrapComponent>();
        while (seats.MoveNext(out var seat, out var strap))
        {
            var bob = CMUVehicleBob.Offset(seat.Vehicle ?? default, CompOrNull<GridVehicleMoverComponent>(seat.Vehicle), _timing.CurTime);
            if (seat.Lift == 0f && bob == 0f && strap.BuckledEntities.All(r => !_lifted.ContainsKey(r)))
                continue;

            foreach (var rider in strap.BuckledEntities)
            {
                if (!TryComp(rider, out SpriteComponent? sprite))
                    continue;

                _seated.Add(rider);
                if (!_lifted.TryGetValue(rider, out var original))
                    _lifted[rider] = original = sprite.Offset;

                _sprite.SetOffset((rider, sprite), original + new Vector2(0, (seat.Lift + bob) / PixelsPerMeter));
            }
        }

        _released.Clear();
        foreach (var (rider, original) in _lifted)
        {
            if (_seated.Contains(rider))
                continue;

            if (TryComp(rider, out SpriteComponent? sprite))
                _sprite.SetOffset((rider, sprite), original);

            _released.Add(rider);
        }

        foreach (var rider in _released)
        {
            _lifted.Remove(rider);
        }
    }
}
