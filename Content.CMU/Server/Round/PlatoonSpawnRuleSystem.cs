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

    private static readonly ISawmill Sawmill = Logger.GetSawmill("platoonspawn");

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
            Sawmill.Debug("[PlatoonSpawnRuleSystem] No selected planet found in AuRoundSystem.");
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

                Sawmill.Debug($"Looking for ship vendor markers on ship {shipUid}");
                var shipMarkers = _entityManager.EntityQuery<VendorMarkerComponent>(true)
                    .Where(m => m.Ship && _entityManager.GetComponent<TransformComponent>(m.Owner).ParentUid == shipUid)
                    .ToList();
                Sawmill.Debug($"Found {shipMarkers.Count} ship vendor markers on ship {shipUid}");
                foreach (var marker in shipMarkers)
                {
                    var markerClass = marker.Class;
                    var markerUid = marker.Owner;
                    var transform = _entityManager.GetComponent<TransformComponent>(markerUid);
                    Sawmill.Debug($"Processing ship marker {markerUid} (class {markerClass}) on ship {shipUid}");

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
                            Sawmill.Debug($"Spawning door {doorProtoId} at {transform.Coordinates}");
                            _entityManager.SpawnEntity(doorProtoId, transform.Coordinates);
                            Sawmill.Debug($"Spawned door {doorProtoId} at {transform.Coordinates}");
                        }
                        else
                        {
                            Sawmill.Debug($"Could not find door proto {doorProtoId}");
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

                    // --- VENDOR MARKER LOGIC ---
                    if (!shipPlatoon.VendorMarkersByClass.TryGetValue(markerClass, out var vendorProtoId))
                    {
                        Sawmill.Debug($"No vendor proto for class {markerClass} in platoon {shipPlatoon.ID}");
                        continue;
                    }
                    Sawmill.Debug($"Found vendor proto {vendorProtoId} for class {markerClass}");
                    if (!_prototypeManager.TryIndex<EntityPrototype>(vendorProtoId, out var vendorProto))
                    {
                        Sawmill.Debug($"Could not find vendor proto {vendorProtoId}");
                        continue;
                    }
                    Sawmill.Debug($"Spawning vendor {vendorProto.ID} at {transform.Coordinates}");
                    _entityManager.SpawnEntity(vendorProto.ID, transform.Coordinates);
                    Sawmill.Debug($"Spawned vendor {vendorProto.ID} at {transform.Coordinates}");
                }
            }
        }

        // Find all vendor markers in the map
        var query = _entityManager.EntityQuery<VendorMarkerComponent>(true);
        foreach (var marker in query)
        {
            var markerClass = marker.Class;
            var markerUid = marker.Owner;
            var transform = _entityManager.GetComponent<TransformComponent>(markerUid);

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
                if (marker.Govfor)
                    fetchReturnProtoId = "fetchreturngovfor";
                else if (marker.Opfor)
                    fetchReturnProtoId = "fetchreturnopfor";
                // Add more factions as needed
                if (fetchReturnProtoId != null && _prototypeManager.TryIndex(fetchReturnProtoId, out _))
                {
                    _entityManager.SpawnEntity(fetchReturnProtoId, transform.Coordinates);
                }
                continue;
            }

            if (!platoon.VendorMarkersByClass.TryGetValue(markerClass, out var vendorProtoId))
                continue;

            if (!_prototypeManager.TryIndex<EntityPrototype>(vendorProtoId, out var vendorProto))
                continue;

            _entityManager.SpawnEntity(vendorProto.ID, transform.Coordinates);
        }

        // --- DROPSHIP & FIGHTER CONSOLE SPAWNING LOGIC ---
        // Helper: Find a destination entity for a given faction and type
        EntityUid? FindDestination(string faction, DropshipDestinationComponent.DestinationType type)
        {
            foreach (var dest in _entityManager.EntityQuery<DropshipDestinationComponent>(true))
            {
                var destUid = dest.Owner;
                if (_entityManager.TryGetComponent<DropshipDestinationComponent>(destUid, out DropshipDestinationComponent? comp) && comp != null)
                {
                    if (comp.FactionController == faction && comp.Destinationtype == type)
                        return destUid;
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

        // --- For each platoon, select and spawn dropship/fighter consoles ---
        void HandlePlatoonConsoles(PlatoonPrototype? platoon, string faction, int dropshipCount, int fighterCount)
        {
            Sawmill.Debug($"[SPAWN] HandlePlatoonConsoles called for faction={faction}, dropshipCount={dropshipCount}, fighterCount={fighterCount}");
            if (platoon == null)
            {
                Sawmill.Debug($"[SPAWN] Platoon is null for faction {faction}, skipping");
                return;
            }
            var random = new Random();
            Sawmill.Debug($"[SPAWN] CompatibleDropships: {string.Join(", ", platoon.CompatibleDropships)}");
            Sawmill.Debug($"[SPAWN] CompatibleFighters: {string.Join(", ", platoon.CompatibleFighters)}");
            // DROPSHIPS
            var dropships = platoon.CompatibleDropships.ToList();
            for (int i = 0; i < dropshipCount && dropships.Count > 0; i++)
            {
                Sawmill.Debug($"[DROPSHIP] Iteration {i}, dropships left: {string.Join(", ", dropships)}");
                var idx = random.Next(dropships.Count);
                var mapId = dropships[idx];
                Sawmill.Debug($"[DROPSHIP] Attempting to load map: {mapId}");
                dropships.RemoveAt(idx);
                if (!_mapLoader.TryLoadMap(mapId, out _, out var grids))
                {
                    Sawmill.Debug($"[DROPSHIP] Failed to load map: {mapId}");
                    continue;
                }
                Sawmill.Debug($"[DROPSHIP] Loaded map: {mapId}, grids: {string.Join(", ", grids)}");
                foreach (var grid in grids)
                {
                    Sawmill.Debug($"[DROPSHIP] Processing grid {grid} for dropship spawn");
                    // Initialize the map the shuttle is on before flying it
                    var gridMapId = _entityManager.GetComponent<TransformComponent>(grid).MapID;
                    _mapSystem.InitializeMap(gridMapId);
                    // Find nav console marker and spawn nav console
                    var navMarkers = FindMarkersOnGrid(grid, "dropshipshuttlevmarker");
                    Sawmill.Debug($"[DROPSHIP] Found {navMarkers.Count} nav markers on grid {grid}");
                    if (navMarkers.Count > 0)
                    {
                        var navMarkerUid = navMarkers[random.Next(navMarkers.Count)];
                        var navProto = faction == "govfor" ? "CMComputerDropshipNavigation" : "CMComputerDropshipNavigationOpfor";
                        Sawmill.Debug($"[DROPSHIP] Spawning nav console {navProto} at marker {navMarkerUid}");
                        SpawnWeaponsConsole(navProto, navMarkerUid, faction, DropshipDestinationComponent.DestinationType.Dropship);
                    }
                    // Find weapons console marker and spawn weapons console
                    var weaponsMarkers = FindMarkersOnGrid(grid, "dropshipweaponsvmarker");
                    Sawmill.Debug($"[DROPSHIP] Found {weaponsMarkers.Count} weapons markers on grid {grid}");
                    if (weaponsMarkers.Count > 0)
                    {
                        var weaponsMarkerUid = weaponsMarkers[random.Next(weaponsMarkers.Count)];
                        var weaponsProto = faction == "govfor" ? "CMComputerDropshipWeaponsGovfor" : "CMComputerDropshipWeaponsOpfor";
                        Sawmill.Debug($"[DROPSHIP] Spawning weapons console {weaponsProto} at marker {weaponsMarkerUid}");
                        SpawnWeaponsConsole(weaponsProto, weaponsMarkerUid, faction, DropshipDestinationComponent.DestinationType.Dropship);
                    }
                    // Fly to a destination
                    var dest = FindDestination(faction, DropshipDestinationComponent.DestinationType.Dropship);
                    Sawmill.Debug($"[DROPSHIP] Found destination {dest} for faction {faction}");
                    var navComputer = FindNavComputerOnGrid(grid);
                    Sawmill.Debug($"[DROPSHIP] Found nav computer {navComputer} on grid {grid}");
                    if (dest != null && navComputer != null)
                    {
                        var navComp = _entityManager.GetComponent<DropshipNavigationComputerComponent>(navComputer.Value);
                        var navEntity = new Entity<DropshipNavigationComputerComponent>(navComputer.Value, navComp);
                        Sawmill.Debug($"[DROPSHIP] Flying to destination {dest.Value} using nav computer {navComputer.Value}");
                        _sharedDropshipSystem.FlyTo(navEntity, dest.Value, null);
                    }
                    else
                    {
                        Sawmill.Debug($"[DROPSHIP] Could not fly: dest or navComputer is null (dest={dest}, navComputer={navComputer})");
                    }
                }
            }
            // FIGHTERS
            var fighters = platoon.CompatibleFighters.ToList();
            for (int i = 0; i < fighterCount && fighters.Count > 0; i++)
            {
                Sawmill.Debug($"[FIGHTER] Iteration {i}, fighters left: {string.Join(", ", fighters)}");
                var idx = random.Next(fighters.Count);
                var mapId = fighters[idx];
                Sawmill.Debug($"[FIGHTER] Attempting to load map: {mapId}");
                fighters.RemoveAt(idx);
                if (!_mapLoader.TryLoadMap(mapId, out _, out var grids))
                {
                    Sawmill.Debug($"[FIGHTER] Failed to load map: {mapId}");
                    continue;
                }
                Sawmill.Debug($"[FIGHTER] Loaded map: {mapId}, grids: {string.Join(", ", grids)}");
                foreach (var grid in grids)
                {
                    Sawmill.Debug($"[FIGHTER] Processing grid {grid} for fighter spawn");
                    // Initialize the map the shuttle is on before flying it
                    var gridMapId = _entityManager.GetComponent<TransformComponent>(grid).MapID;
                    _mapSystem.InitializeMap(gridMapId);
                    var markers = FindMarkersOnGrid(grid, "dropshipfighterdestmarker");
                    Sawmill.Debug($"[FIGHTER] Found {markers.Count} fighter markers on grid {grid}");
                    if (markers.Count == 0)
                    {
                        Sawmill.Debug($"[FIGHTER] No fighter markers found on grid {grid}, skipping");
                        continue;
                    }
                    var markerUid = markers[random.Next(markers.Count)];
                    Sawmill.Debug($"[FIGHTER] Selected marker {markerUid} on grid {grid}");
                    var proto = faction == "govfor" ? "CMComputerDropshipWeaponsGovfor" : "CMComputerDropshipWeaponsOpfor";
                    Sawmill.Debug($"[FIGHTER] Spawning weapons console {proto} at marker {markerUid}");
                    SpawnWeaponsConsole(proto, markerUid, faction, DropshipDestinationComponent.DestinationType.Figher);
                    // Fly to a destination
                    var dest = FindDestination(faction, DropshipDestinationComponent.DestinationType.Figher);
                    Sawmill.Debug($"[FIGHTER] Found destination {dest} for faction {faction}");
                    var navComputer = FindNavComputerOnGrid(grid);
                    Sawmill.Debug($"[FIGHTER] Found nav computer {navComputer} on grid {grid}");
                    if (dest != null && navComputer != null)
                    {
                        var navComp = _entityManager.GetComponent<DropshipNavigationComputerComponent>(navComputer.Value);
                        var navEntity = new Entity<DropshipNavigationComputerComponent>(navComputer.Value, navComp);
                        Sawmill.Debug($"[FIGHTER] Flying to destination {dest.Value} using nav computer {navComputer.Value}");
                        _sharedDropshipSystem.FlyTo(navEntity, dest.Value, null);
                    }
                    else
                    {
                        Sawmill.Debug($"[FIGHTER] Could not fly: dest or navComputer is null (dest={dest}, navComputer={navComputer})");
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
