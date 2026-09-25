using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Shared.Vehicle;

/// <summary>
/// Self-damage belongs to an impact, not every movement probe against the same
/// obstacle. The chassis must move clear before that obstacle can hurt it again.
/// </summary>
public sealed class VehicleCollisionContactTracker
{
    private readonly Dictionary<EntityUid, Dictionary<EntityUid, Box2>> _contacts = new();
    private readonly List<EntityUid> _separated = new();

    public bool HasContacts(EntityUid vehicle) => _contacts.ContainsKey(vehicle);

    public bool TryStart(EntityUid vehicle, EntityUid target, Box2 vehicleBounds, Box2 targetBounds)
    {
        Update(vehicle, vehicleBounds);
        if (!_contacts.TryGetValue(vehicle, out var contacts))
        {
            contacts = new Dictionary<EntityUid, Box2>();
            _contacts.Add(vehicle, contacts);
        }

        return contacts.TryAdd(target, targetBounds);
    }

    public void Update(EntityUid vehicle, Box2 vehicleBounds)
    {
        if (!_contacts.TryGetValue(vehicle, out var contacts))
            return;

        _separated.Clear();
        foreach (var (obstacle, bounds) in contacts)
        {
            // Include the small gap left by a blocked movement probe.
            if (!vehicleBounds.Intersects(bounds.Enlarged(0.2f)))
                _separated.Add(obstacle);
        }

        foreach (var obstacle in _separated)
            contacts.Remove(obstacle);

        if (contacts.Count == 0)
            _contacts.Remove(vehicle);
    }

    public void RemoveVehicle(EntityUid vehicle)
    {
        _contacts.Remove(vehicle);
    }
}
