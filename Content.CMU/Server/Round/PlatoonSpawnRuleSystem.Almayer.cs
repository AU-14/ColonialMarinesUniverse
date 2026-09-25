using System.Linq;
using Content.Server.CMU14.Hijack;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;

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
        // Assign each selected airframe a separate hangar, east to west.
        destinations = destinations.OrderByDescending(uid => Transform(uid).LocalPosition.X).ToList();
        if (destinations.Count == 0)
        {
            Log.Error($"Almayer {ToPrettyString(map)} has no dropship landing zones.");
            return;
        }

        var faction = AlmayerFaction(map);
        if (supply.InitialDropshipMaps == null)
        {
            var selected = faction == "opfor" ? SelectedOpforPlatoon : SelectedGovforPlatoon;
            var platoon = selected ?? _prototypeManager.Index(supply.DefaultPlatoon);
            var maps = platoon.CompatibleDropships.Distinct().ToArray();
            Random.Shared.Shuffle(maps);
            supply.InitialDropshipMaps = maps.Take(destinations.Count).ToList();
        }
        for (var i = 0; i < supply.InitialDropshipMaps.Count; i++)
        {
            var path = supply.InitialDropshipMaps[i];
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
            if (string.IsNullOrWhiteSpace(Name(grid)) || Name(grid) == "grid")
                _metaData.SetEntityName(grid, Name(stagingMap.Value));
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
