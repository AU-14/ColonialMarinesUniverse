using Robust.Shared.GameObjects;

namespace Content.Shared.Vehicle;

/// <summary>
/// Tracks short collision cooldowns independently for each vehicle and obstacle.
/// This prevents one wall from suppressing self-damage for every other wall hit
/// during the same ram.
/// </summary>
public sealed class VehicleCollisionCooldownTracker
{
    private readonly Dictionary<(EntityUid Vehicle, EntityUid Target), TimeSpan> _cooldowns = new();
    private readonly List<(EntityUid Vehicle, EntityUid Target)> _expired = new();

    public bool IsActive(EntityUid vehicle, EntityUid target, TimeSpan now)
    {
        var key = (vehicle, target);
        if (!_cooldowns.TryGetValue(key, out var next))
            return false;

        if (now < next)
            return true;

        _cooldowns.Remove(key);
        return false;
    }

    public void Start(EntityUid vehicle, EntityUid target, TimeSpan now, TimeSpan duration)
    {
        RemoveExpired(now);
        _cooldowns[(vehicle, target)] = now + duration;
    }

    public void RemoveVehicle(EntityUid vehicle)
    {
        _expired.Clear();
        foreach (var key in _cooldowns.Keys)
        {
            if (key.Vehicle == vehicle)
                _expired.Add(key);
        }

        RemoveCollected();
    }

    private void RemoveExpired(TimeSpan now)
    {
        _expired.Clear();
        foreach (var (key, expires) in _cooldowns)
        {
            if (expires <= now)
                _expired.Add(key);
        }

        RemoveCollected();
    }

    private void RemoveCollected()
    {
        foreach (var key in _expired)
        {
            _cooldowns.Remove(key);
        }
    }
}
