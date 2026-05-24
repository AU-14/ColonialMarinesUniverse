using System.Linq;
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
            var factionShipsQuery = AllEntityQuery<ShipFactionComponent>();
            while (factionShipsQuery.MoveNext(out var shipUid, out var shipFaction))
            {
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
                    if (!markerComp.Ship || transform.ParentUid != shipUid)
                        continue;

                    // --- DOOR MARKER LOGIC ---
                    string? doorProtoId = null;
                    switch (markerComp.Class)
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
                        case PlatoonMarkerClass.LockedSecurityDoorGlass:
                            doorProtoId = shipFaction.Faction == "govfor"
                                ? "CMAirlockSecurityGovforGlassLocked"
                                : shipFaction.Faction == "opfor"
                                    ? "CMAirlockSecurityOpforGlassLocked"
                                    : null;
                            break;
                        case PlatoonMarkerClass.LockedGlassDoor:
                            doorProtoId = shipFaction.Faction == "govfor"
                                ? "CMAirlockGovforGlassLocked"
                                : shipFaction.Faction == "opfor"
                                    ? "CMAirlockOpforGlassLocked"
                                    : null;
                            break;
                        case PlatoonMarkerClass.LockedCommandGlassDoor:
                            doorProtoId = shipFaction.Faction == "govfor"
                                ? "CMAirlockCommandGovforGlassLocked"
                                : shipFaction.Faction == "opfor"
                                    ? "CMAirlockCommandOpforGlassLocked"
                                    : null;
                            break;
                        case PlatoonMarkerClass.LockedEngineeringDoor:
                            doorProtoId = shipFaction.Faction == "govfor"
                                ? "CMAirlockEngineerGovforLocked"
                                : shipFaction.Faction == "opfor"
                                    ? "CMAirlockEngineerOpforLocked"
                                    : null;
                            break;
                        case PlatoonMarkerClass.LockedEngineeringGlassDoor:
                            doorProtoId = shipFaction.Faction == "govfor"
                                ? "CMAirlockEngineerGovforGlassLocked"
                                : shipFaction.Faction == "opfor"
                                    ? "CMAirlockEngineerOpforGlassLocked"
                                    : null;
                            break;
                        case PlatoonMarkerClass.LockedMedicalDoor:
                            doorProtoId = shipFaction.Faction == "govfor"
                                ? "CMAirlockMedicalGovforLocked"
                                : shipFaction.Faction == "opfor"
                                    ? "CMAirlockMedicalOpforLocked"
                                    : null;
                            break;
                        case PlatoonMarkerClass.LockedMedicalGlassDoor:
                            doorProtoId = shipFaction.Faction == "govfor"
                                ? "CMAirlockMedicalGovforGlassLocked"
                                : shipFaction.Faction == "opfor"
                                    ? "CMAirlockMedicalOpforGlassLocked"
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
                    if (shipPlatoon != null && shipPlatoon.VendorMarkersByClass.TryGetValue(markerComp.Class, out var vendorProtoId))
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
            if (!platoon.VendorMarkersByClass.TryGetValue(markerComp.Class, out var vendorProtoId))
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

        // --- DROPSHIP & FIGHTER CONSOLE SPAWNING LOGIC ---
        // Track destinations already handed out this round so multiple ships of the same
        // faction/type don't all pile onto the same LZ (e.g. both dropships at LZ1 on USSBush).
        var usedDestinations = new HashSet<EntityUid>();
        var destinationRandom = new Random();

        // Helper: Find a destination entity for a given faction and type, optionally filtering by grid.
        // Picks a random unused destination from the matching pool and marks it as used.
        EntityUid? FindDestination(string faction, DropshipDestinationComponent.DestinationType type, EntityUid? gridUid = null)
        {
            var candidates = new List<EntityUid>();
            var dropshipsDestQuery = AllEntityQuery<DropshipDestinationComponent>();
            while (dropshipsDestQuery.MoveNext(out var destUid, out var comp))
            {
                if (usedDestinations.Contains(destUid))
                    continue;
                if (comp.FactionController != faction || comp.Destinationtype != type)
                    continue;
                if (gridUid != null &&
                    _entityManager.GetComponent<TransformComponent>(destUid).GridUid != gridUid)
                    continue;
                candidates.Add(destUid);
            }

            if (candidates.Count == 0)
                return null;

            var picked = candidates[destinationRandom.Next(candidates.Count)];
            usedDestinations.Add(picked);
            return picked;
        }

        // Helper: For a given grid, find all marker UIDs of a given prototype ID
        List<EntityUid> FindMarkersOnGrid(EntityUid grid, string markerProtoId)
        {
            var result = new List<EntityUid>();
            var vendorMarkerQuery = AllEntityQuery<VendorMarkerComponent>();
            while (vendorMarkerQuery.MoveNext(out var entUid, out var comp))
            {
                if (_entityManager.GetComponent<TransformComponent>(entUid).GridUid == grid
                    && _entityManager.TryGetComponent<MetaDataComponent>(entUid, out var meta)
                    && meta.EntityPrototype != null
                    && meta.EntityPrototype.ID == markerProtoId)
                    result.Add(entUid);
            }
            return result;
        }

        void SetPhonesFactionOnGrid(EntityUid grid, string faction)
        {
            var query = AllEntityQuery<RotaryPhoneComponent>();
            while (query.MoveNext(out var phoneUid, out var phoneComp))
            {
                if (Transform(phoneUid).GridUid == grid)
                {
                    phoneComp.Faction = faction;
                    Dirty(phoneUid, phoneComp);
                }
            }
        }

        // New helper: offset ladder ids found on a grid by an integer offset (e.g., +100)
        void OffsetLaddersOnGrid(EntityUid grid, int offset)
        {
            // Iterate all ladder components and adjust those on the target grid
            var query = AllEntityQuery<LadderComponent>();
            while (query.MoveNext(out var ladderUid, out var ladderComp))
            // foreach (var ladderComp in _entityManager.EntityQuery<LadderComponent>(true))
            {
                if (Transform(ladderUid).GridUid != grid)
                    continue;
                if (ladderComp.Id == null)
                    continue;

                // Try to parse numeric id, otherwise skip
                if (int.TryParse(ladderComp.Id, out var numeric))
                {
                    ladderComp.Id = (numeric + offset).ToString();
                    Dirty(ladderUid, ladderComp);
                }
            }
        }

        // Helper: Set faction for all phones that are parented to a given entity (or share its grid)
        void SetPhonesFactionForParent(EntityUid parent, string faction)
        {
            if (!_entityManager.TryGetComponent<TransformComponent>(parent, out var parentTransform))
                return;

            var parentGrid = parentTransform.GridUid;
            var query = AllEntityQuery<RotaryPhoneComponent>();
            while (query.MoveNext(out var phoneUid, out var phoneComp))
            {
                if (Transform(phoneUid).ParentUid == parent || Transform(phoneUid).GridUid == parentGrid)
                {
                    phoneComp.Faction = faction;
                    Dirty(phoneUid, phoneComp);
                }
            }
        }

        // Helper: Find a navigation computer on a grid
        EntityUid? FindNavComputerOnGrid(EntityUid grid)
        {
            // foreach (var comp in _entityManager.EntityQuery<DropshipNavigationComputerComponent>(true))
            var query = AllEntityQuery<DropshipNavigationComputerComponent>();
            while (query.MoveNext(out var entUid, out var comp))
            {
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
                    // Ensure any existing rotary phones on this grid inherit the platoon faction
                    SetPhonesFactionOnGrid(grid, faction);

                    // Offset ladder IDs on opfor ships to avoid duplicate numeric IDs when the same ship/map
                    // is loaded multiple times (adds 100 to numeric ladder IDs, e.g. "2" -> "102").
                    if (faction == "opfor" && planetComp != null && planetComp.OpforInShip)
                    {
                        OffsetLaddersOnGrid(grid, 100);
                    }

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
                    // Ensure any existing rotary phones on this fighter grid inherit the platoon faction
                    SetPhonesFactionOnGrid(grid, faction);

                    // Offset ladder IDs on opfor ships (fighters) as well
                    if (faction == "opfor" && planetComp != null && planetComp.OpforInShip)
                    {
                        OffsetLaddersOnGrid(grid, 100);
                    }

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

    protected override void Ended(EntityUid uid, PlatoonSpawnRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        // Clear selections on rule end/restart so they don't persist across restarts
        SelectedGovforPlatoon = null;
        SelectedOpforPlatoon = null;
    }
}
