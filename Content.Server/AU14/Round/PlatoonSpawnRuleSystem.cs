using System.Linq;
using Content.Server.AU14.VendorMarker;
using Robust.Shared.Prototypes;
using Content.Server.GameTicking.Rules;
using Content.Server.Maps;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.util;
using Content.Shared.GameTicking.Components;
using Robust.Client.GameObjects;
using Robust.Shared.EntitySerialization.Systems;

namespace Content.Server.AU14.Round;

public sealed class PlatoonSpawnRuleSystem : GameRuleSystem<PlatoonSpawnRuleComponent>
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly AuRoundSystem _auRoundSystem = default!;
    [Dependency] private readonly SharedDropshipSystem _sharedDropshipSystem = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    // Store selected platoons in the system
    public PlatoonPrototype? SelectedGovforPlatoon { get; set; }
    public PlatoonPrototype? SelectedOpforPlatoon { get; set; }

    protected override void Started(EntityUid uid, PlatoonSpawnRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        // Get selected platoons from the system
        var govPlatoon = SelectedGovforPlatoon;
        var opPlatoon = SelectedOpforPlatoon;

        // Use the selected planet from AuRoundSystem
        var planetComp = _auRoundSystem.GetSelectedPlanet();
        if (planetComp == null)
        {
            return;
        }

        // Fallback to default platoon if none selected, using planet component
        if (govPlatoon == null && !string.IsNullOrEmpty(planetComp.DefaultGovforPlatoon))
            govPlatoon = _prototypeManager.Index<PlatoonPrototype>(planetComp.DefaultGovforPlatoon);
        if (opPlatoon == null && !string.IsNullOrEmpty(planetComp.DefaultOpforPlatoon))
            opPlatoon = _prototypeManager.Index<PlatoonPrototype>(planetComp.DefaultOpforPlatoon);

        // --- SHIP VENDOR MARKER LOGIC ---
        if ((planetComp.GovforInShip || planetComp.OpforInShip))
        {
            foreach (var (shipUid, shipFaction) in _entityManager.EntityQuery<ShipFactionComponent>(true)
                         .Select(s => (s.Owner, s)))
            {
                PlatoonPrototype? shipPlatoon = null;
                if (shipFaction.Faction == "govfor" && planetComp.GovforInShip && govPlatoon != null)
                    shipPlatoon = govPlatoon;
                else if (shipFaction.Faction == "opfor" && planetComp.OpforInShip && opPlatoon != null)
                    shipPlatoon = opPlatoon;
                else
                    continue;

                var shipMarkers = _entityManager.EntityQuery<VendorMarkerComponent>(true)
                    .Where(m => m.Ship && _entityManager.GetComponent<TransformComponent>(m.Owner).ParentUid == shipUid)
                    .ToList();
                foreach (var marker in shipMarkers)
                {
                    var markerClass = marker.Class;
                    var markerUid = marker.Owner;
                    var transform = _entityManager.GetComponent<TransformComponent>(markerUid);

                    // --- DOOR MARKER LOGIC ---
                    string? doorProtoId = null;
                    switch (markerClass)
                    {
                        case PlatoonMarkerClass.LockedCommandDoor:
                            doorProtoId = shipFaction.Faction == "govfor"
                                ? "CMAirlockCommandGovforLocked"
                                : shipFaction.Faction == "opfor"
                                    ? "CMAirlockCommandOpforLocked"
                                    : null;
                            break;
                        case PlatoonMarkerClass.LockedSecurityDoor:
                            doorProtoId = shipFaction.Faction == "govfor"
                                ? "CMAirlockSecurityGovforLocked"
                                : shipFaction.Faction == "opfor"
                                    ? "CMAirlockSecurityOpforLocked"
                                    : null;
                            break;
                        case PlatoonMarkerClass.LockedGlassDoor:
                            doorProtoId = shipFaction.Faction == "govfor"
                                ? "CMAirlockGovforGlassLocked"
                                : shipFaction.Faction == "opfor"
                                    ? "CMAirlockOpforGlassLocked"
                                    : null;
                            break;
                        case PlatoonMarkerClass.LockedNormalDoor:
                            doorProtoId = shipFaction.Faction == "govfor"
                                ? "CMAirlockGovforLocked"
                                : shipFaction.Faction == "opfor"
                                    ? "CMAirlockOpforLocked"
                                    : null;
                            break;
                    }
                    if (doorProtoId != null)
                    {
                        if (_prototypeManager.TryIndex(doorProtoId, out _))
                        {
                            _entityManager.SpawnEntity(doorProtoId, transform.Coordinates);
                        }
                        else
                        {
                            continue;
                        }
                        continue;
                    }

                    // --- OVERWATCH CONSOLE MARKER LOGIC ---
                    if (markerClass == PlatoonMarkerClass.OverwatchConsole)
                    {
                        string? overwatchConsoleProtoId = null;
                        if (marker.Govfor)
                            overwatchConsoleProtoId = "RMCOverwatchConsoleGovfor";
                        else if (marker.Opfor)
                            overwatchConsoleProtoId = "RMCOverwatchConsoleOpfor";
                        else if (marker.Ship)
                        {
                            // Try to determine ship faction by parent entity
                            var parentUid = transform.ParentUid;
                            if (_entityManager.TryGetComponent<ShipFactionComponent>(parentUid, out var parentShipFaction))
                            {
                                overwatchConsoleProtoId = parentShipFaction.Faction == "govfor"
                                    ? "RMCOverwatchConsoleGovfor"
                                    : parentShipFaction.Faction == "opfor"
                                        ? "RMCOverwatchConsoleOpfor"
                                        : null;
                            }
                        }
                        if (overwatchConsoleProtoId != null && _prototypeManager.TryIndex(overwatchConsoleProtoId, out _))
                        {
                            _entityManager.SpawnEntity(overwatchConsoleProtoId, transform.Coordinates);
                        }
                        continue;
                    }

                    // --- OBJECTIVES CONSOLE MARKER LOGIC ---
                    if (markerClass == PlatoonMarkerClass.ObjectivesConsole)
                    {
                        string? objectivesConsoleProtoId = null;
                        if (shipFaction.Faction == "govfor")
                            objectivesConsoleProtoId = "ComputerObjectivesGovfor";
                        else if (shipFaction.Faction == "opfor")
                            objectivesConsoleProtoId = "ComputerObjectivesOpfor";
                        // Add more factions as needed
                        if (objectivesConsoleProtoId != null && _prototypeManager.TryIndex(objectivesConsoleProtoId, out _))
                        {
                            _entityManager.SpawnEntity(objectivesConsoleProtoId, transform.Coordinates);
                        }
                        continue;
                    }

                    // --- GENERIC FETCH RETURN POINT MARKER LOGIC ---
                    if (markerClass == PlatoonMarkerClass.ReturnPointGeneric)
                    {
                        string? fetchReturnProtoId = null;
                        if (shipFaction.Faction == "govfor")
                            fetchReturnProtoId = "fetchreturngovfor";
                        else if (shipFaction.Faction == "opfor")
                            fetchReturnProtoId = "fetchreturnopfor";
                        // Add more factions as needed
                        if (fetchReturnProtoId != null && _prototypeManager.TryIndex(fetchReturnProtoId, out _))
                        {
                            _entityManager.SpawnEntity(fetchReturnProtoId, transform.Coordinates);
                        }
                        continue;
                    }

                    if (markerClass == PlatoonMarkerClass.DropshipDestination)
                    {
                        string dropshipDestinationProtoId = "CMDropshipDestination";
                        var dropshipEntity = _entityManager.SpawnEntity(dropshipDestinationProtoId, transform.Coordinates);
                        // Inherit the metadata name from the marker
                        if (_entityManager.TryGetComponent<MetaDataComponent>(markerUid, out var markerMeta) &&
                            _entityManager.TryGetComponent<MetaDataComponent>(dropshipEntity, out var destMeta))
                        {
                            _metaData.SetEntityName(dropshipEntity, markerMeta.EntityName, destMeta);
                        }
                        _sharedDropshipSystem.SetFactionController(dropshipEntity, shipFaction.Faction);
                        _sharedDropshipSystem.SetDestinationType(dropshipEntity, "Dropship");
                        continue;
                    }


                    // --- VENDOR MARKER LOGIC (shipside) ---
                    // Ignore marker.Govfor/Opfor, use shipPlatoon and markerClass
                    if (shipPlatoon != null && shipPlatoon.VendorMarkersByClass.TryGetValue(markerClass, out var vendorProtoId))
                    {
                        if (_prototypeManager.TryIndex<EntityPrototype>(vendorProtoId, out var vendorProto))
                        {
                            _entityManager.SpawnEntity(vendorProto.ID, transform.Coordinates);
                        }
                    }
                }
            }
        }

        // Find all vendor markers in the map
        var query = _entityManager.EntityQuery<VendorMarkerComponent>(true);
        var usedMarkers = new HashSet<EntityUid>();
        foreach (var marker in query)
        {
            var markerClass = marker.Class;
            var markerUid = marker.Owner;
            var transform = _entityManager.GetComponent<TransformComponent>(markerUid);

            // Skip markers that are both or neither
            if ((marker.Govfor && marker.Opfor) || (!marker.Govfor && !marker.Opfor))
                continue;
            if (usedMarkers.Contains(markerUid))
                continue;

            PlatoonPrototype? platoon = null;
            if (marker.Govfor && govPlatoon != null)
                platoon = govPlatoon;
            else if (marker.Opfor && opPlatoon != null)
                platoon = opPlatoon;
            else
                continue;

            // --- OVERWATCH CONSOLE MARKER LOGIC ---
            if (markerClass == PlatoonMarkerClass.OverwatchConsole)
            {
                string? overwatchConsoleProtoId = null;
                if (marker.Govfor)
                    overwatchConsoleProtoId = "RMCOverwatchConsoleGovfor";
                else if (marker.Opfor)
                    overwatchConsoleProtoId = "RMCOverwatchConsoleOpfor";
                else if (marker.Ship)
                {
                    // Try to determine ship faction by parent entity
                    var parentUid = transform.ParentUid;
                    if (_entityManager.TryGetComponent<ShipFactionComponent>(parentUid, out var shipFaction))
                    {
                        overwatchConsoleProtoId = shipFaction.Faction == "govfor"
                            ? "RMCOverwatchConsoleGovfor"
                            : shipFaction.Faction == "opfor"
                                ? "RMCOverwatchConsoleOpfor"
                                : null;
                    }
                }
                if (overwatchConsoleProtoId != null && _prototypeManager.TryIndex(overwatchConsoleProtoId, out _))
                {
                    _entityManager.SpawnEntity(overwatchConsoleProtoId, transform.Coordinates);
                }
                usedMarkers.Add(markerUid);
                continue;
            }

            // --- OBJECTIVES CONSOLE MARKER LOGIC ---
            if (markerClass == PlatoonMarkerClass.ObjectivesConsole)
            {
                string? objectivesConsoleProtoId = null;
                if (marker.Govfor)
                    objectivesConsoleProtoId = "ComputerObjectivesGovfor";
                else if (marker.Opfor)
                    objectivesConsoleProtoId = "ComputerObjectivesOpfor";
                if (objectivesConsoleProtoId != null && _prototypeManager.TryIndex(objectivesConsoleProtoId, out _))
                {
                    _entityManager.SpawnEntity(objectivesConsoleProtoId, transform.Coordinates);
                }
                usedMarkers.Add(markerUid);
                continue;
            }

            // --- VENDOR MARKER LOGIC ---
            if (!platoon.VendorMarkersByClass.TryGetValue(markerClass, out var vendorProtoId))
                continue;
            if (!_prototypeManager.TryIndex<EntityPrototype>(vendorProtoId, out var vendorProto))
                continue;
            _entityManager.SpawnEntity(vendorProto.ID, transform.Coordinates);
            usedMarkers.Add(markerUid);
        }

        // --- DROPSHIP & FIGHTER CONSOLE SPAWNING LOGIC ---
        // Helper: Find a destination entity for a given faction and type, optionally filtering by grid
        EntityUid? FindDestination(string faction, DropshipDestinationComponent.DestinationType type, EntityUid? gridUid = null)
        {
            foreach (var dest in _entityManager.EntityQuery<DropshipDestinationComponent>(true))
            {
                var destUid = dest.Owner;
                if (_entityManager.TryGetComponent<DropshipDestinationComponent>(destUid, out DropshipDestinationComponent? comp) && comp != null)
                {
                    if (comp.FactionController == faction && comp.Destinationtype == type)
                    {
                        if (gridUid != null)
                        {
                            if (_entityManager.GetComponent<TransformComponent>(destUid).GridUid == gridUid)
                                return destUid;
                        }
                        else
                        {
                            return destUid;
                        }
                    }
                }
            }
            return null;
        }

        // Helper: For a given grid, find all marker UIDs of a given prototype ID
        List<EntityUid> FindMarkersOnGrid(EntityUid grid, string markerProtoId)
        {
            var result = new List<EntityUid>();
            foreach (var ent in _entityManager.EntityQuery<VendorMarkerComponent>())
            {
                var entUid = ent.Owner;
                if (_entityManager.GetComponent<TransformComponent>(entUid).GridUid == grid &&
                    _entityManager.TryGetComponent<MetaDataComponent>(entUid, out var meta) &&
                    meta.EntityPrototype != null &&
                    meta.EntityPrototype.ID == markerProtoId)
                {
                    result.Add(entUid);
                }
            }
            return result;
        }

        // Helper: Find a navigation computer on a grid
        EntityUid? FindNavComputerOnGrid(EntityUid grid)
        {
            foreach (var comp in _entityManager.EntityQuery<DropshipNavigationComputerComponent>(true))
            {
                var entUid = comp.Owner;
                if (_entityManager.GetComponent<TransformComponent>(entUid).GridUid == grid)
                    return entUid;
            }
            return null;
        }

        // Helper: Spawn and configure a weapons console at a marker
        void SpawnWeaponsConsole(string protoId, EntityUid markerUid, string faction, DropshipDestinationComponent.DestinationType type)
        {
            var transform = _entityManager.GetComponent<TransformComponent>(markerUid);
            var console = _entityManager.SpawnEntity(protoId, transform.Coordinates);
            if (!_entityManager.HasComponent<WhitelistedShuttleComponent>(console))
                _entityManager.AddComponent<WhitelistedShuttleComponent>(console);
            var whitelist = _entityManager.GetComponent<WhitelistedShuttleComponent>(console);
            whitelist.Faction = faction;
            whitelist.ShuttleType = type;
        }


        void HandlePlatoonConsoles(PlatoonPrototype? platoon, string faction, int dropshipCount, int fighterCount)
        {
            if (platoon == null)
            {
                return;
            }
            var random = new Random();
            var dropships = platoon.CompatibleDropships.ToList();
            for (int i = 0; i < dropshipCount && dropships.Count > 0; i++)
            {
                var idx = random.Next(dropships.Count);
                var mapId = dropships[idx];
                dropships.RemoveAt(idx);
                if (!_mapLoader.TryLoadMap(mapId, out _, out var grids))
                {
                    continue;
                }
                foreach (var grid in grids)
                {
                    var gridMapId = _entityManager.GetComponent<TransformComponent>(grid).MapID;
                    _mapSystem.InitializeMap(gridMapId);
                    var navMarkers = FindMarkersOnGrid(grid, "dropshipshuttlevmarker");
                    if (navMarkers.Count > 0)
                    {
                        var navProto = faction == "govfor" ? "CMComputerDropshipNavigationGovfor" : "CMComputerDropshipNavigationOpfor";
                        foreach (var navMarkerUid in navMarkers)
                        {
                            SpawnWeaponsConsole(navProto, navMarkerUid, faction, DropshipDestinationComponent.DestinationType.Dropship);
                        }
                    }
                    var weaponsMarkers = FindMarkersOnGrid(grid, "dropshipweaponsvmarker");
                    if (weaponsMarkers.Count > 0)
                    {
                        var weaponsProto = faction == "govfor" ? "CMComputerDropshipWeaponsGovfor" : "CMComputerDropshipWeaponsOpfor";
                        foreach (var weaponsMarkerUid in weaponsMarkers)
                        {
                            SpawnWeaponsConsole(weaponsProto, weaponsMarkerUid, faction, DropshipDestinationComponent.DestinationType.Dropship);
                        }
                    }
                    // Fly to a destination, prioritizing ship destinations if in ship
                    EntityUid? dest = null;
                    bool inShip = (faction == "govfor" && planetComp != null && planetComp.GovforInShip) || (faction == "opfor" && planetComp != null && planetComp.OpforInShip);
                    if (inShip)
                    {
                        dest = FindDestination(faction, DropshipDestinationComponent.DestinationType.Dropship, grid);
                        if (dest == null)
                        {
                            dest = FindDestination(faction, DropshipDestinationComponent.DestinationType.Dropship);
                        }
                    }
                    else
                    {
                        dest = FindDestination(faction, DropshipDestinationComponent.DestinationType.Dropship);
                    }
                    var navComputer = FindNavComputerOnGrid(grid);
                    if (dest != null && navComputer != null)
                    {
                        var navComp = _entityManager.GetComponent<DropshipNavigationComputerComponent>(navComputer.Value);
                        var navEntity = new Entity<DropshipNavigationComputerComponent>(navComputer.Value, navComp);
                        _sharedDropshipSystem.FlyTo(navEntity, dest.Value, null);
                    }
                }
            }

            // FIGHTERS
            var fighters = platoon.CompatibleFighters.ToList();
            var loadedFighterGrids = new List<EntityUid>();
            foreach (var fighterMap in fighters.ToList())
            {
                if (!_mapLoader.TryLoadMap(fighterMap, out _, out var grids))
                {
                    continue;
                }
                foreach (var grid in grids)
                {
                    loadedFighterGrids.Add(grid);
                    var fighterMarkers = FindMarkersOnGrid(grid, "dropshipfighterdestmarker");
                    if (fighterMarkers.Count > 0)
                    {
                        var proto = faction == "govfor" ? "CMComputerDropshipNavigationGovfor" : "CMComputerDropshipNavigationOpfor";
                        foreach (var markerUid in fighterMarkers)
                        {
                            SpawnWeaponsConsole(proto, markerUid, faction, DropshipDestinationComponent.DestinationType.Figher);
                        }
                    }
                    var weaponsMarkers = FindMarkersOnGrid(grid, "dropshipweaponsvmarker");
                    if (weaponsMarkers.Count > 0)
                    {
                        var weaponsProto = faction == "govfor" ? "CMComputerDropshipWeaponsGovfor" : "CMComputerDropshipWeaponsOpfor";
                        foreach (var weaponsMarkerUid in weaponsMarkers)
                        {
                            SpawnWeaponsConsole(weaponsProto, weaponsMarkerUid, faction, DropshipDestinationComponent.DestinationType.Figher);
                        }
                    }
                    // Fly to a destination, prioritizing ship destinations if in ship
                    EntityUid? dest = null;
                    bool inShip = (faction == "govfor" && planetComp != null && planetComp.GovforInShip) || (faction == "opfor" && planetComp != null && planetComp.OpforInShip);
                    if (inShip)
                    {
                        dest = FindDestination(faction, DropshipDestinationComponent.DestinationType.Figher, grid);
                        if (dest == null)
                        {
                            dest = FindDestination(faction, DropshipDestinationComponent.DestinationType.Figher);
                        }
                    }
                    else
                    {
                        dest = FindDestination(faction, DropshipDestinationComponent.DestinationType.Figher);
                    }
                    var navComputer = FindNavComputerOnGrid(grid);
                    if (dest != null && navComputer != null)
                    {
                        var navComp = _entityManager.GetComponent<DropshipNavigationComputerComponent>(navComputer.Value);
                        var navEntity = new Entity<DropshipNavigationComputerComponent>(navComputer.Value, navComp);
                        _sharedDropshipSystem.FlyTo(navEntity, dest.Value, null);
                    }
                }
            }
        }
        // Use the planet config to determine how many to spawn
        var govforDropships = planetComp.govfordropships;
        var govforFighters = planetComp.govforfighters;
        var opforDropships = planetComp.opfordropships;
        var opforFighters = planetComp.opforfighters;
        HandlePlatoonConsoles(govPlatoon, "govfor", govforDropships, govforFighters);
        HandlePlatoonConsoles(opPlatoon, "opfor", opforDropships, opforFighters);
    }
}
