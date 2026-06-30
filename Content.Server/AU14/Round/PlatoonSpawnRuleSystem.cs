using System.Linq;
using Content.Server._CMU14.ZLevels.Core;
using Content.Server.AU14.VendorMarker;
using Robust.Shared.Prototypes;
using Content.Server.GameTicking.Rules;
using Content.Server.Maps;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.Round;
using Content.Shared.AU14.util;
using Content.Shared.GameTicking.Components;
using Robust.Client.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Content.Server._RMC14.Requisitions;
using Content.Shared._RMC14.Telephone;
using Content.Shared._RMC14.Ladder;
using Content.Shared.AU14;

namespace Content.Server.AU14.Round;

public sealed partial class PlatoonSpawnRuleSystem : GameRuleSystem<PlatoonSpawnRuleComponent>
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private SharedDropshipSystem _sharedDropshipSystem = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    // Store selected platoons in the system
    private PlatoonPrototype? _selectedGovforPlatoon;
    public PlatoonPrototype? SelectedGovforPlatoon
    {
        get => _selectedGovforPlatoon;
        set
        {
            _selectedGovforPlatoon = value;
            // Reapply catalogs to any existing requisitions consoles
            var reqSys = EntityManager.EntitySysManager.GetEntitySystem<RequisitionsSystem>();
            reqSys?.ReapplyPlatoonCatalogs();
        }
    }

    private PlatoonPrototype? _selectedOpforPlatoon;
    public PlatoonPrototype? SelectedOpforPlatoon
    {
        get => _selectedOpforPlatoon;
        set
        {
            _selectedOpforPlatoon = value;
            var reqSys = EntityManager.EntitySysManager.GetEntitySystem<RequisitionsSystem>();
            reqSys?.ReapplyPlatoonCatalogs();
        }
    }

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

        // Store the resolved selections back onto the system so other systems can access them
        SelectedGovforPlatoon = govPlatoon;
        SelectedOpforPlatoon = opPlatoon;

        // --- SHIP VENDOR MARKER LOGIC ---
        if ((planetComp.GovforInShip || planetComp.OpforInShip))
        {
            var usedShipMarkers = new HashSet<EntityUid>();
            var factionShipsQuery = AllEntityQuery<ShipFactionComponent>();
            while (factionShipsQuery.MoveNext(out var shipUid, out var shipFaction))
            {
                var shipTransform = _entityManager.GetComponent<TransformComponent>(shipUid);

                // Ensure any existing rotary phones that belong to this ship inherit the ship faction
                if (!string.IsNullOrEmpty(shipFaction.Faction))
                    SetPhonesFactionForParent(shipUid, shipFaction.Faction);

                PlatoonPrototype? shipPlatoon = null;
                if (shipFaction.Faction == "govfor" && planetComp.GovforInShip && govPlatoon != null)
                    shipPlatoon = govPlatoon;
                else if (shipFaction.Faction == "opfor" && planetComp.OpforInShip && opPlatoon != null)
                    shipPlatoon = opPlatoon;
                else
                    continue;

                var shipMarkers = AllEntityQuery<VendorMarkerComponent>();
                while (shipMarkers.MoveNext(out var markerUid, out var markerComp))
                {
                    var transform = _entityManager.GetComponent<TransformComponent>(markerUid);
                    if (!markerComp.Ship ||
                        !IsMarkerOnShipOrZLevel(shipUid, shipTransform, transform) ||
                        !usedShipMarkers.Add(markerUid))
                    {
                        continue;
                    }

                    var doorProtoId = GetLockedDoorPrototype(markerComp.Class, shipFaction.Faction);

                    if (doorProtoId != null)
                    {
                        if (_prototypeManager.TryIndex(doorProtoId, out _))
                            _entityManager.SpawnEntity(doorProtoId, transform.Coordinates);
                        continue;
                    }

                    // --- OVERWATCH CONSOLE MARKER LOGIC ---
                    if (markerComp.Class == PlatoonMarkerClass.OverwatchConsole)
                    {
                        string? overwatchConsoleProtoId = null;
                        if (markerComp.Govfor)
                            overwatchConsoleProtoId = "RMCOverwatchConsoleGovforRotating";
                        else if (markerComp.Opfor)
                            overwatchConsoleProtoId = "RMCOverwatchConsoleOpforRotating";
                        else if (markerComp.Ship)
                        {
                            // Try to determine ship faction by parent entity
                            var parentUid = transform.ParentUid;
                            if (_entityManager.TryGetComponent<ShipFactionComponent>(parentUid, out var parentShipFaction))
                            {
                                overwatchConsoleProtoId = parentShipFaction.Faction == "govfor"
                                    ? "RMCOverwatchConsoleGovforRotating"
                                    : parentShipFaction.Faction == "opfor"
                                        ? "RMCOverwatchConsoleOpforRotating"
                                        : null;
                            }
                        }
                        if (overwatchConsoleProtoId != null && _prototypeManager.TryIndex(overwatchConsoleProtoId, out _))
                        {
                            _entityManager.SpawnEntity(overwatchConsoleProtoId, transform.Coordinates);
                        }
                        continue;
                    }

                    // --- INTEL COMPUTER MARKER LOGIC ---
                    if (markerComp.Class == PlatoonMarkerClass.IntelComputer)
                    {
                        string? intelConsoleProtoId = null;
                        if (markerComp.Govfor)
                            intelConsoleProtoId = "RMCComputerIntelGovfor";
                        else if (markerComp.Opfor)
                            intelConsoleProtoId = "RMCComputerIntelOpfor";
                        else if (markerComp.Ship)
                        {
                            var parentUid = transform.ParentUid;
                            if (_entityManager.TryGetComponent<ShipFactionComponent>(parentUid, out var parentShipFaction))
                            {
                                intelConsoleProtoId = parentShipFaction.Faction == "govfor"
                                    ? "RMCComputerIntelGovfor"
                                    : parentShipFaction.Faction == "opfor"
                                        ? "RMCComputerIntelOpfor"
                                        : null;
                            }
                        }
                        if (intelConsoleProtoId != null && _prototypeManager.TryIndex(intelConsoleProtoId, out _))
                        {
                            _entityManager.SpawnEntity(intelConsoleProtoId, transform.Coordinates);
                        }
                        continue;
                    }

                    // --- TECH TREE CONSOLE MARKER LOGIC ---
                    if (markerComp.Class == PlatoonMarkerClass.TechTree)
                    {
                        string? techTreeProtoId = null;
                        if (markerComp.Govfor)
                            techTreeProtoId = "RMCTechTreeConsoleGovfor";
                        else if (markerComp.Opfor)
                            techTreeProtoId = "RMCTechTreeConsoleOpfor";
                        else if (markerComp.Ship)
                        {
                            var parentUid = transform.ParentUid;
                            if (_entityManager.TryGetComponent<ShipFactionComponent>(parentUid, out var parentShipFaction))
                            {
                                techTreeProtoId = parentShipFaction.Faction == "govfor"
                                    ? "RMCTechTreeConsoleGovfor"
                                    : parentShipFaction.Faction == "opfor"
                                        ? "RMCTechTreeConsoleOpfor"
                                        : null;
                            }
                        }
                        if (techTreeProtoId != null && _prototypeManager.TryIndex(techTreeProtoId, out _))
                        {
                            _entityManager.SpawnEntity(techTreeProtoId, transform.Coordinates);
                        }
                        continue;
                    }

                    // --- GROUNDSIDE OPERATIONS CONSOLE MARKER LOGIC ---
                    if (markerComp.Class == PlatoonMarkerClass.GroundsideOps)
                    {
                        string? groundsideProtoId = null;
                        if (markerComp.Govfor)
                            groundsideProtoId = "RMCGroundsideOperationsConsole";
                        else if (markerComp.Opfor)
                            groundsideProtoId = "RMCGroundsideOperationsConsoleOpfor";
                        else if (markerComp.Ship)
                        {
                            var parentUid = transform.ParentUid;
                            if (_entityManager.TryGetComponent<ShipFactionComponent>(parentUid, out var parentShipFaction))
                            {
                                groundsideProtoId = parentShipFaction.Faction == "govfor"
                                    ? "RMCGroundsideOperationsConsole"
                                    : parentShipFaction.Faction == "opfor"
                                        ? "RMCGroundsideOperationsConsoleOpfor"
                                        : null;
                            }
                        }
                        if (groundsideProtoId != null && _prototypeManager.TryIndex(groundsideProtoId, out _))
                        {
                            _entityManager.SpawnEntity(groundsideProtoId, transform.Coordinates);
                        }
                        continue;
                    }

                    // --- GROUNDSIDE OPS SEGREGATED MARKERS ---
                    if (markerComp.Class == PlatoonMarkerClass.GroundsideOpsGovfor)
                    {
                        _entityManager.SpawnEntity("RMCGroundsideOperationsConsoleGovfor", transform.Coordinates);
                        continue;
                    }
                    if (markerComp.Class == PlatoonMarkerClass.GroundsideOpsOpfor)
                    {
                        _entityManager.SpawnEntity("RMCGroundsideOperationsConsoleOpfor", transform.Coordinates);
                        continue;
                    }

                    // --- ALLIANCE CONSOLE MARKERS ---
                    if (markerComp.Class == PlatoonMarkerClass.AllianceConsoleGovfor)
                    {
                        _entityManager.SpawnEntity("AU14AllianceConsoleGovfor", transform.Coordinates);
                        continue;
                    }
                    if (markerComp.Class == PlatoonMarkerClass.AllianceConsoleOpfor)
                    {
                        _entityManager.SpawnEntity("AU14AllianceConsoleOpfor", transform.Coordinates);
                        continue;
                    }

                    // --- ORBITAL CANNON MARKERS ---
                    if (markerComp.Class == PlatoonMarkerClass.OrbitalCannonGovfor)
                    {
                        _entityManager.SpawnEntity("AU14OrbitalCannonGovfor", transform.Coordinates);
                        continue;
                    }
                    if (markerComp.Class == PlatoonMarkerClass.OrbitalCannonOpfor)
                    {
                        _entityManager.SpawnEntity("AU14OrbitalCannonOpfor", transform.Coordinates);
                        continue;
                    }

                    // --- WITHDRAW CONSOLE MARKERS ---
                    if (markerComp.Class == PlatoonMarkerClass.WithdrawConsoleGovfor)
                    {
                        _entityManager.SpawnEntity("AU14WithdrawConsoleGovFor", transform.Coordinates);
                        continue;
                    }
                    if (markerComp.Class == PlatoonMarkerClass.WithdrawConsoleOpfor)
                    {
                        _entityManager.SpawnEntity("AU14WithdrawConsoleOpFor", transform.Coordinates);
                        continue;
                    }
                    if (markerComp.Class == PlatoonMarkerClass.WithdrawConsoleColony)
                    {
                        _entityManager.SpawnEntity("AU14WithdrawConsoleColony", transform.Coordinates);
                        continue;
                    }

                    // --- COMMAND TABLET MARKERS ---
                    if (markerComp.Class == PlatoonMarkerClass.CommandTabletGovfor)
                    {
                        _entityManager.SpawnEntity("AU14TabletGovfor", transform.Coordinates);
                        continue;
                    }
                    if (markerComp.Class == PlatoonMarkerClass.CommandTabletOpfor)
                    {
                        _entityManager.SpawnEntity("AU14TabletOpfor", transform.Coordinates);
                        continue;
                    }

                    // --- OBJECTIVES CONSOLE MARKER LOGIC ---
                    if (markerComp.Class == PlatoonMarkerClass.ObjectivesConsole)
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
                    if (markerComp.Class == PlatoonMarkerClass.ReturnPointGeneric)
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

                    if (markerComp.Class == PlatoonMarkerClass.DropshipDestination)
                    {
                        string dropshipDestinationProtoId = "CMDropshipDestinationHome";
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
                    // Ignore markerComp.Govfor/Opfor, use shipPlatoon and markerComp.Class
                    if (shipPlatoon != null && TryResolvePlatoonVendor(shipPlatoon, markerComp.Class, out var vendorProtoId))
                    {
                        if (_prototypeManager.TryIndex<EntityPrototype>(vendorProtoId, out var vendorProto))
                        {
                            var spawned = _entityManager.SpawnEntity(vendorProto.ID, transform.Coordinates);
                            if (_entityManager.TryGetComponent<RotaryPhoneComponent>(spawned, out var spawnedPhone))
                            {
                                if (!string.IsNullOrEmpty(shipFaction.Faction))
                                {
                                    spawnedPhone.Faction = shipFaction.Faction;
                                    Dirty(spawned, spawnedPhone);
                                }
                            }
                        }
                    }

                    // --- REQUISITIONS CONSOLE / LIFT MARKER LOGIC (shipside) ---
                    if (markerComp.Class == PlatoonMarkerClass.RequisitionsConsole)
                    {
                        string? reqConsoleProto = null;
                        // Use ship faction directly for ship markers (don't rely on marker govfor/opfor flags)
                        if (shipFaction.Faction == "govfor")
                            reqConsoleProto = "CMASRSConsoleGovfor";
                        else if (shipFaction.Faction == "opfor")
                            reqConsoleProto = "CMASRSConsoleOpfor";

                        if (reqConsoleProto != null && _prototypeManager.TryIndex(reqConsoleProto, out _))
                        {
                            _entityManager.SpawnEntity(reqConsoleProto, transform.Coordinates);
                        }
                        continue;
                    }

                    if (markerComp.Class == PlatoonMarkerClass.RequisitionsLift)
                    {
                        string? liftProto = null;
                        // For ships we can use the ship faction
                        if (shipFaction.Faction == "govfor")
                            liftProto = "CMCargoElevatorGovfor";
                        else if (shipFaction.Faction == "opfor")
                            liftProto = "CMCargoElevatorOpfor";

                        if (liftProto != null && _prototypeManager.TryIndex(liftProto, out _))
                        {
                            _entityManager.SpawnEntity(liftProto, transform.Coordinates);
                        }
                        continue;
                    }

                    // --- ANALYZER MARKER LOGIC (shipside) ---
                    if (markerComp.Class == PlatoonMarkerClass.Analyzer)
                    {
                        string? analyzerProto = null;
                        // Use ship faction directly for ship markers
                        if (shipFaction.Faction == "govfor")
                            analyzerProto = "AU14AnalyzerMachine";
                        else if (shipFaction.Faction == "opfor")
                            analyzerProto = "AU14AnalyzerMachineOpfor";

                        if (analyzerProto != null && _prototypeManager.TryIndex(analyzerProto, out _))
                        {
                            _entityManager.SpawnEntity(analyzerProto, transform.Coordinates);
                        }
                        continue;
                    }
                }
            }
        }

        // Find all vendor markers in the map
        var vendorMarkersQuery = AllEntityQuery<VendorMarkerComponent>();
        var usedMarkers = new HashSet<EntityUid>();
        // foreach (var marker in query)
        while (vendorMarkersQuery.MoveNext(out var markerUid, out var markerComp))
        {
            var transform = _entityManager.GetComponent<TransformComponent>(markerUid);

            // Skip markers that are both or neither
            if ((markerComp.Govfor && markerComp.Opfor) || (!markerComp.Govfor && !markerComp.Opfor))
                continue;
            if (!usedMarkers.Add(markerUid)) // already in set so skip
                continue;

            PlatoonPrototype? platoon = null;
            if (markerComp.Govfor && govPlatoon != null)
                platoon = govPlatoon;
            else if (markerComp.Opfor && opPlatoon != null)
                platoon = opPlatoon;
            else
                continue;

            // --- OVERWATCH CONSOLE MARKER LOGIC ---
            if (markerComp.Class == PlatoonMarkerClass.OverwatchConsole)
            {
                string? overwatchConsoleProtoId = null;
                if (markerComp.Govfor)
                    overwatchConsoleProtoId = "RMCOverwatchConsoleGovfor";
                else if (markerComp.Opfor)
                    overwatchConsoleProtoId = "RMCOverwatchConsoleOpfor";
                else if (markerComp.Ship)
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
                    _entityManager.SpawnEntity(overwatchConsoleProtoId, transform.Coordinates);
                continue;
            }

            // --- OBJECTIVES CONSOLE MARKER LOGIC ---
            if (markerComp.Class == PlatoonMarkerClass.ObjectivesConsole)
            {
                string? objectivesConsoleProtoId = null;
                if (markerComp.Govfor)
                    objectivesConsoleProtoId = "ComputerObjectivesGovfor";
                else if (markerComp.Opfor)
                    objectivesConsoleProtoId = "ComputerObjectivesOpfor";
                if (objectivesConsoleProtoId != null && _prototypeManager.TryIndex(objectivesConsoleProtoId, out _))
                {
                    _entityManager.SpawnEntity(objectivesConsoleProtoId, transform.Coordinates);
                }
                continue;
            }

            // --- VENDOR MARKER LOGIC ---
            if (!TryResolvePlatoonVendor(platoon, markerComp.Class, out var vendorProtoId))
                continue;
            if (!_prototypeManager.TryIndex<EntityPrototype>(vendorProtoId, out var vendorProto))
                continue;
            var spawnedEnt = _entityManager.SpawnEntity(vendorProto.ID, transform.Coordinates);
            if (_entityManager.TryGetComponent<RotaryPhoneComponent>(spawnedEnt, out var spawnedPhone2))
            {
                spawnedPhone2.Faction = markerComp.Govfor ? "govfor" : "opfor";
                Dirty(spawnedEnt, spawnedPhone2);
            }
        }

        HandlePlatoonShuttleSpawns(planetComp, govPlatoon, opPlatoon);
    }

    private void HandlePlatoonShuttleSpawns(
        RMCPlanetMapPrototypeComponent planetComp,
        PlatoonPrototype? govPlatoon,
        PlatoonPrototype? opPlatoon)
    {
        // Track destinations already handed out this round so multiple ships of the same
        // faction/type don't all pile onto the same LZ.
        var usedDestinations = new HashSet<EntityUid>();
        var destinationRandom = new Random();

        LoadPlatoonShuttles(
            planetComp,
            govPlatoon,
            "govfor",
            planetComp.govfordropships,
            planetComp.govforfighters,
            usedDestinations,
            destinationRandom);

        LoadPlatoonShuttles(
            planetComp,
            opPlatoon,
            "opfor",
            planetComp.opfordropships,
            planetComp.opforfighters,
            usedDestinations,
            destinationRandom);
    }

    private void LoadPlatoonShuttles(
        RMCPlanetMapPrototypeComponent planetComp,
        PlatoonPrototype? platoon,
        string faction,
        int dropshipCount,
        int fighterCount,
        HashSet<EntityUid> usedDestinations,
        Random destinationRandom)
    {
        if (platoon == null)
            return;

        var mapRandom = new Random();
        var dropships = platoon.CompatibleDropships.ToList();
        for (var i = 0; i < dropshipCount && dropships.Count > 0; i++)
        {
            var index = mapRandom.Next(dropships.Count);
            var mapId = dropships[index];
            dropships.RemoveAt(index);

            if (!_mapLoader.TryLoadMap(mapId, out _, out var grids))
                continue;

            foreach (var grid in grids)
            {
                var gridMapId = _entityManager.GetComponent<TransformComponent>(grid).MapID;
                _mapSystem.InitializeMap(gridMapId);
                PrepareLoadedShuttleGrid(grid, faction, planetComp);
                SpawnShuttleConsoleMarkers(
                    grid,
                    faction,
                    DropshipDestinationComponent.DestinationType.Dropship,
                    "dropshipshuttlevmarker");
                TryFlyShuttleToDestination(
                    grid,
                    faction,
                    DropshipDestinationComponent.DestinationType.Dropship,
                    planetComp,
                    usedDestinations,
                    destinationRandom);
            }
        }

        var fighters = platoon.CompatibleFighters.ToList();
        for (var i = 0; i < fighterCount && fighters.Count > 0; i++)
        {
            var index = mapRandom.Next(fighters.Count);
            var fighterMap = fighters[index];
            fighters.RemoveAt(index);

            if (!_mapLoader.TryLoadGrid(fighterMap, out _, out var grid))
                continue;

            PrepareLoadedShuttleGrid(grid.Value, faction, planetComp);
            SpawnShuttleConsoleMarkers(
                grid.Value,
                faction,
                DropshipDestinationComponent.DestinationType.Figher,
                "dropshipfighterdestmarker");
            TryFlyShuttleToDestination(
                grid.Value,
                faction,
                DropshipDestinationComponent.DestinationType.Figher,
                planetComp,
                usedDestinations,
                destinationRandom);
        }
    }

    private void PrepareLoadedShuttleGrid(
        EntityUid grid,
        string faction,
        RMCPlanetMapPrototypeComponent planetComp)
    {
        SetPhonesFactionOnGrid(grid, faction);

        if (faction == "opfor" && planetComp.OpforInShip)
            OffsetLaddersOnGrid(grid, 100);
    }

    private void SpawnShuttleConsoleMarkers(
        EntityUid grid,
        string faction,
        DropshipDestinationComponent.DestinationType type,
        string navigationMarkerProtoId)
    {
        var navigationMarkers = FindMarkersOnGrid(grid, navigationMarkerProtoId);
        if (navigationMarkers.Count > 0)
        {
            var navigationProto = faction == "govfor"
                ? "CMComputerDropshipNavigationGovfor"
                : "CMComputerDropshipNavigationOpfor";
            foreach (var markerUid in navigationMarkers)
                SpawnWeaponsConsole(navigationProto, markerUid, faction, type);
        }

        var weaponsMarkers = FindMarkersOnGrid(grid, "dropshipweaponsvmarker");
        if (weaponsMarkers.Count == 0)
            return;

        var weaponsProto = faction == "govfor"
            ? "CMComputerDropshipWeaponsGovfor"
            : "CMComputerDropshipWeaponsOpfor";
        foreach (var markerUid in weaponsMarkers)
            SpawnWeaponsConsole(weaponsProto, markerUid, faction, type);
    }

    private void TryFlyShuttleToDestination(
        EntityUid grid,
        string faction,
        DropshipDestinationComponent.DestinationType type,
        RMCPlanetMapPrototypeComponent planetComp,
        HashSet<EntityUid> usedDestinations,
        Random destinationRandom)
    {
        EntityUid? destination = null;
        if (UsesShipDestination(planetComp, faction))
            destination = FindDestination(faction, type, usedDestinations, destinationRandom, grid);

        destination ??= FindDestination(faction, type, usedDestinations, destinationRandom);

        var navComputer = FindNavComputerOnGrid(grid);
        if (destination == null || navComputer == null)
            return;

        var navComp = _entityManager.GetComponent<DropshipNavigationComputerComponent>(navComputer.Value);
        var navEntity = new Entity<DropshipNavigationComputerComponent>(navComputer.Value, navComp);
        _sharedDropshipSystem.FlyTo(navEntity, destination.Value, null);
    }

    private EntityUid? FindDestination(
        string faction,
        DropshipDestinationComponent.DestinationType type,
        HashSet<EntityUid> usedDestinations,
        Random destinationRandom,
        EntityUid? gridUid = null)
    {
        var candidates = new List<EntityUid>();
        var query = AllEntityQuery<DropshipDestinationComponent>();
        while (query.MoveNext(out var destUid, out var comp))
        {
            if (usedDestinations.Contains(destUid))
                continue;

            if (comp.FactionController != faction || comp.Destinationtype != type)
                continue;

            if (gridUid != null &&
                _entityManager.GetComponent<TransformComponent>(destUid).GridUid != gridUid)
            {
                continue;
            }

            candidates.Add(destUid);
        }

        if (candidates.Count == 0)
            return null;

        var picked = candidates[destinationRandom.Next(candidates.Count)];
        usedDestinations.Add(picked);
        return picked;
    }

    private List<EntityUid> FindMarkersOnGrid(EntityUid grid, string markerProtoId)
    {
        var result = new List<EntityUid>();
        var query = AllEntityQuery<VendorMarkerComponent>();
        while (query.MoveNext(out var markerUid, out _))
        {
            if (_entityManager.GetComponent<TransformComponent>(markerUid).GridUid == grid &&
                _entityManager.TryGetComponent<MetaDataComponent>(markerUid, out var meta) &&
                meta.EntityPrototype != null &&
                meta.EntityPrototype.ID == markerProtoId)
            {
                result.Add(markerUid);
            }
        }

        return result;
    }

    private EntityUid? FindNavComputerOnGrid(EntityUid grid)
    {
        var query = AllEntityQuery<DropshipNavigationComputerComponent>();
        while (query.MoveNext(out var entityUid, out _))
        {
            if (_entityManager.GetComponent<TransformComponent>(entityUid).GridUid == grid)
                return entityUid;
        }

        return null;
    }

    private void SpawnWeaponsConsole(
        string protoId,
        EntityUid markerUid,
        string faction,
        DropshipDestinationComponent.DestinationType type)
    {
        var transform = _entityManager.GetComponent<TransformComponent>(markerUid);
        var console = _entityManager.SpawnEntity(protoId, transform.Coordinates);
        if (!_entityManager.HasComponent<WhitelistedShuttleComponent>(console))
            _entityManager.AddComponent<WhitelistedShuttleComponent>(console);

        var whitelist = _entityManager.GetComponent<WhitelistedShuttleComponent>(console);
        whitelist.Faction = faction;
        whitelist.ShuttleType = type;
    }

    private void SetPhonesFactionOnGrid(EntityUid grid, string faction)
    {
        var query = AllEntityQuery<RotaryPhoneComponent>();
        while (query.MoveNext(out var phoneUid, out var phoneComp))
        {
            if (Transform(phoneUid).GridUid != grid)
                continue;

            phoneComp.Faction = faction;
            Dirty(phoneUid, phoneComp);
        }
    }

    private void SetPhonesFactionForParent(EntityUid parent, string faction)
    {
        if (!_entityManager.TryGetComponent<TransformComponent>(parent, out var parentTransform))
            return;

        var parentGrid = parentTransform.GridUid;
        var query = AllEntityQuery<RotaryPhoneComponent>();
        while (query.MoveNext(out var phoneUid, out var phoneComp))
        {
            if (Transform(phoneUid).ParentUid != parent && Transform(phoneUid).GridUid != parentGrid)
                continue;

            phoneComp.Faction = faction;
            Dirty(phoneUid, phoneComp);
        }
    }

    private void OffsetLaddersOnGrid(EntityUid grid, int offset)
    {
        var query = AllEntityQuery<LadderComponent>();
        while (query.MoveNext(out var ladderUid, out var ladderComp))
        {
            if (Transform(ladderUid).GridUid != grid ||
                ladderComp.Id == null ||
                !int.TryParse(ladderComp.Id, out var numeric))
            {
                continue;
            }

            ladderComp.Id = (numeric + offset).ToString();
            Dirty(ladderUid, ladderComp);
        }
    }

    private static bool UsesShipDestination(RMCPlanetMapPrototypeComponent planetComp, string faction)
    {
        return faction == "govfor" && planetComp.GovforInShip ||
               faction == "opfor" && planetComp.OpforInShip;
    }

    private static string? GetLockedDoorPrototype(PlatoonMarkerClass markerClass, string faction)
    {
        return markerClass switch
        {
            PlatoonMarkerClass.LockedFTLDoor => FactionPrototype(
                faction,
                "CMAirlockGovforLockedFTL",
                "CMAirlockOpforLockedFTL"),
            PlatoonMarkerClass.LockedFTLGlassDoor => FactionPrototype(
                faction,
                "CMAirlockGovforGlassLockedFTL",
                "CMAirlockOpforGlassLockedFTL"),
            PlatoonMarkerClass.LockedCommandDoor => FactionPrototype(
                faction,
                "CMAirlockCommandGovforLocked",
                "CMAirlockCommandOpforLocked"),
            PlatoonMarkerClass.LockedSecurityDoor => FactionPrototype(
                faction,
                "CMAirlockSecurityGovforLocked",
                "CMAirlockSecurityOpforLocked"),
            PlatoonMarkerClass.LockedSecurityDoorGlass => FactionPrototype(
                faction,
                "CMAirlockSecurityGovforGlassLocked",
                "CMAirlockSecurityOpforGlassLocked"),
            PlatoonMarkerClass.LockedGlassDoor => FactionPrototype(
                faction,
                "CMAirlockGovforGlassLocked",
                "CMAirlockOpforGlassLocked"),
            PlatoonMarkerClass.LockedCommandGlassDoor => FactionPrototype(
                faction,
                "CMAirlockCommandGovforGlassLocked",
                "CMAirlockCommandOpforGlassLocked"),
            PlatoonMarkerClass.LockedEngineeringDoor => FactionPrototype(
                faction,
                "CMAirlockEngineerGovforLocked",
                "CMAirlockEngineerOpforLocked"),
            PlatoonMarkerClass.LockedEngineeringGlassDoor => FactionPrototype(
                faction,
                "CMAirlockEngineerGovforGlassLocked",
                "CMAirlockEngineerOpforGlassLocked"),
            PlatoonMarkerClass.LockedMedicalDoor => FactionPrototype(
                faction,
                "CMAirlockMedicalGovforLocked",
                "CMAirlockMedicalOpforLocked"),
            PlatoonMarkerClass.LockedMedicalGlassDoor => FactionPrototype(
                faction,
                "CMAirlockMedicalGovforGlassLocked",
                "CMAirlockMedicalOpforGlassLocked"),
            PlatoonMarkerClass.LockedNormalDoor => FactionPrototype(
                faction,
                "CMAirlockGovforLocked",
                "CMAirlockOpforLocked"),
            _ => null,
        };
    }

    private static string? FactionPrototype(string faction, string govforPrototype, string opforPrototype)
    {
        return faction switch
        {
            "govfor" => govforPrototype,
            "opfor" => opforPrototype,
            _ => null,
        };
    }

    private bool IsMarkerOnShipOrZLevel(EntityUid shipUid, TransformComponent shipTransform, TransformComponent markerTransform)
    {
        if (markerTransform.ParentUid == shipUid || markerTransform.GridUid == shipUid)
            return true;

        if (shipTransform.MapUid is not { } shipMap ||
            markerTransform.MapUid is not { } markerMap)
        {
            return false;
        }

        if (markerMap == shipMap)
            return false;

        if (!_zLevels.TryGetZNetwork(shipMap, out var shipNetwork) ||
            !_zLevels.TryGetZNetwork(markerMap, out var markerNetwork))
        {
            return false;
        }

        return shipNetwork.Value.Owner == markerNetwork.Value.Owner;
    }

    private bool TryResolvePlatoonVendor(
        PlatoonPrototype platoon,
        PlatoonMarkerClass markerClass,
        out EntProtoId vendorProtoId)
    {
        if (platoon.VendorOverrides.TryGetValue(markerClass, out vendorProtoId))
            return true;

        if (platoon.VendorMarkersByClass.TryGetValue(markerClass, out vendorProtoId))
            return true;

        if (platoon.VendorSet != null &&
            _prototypeManager.TryIndex(platoon.VendorSet.Value, out PlatoonVendorSetPrototype? vendorSet) &&
            vendorSet.Vendors.TryGetValue(markerClass, out vendorProtoId))
        {
            return true;
        }

        vendorProtoId = default;
        return false;
    }

    protected override void Ended(EntityUid uid, PlatoonSpawnRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        // Clear selections on rule end/restart so they don't persist across restarts
        SelectedGovforPlatoon = null;
        SelectedOpforPlatoon = null;
    }
}
