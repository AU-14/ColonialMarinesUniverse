using System.Linq;
using Content.Server.CMU14.Hijack;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14;

namespace Content.Server.CMU14.Round;

public sealed partial class PlatoonSpawnRuleSystem
{
    public bool IsAlmayerLanding(EntityUid destination)
    {
        if (Transform(destination).MapUid is not { } map)
            return false;
        return _zLevels.GetAllNetworkMaps(map).Any(HasComp<CMUAlmayerSupplyComponent>);
    }

    public bool TryInitializeAlmayerDropships(string faction)
    {
        var found = false;
        var query = AllEntityQuery<CMUAlmayerSupplyComponent>();
        while (query.MoveNext(out var map, out var supply))
        {
            if (AlmayerFaction(map) != faction)
                continue;
            found = true;
            InitializeAlmayerDropships(map, supply);
        }
        return found;
    }

    private string AlmayerFaction(EntityUid map)
    {
        var query = AllEntityQuery<ShipFactionComponent, TransformComponent>();
        while (query.MoveNext(out _, out var faction, out var transform))
            if (transform.MapUid == map)
                return faction.Faction ?? "govfor";
        return "govfor";
    }

    public void InitializeAlmayerDropships(EntityUid map, CMUAlmayerSupplyComponent supply)
    {
        var decks = _zLevels.GetAllNetworkMaps(map).ToHashSet();
        var destinations = new List<EntityUid>();
        var query = AllEntityQuery<DropshipDestinationComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var destination, out var transform))
        {
            if (transform.MapUid is { } deck && decks.Contains(deck) &&
                destination.Destinationtype == DropshipDestinationComponent.DestinationType.Dropship &&
                !HasComp<DropshipHijackDestinationComponent>(uid))
                destinations.Add(uid);
        }
        // Port 1 (east) is Alamo; port 2 (west) is Normandy.
        destinations = destinations.OrderByDescending(uid => Transform(uid).LocalPosition.X).ToList();
        if (destinations.Count < supply.DropshipMaps.Count)
        {
            Log.Error($"Almayer {ToPrettyString(map)} has fewer landing zones than its initial dropships.");
            return;
        }

        var faction = AlmayerFaction(map);
        for (var i = 0; i < supply.DropshipMaps.Count; i++)
        {
            var path = supply.DropshipMaps[i];
            if (supply.InitialDropships.ContainsKey(path))
                continue;
            if (!_mapLoader.TryLoadMap(path, out var stagingMap, out var grids) || grids.Count != 1)
            {
                Log.Error($"Could not load Almayer dropship {path}.");
                continue;
            }
            var grid = grids.Single();
            _mapSystem.InitializeMap(stagingMap.Value.Owner);
            SetPhonesFactionOnGrid(grid, faction);
            _metaData.SetEntityName(grid, i == 0 ? "Alamo" : "Normandy");
            SpawnShuttleConsoleMarkers(grid, faction,
                DropshipDestinationComponent.DestinationType.Dropship, "dropshipshuttlevmarker");
            var computer = FindNavComputerOnGrid(grid);
            if (computer is not { } nav || !_sharedDropshipSystem.FlyTo(
                    (nav, Comp<DropshipNavigationComputerComponent>(nav)), destinations[i], null,
                    startupTime: 1, hyperspaceTime: 1, offset: true))
            {
                Log.Error($"Could not start the arrival of Almayer dropship {path}.");
                QueueDel(stagingMap.Value);
                continue;
            }
            supply.InitialDropships.Add(path, grid);
        }
    }
}
