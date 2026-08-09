using Robust.Shared.GameObjects;

namespace Content.Server.AU14.Round;

/// <summary>
/// One-shot index of the existing ships, vendor markers, and phones used by initial platoon setup.
/// Entity lists retain their component-query order; consumers revalidate mutable facts before use.
/// </summary>
internal sealed class PlatoonInitialSetupInventory
{
    private readonly Dictionary<EntityUid, List<EntityUid>> _markersByShip = [];
    private readonly Dictionary<EntityUid, PlatoonInitialShipFacts> _shipFacts = [];
    private readonly Dictionary<EntityUid, List<EntityUid>> _shipsByGrid = [];
    private readonly Dictionary<EntityUid, List<EntityUid>> _shipsByNetwork = [];
    private readonly List<EntityUid> _shipsWithoutGrid = [];
    private readonly Dictionary<EntityUid, List<EntityUid>> _phonesByShip = [];

    public readonly List<EntityUid> Ships = [];
    public readonly List<EntityUid> VendorMarkers = [];

    public int IndexedPhones { get; private set; }
    public int ShipMarkerAssignments { get; private set; }
    public int ShipPhoneAssignments { get; private set; }

    public void AddShip(EntityUid ship, EntityUid? grid, EntityUid? map, EntityUid? network)
    {
        if (!_shipFacts.TryAdd(ship, new PlatoonInitialShipFacts(map)))
            return;

        Ships.Add(ship);
        _markersByShip.Add(ship, []);
        _phonesByShip.Add(ship, []);

        if (grid is { } gridUid)
            AddToBucket(_shipsByGrid, gridUid, ship);
        else
            _shipsWithoutGrid.Add(ship);

        if (network is { } networkUid)
            AddToBucket(_shipsByNetwork, networkUid, ship);
    }

    public void AddVendorMarker(
        EntityUid marker,
        EntityUid parent,
        EntityUid? grid,
        EntityUid? map,
        EntityUid? network)
    {
        VendorMarkers.Add(marker);

        // Direct ship ownership takes precedence in the live resolver, but a marker can
        // factually match more than one ship. Keep every candidate so the consumer's
        // existing used-marker set can preserve first-ship-wins behavior.
        if (_markersByShip.TryGetValue(parent, out var parentMarkers))
            AddShipMarker(parentMarkers, marker);

        if (grid is { } gridUid && _markersByShip.TryGetValue(gridUid, out var gridMarkers))
            AddShipMarker(gridMarkers, marker);

        if (network is not { } networkUid ||
            !_shipsByNetwork.TryGetValue(networkUid, out var ships))
        {
            return;
        }

        for (var i = 0; i < ships.Count; i++)
        {
            var ship = ships[i];
            if (_shipFacts.TryGetValue(ship, out var facts) && facts.Map != map)
                AddShipMarker(_markersByShip[ship], marker);
        }
    }

    public void AddPhone(EntityUid phone, EntityUid parent, EntityUid? grid)
    {
        IndexedPhones++;

        if (_phonesByShip.TryGetValue(parent, out var parentPhones))
            AddShipPhone(parentPhones, phone);

        if (grid is { } gridUid)
        {
            if (!_shipsByGrid.TryGetValue(gridUid, out var ships))
                return;

            for (var i = 0; i < ships.Count; i++)
                AddShipPhone(_phonesByShip[ships[i]], phone);
        }
        else
        {
            // This retains the old nullable GridUid equality behavior for nullspace entities.
            for (var i = 0; i < _shipsWithoutGrid.Count; i++)
                AddShipPhone(_phonesByShip[_shipsWithoutGrid[i]], phone);
        }
    }

    public IReadOnlyList<EntityUid> GetShipMarkers(EntityUid ship)
    {
        return _markersByShip.TryGetValue(ship, out var markers)
            ? markers
            : Array.Empty<EntityUid>();
    }

    public IReadOnlyList<EntityUid> GetShipPhones(EntityUid ship)
    {
        return _phonesByShip.TryGetValue(ship, out var phones)
            ? phones
            : Array.Empty<EntityUid>();
    }

    private void AddShipMarker(List<EntityUid> markers, EntityUid marker)
    {
        if (markers.Count > 0 && markers[^1] == marker)
            return;

        markers.Add(marker);
        ShipMarkerAssignments++;
    }

    private void AddShipPhone(List<EntityUid> phones, EntityUid phone)
    {
        if (phones.Count > 0 && phones[^1] == phone)
            return;

        phones.Add(phone);
        ShipPhoneAssignments++;
    }

    private static void AddToBucket(
        Dictionary<EntityUid, List<EntityUid>> buckets,
        EntityUid key,
        EntityUid value)
    {
        if (!buckets.TryGetValue(key, out var entries))
        {
            entries = [];
            buckets.Add(key, entries);
        }

        entries.Add(value);
    }

    private readonly record struct PlatoonInitialShipFacts(EntityUid? Map);
}

/// <summary>
/// One-shot index of the mutable entities used while preparing a loaded platoon shuttle grid.
/// Entries are revalidated by <see cref="PlatoonSpawnRuleSystem"/> immediately before use.
/// </summary>
internal sealed class PlatoonGridSetupInventory(EntityUid grid)
{
    private readonly Dictionary<string, List<EntityUid>> _markersByPrototype =
        new(StringComparer.Ordinal);

    public EntityUid Grid { get; } = grid;

    public int IndexedEntities { get; private set; }

    public readonly List<EntityUid> Ladders = [];
    public readonly List<EntityUid> NavigationComputers = [];
    public readonly List<EntityUid> Phones = [];

    public void RecordEntity()
    {
        IndexedEntities++;
    }

    public void AddMarker(string prototype, EntityUid marker)
    {
        if (!_markersByPrototype.TryGetValue(prototype, out var markers))
        {
            markers = [];
            _markersByPrototype.Add(prototype, markers);
        }

        markers.Add(marker);
    }

    public IReadOnlyList<EntityUid> GetMarkers(string prototype)
    {
        return _markersByPrototype.TryGetValue(prototype, out var markers)
            ? markers
            : Array.Empty<EntityUid>();
    }
}

/// <summary>
/// Round-local destination candidates used by platoon shuttle setup.
/// </summary>
internal sealed class PlatoonDestinationPool
{
    private readonly HashSet<EntityUid> _known = [];
    private readonly HashSet<EntityUid> _used = [];

    public readonly List<EntityUid> Candidates = [];
    public readonly List<EntityUid> Destinations = [];

    public int Count => Destinations.Count;

    public void Add(EntityUid destination)
    {
        if (_known.Add(destination))
            Destinations.Add(destination);
    }

    public bool IsUsed(EntityUid destination)
    {
        return _used.Contains(destination);
    }

    public void MarkUsed(EntityUid destination)
    {
        _used.Add(destination);
    }
}
