using System.Linq;
using Content.Server._CMU14.ZLevels.Core;
using Content.Shared._CMU14.RoundSetup.LegacyBush;
using Robust.Shared.Prototypes;
using Content.Server.GameTicking.Rules;
using Content.Server.Maps;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.Round;
using Content.Shared.AU14.util;
using Content.Shared.GameTicking.Components;
using Robust.Shared.EntitySerialization.Systems;
using Content.Server._RMC14.Requisitions;
using Content.Shared._RMC14.Telephone;
using Content.Shared._RMC14.Ladder;
using Content.Shared.AU14;
using Robust.Shared.Profiling;
using Robust.Shared.Random;

namespace Content.Server.AU14.Round;

public sealed partial class PlatoonSpawnRuleSystem : GameRuleSystem<PlatoonSpawnRuleComponent>
{
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private ProfManager _prof = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedDropshipSystem _sharedDropshipSystem = default!;
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

        var preset = _auRoundSystem.SelectedPreset;
        var activeFactions = preset == null
            ? AuRoundVoteBranch.Govfor | AuRoundVoteBranch.Opfor
            : AuRoundSelectionRules.GetActiveFactionBranches(
                preset.RequiresGovforVote,
                preset.RequiresOpforVote,
                preset.UsesGovforPlatoon,
                preset.UsesOpforPlatoon);

        // Fallback only for factions enabled by the finalized preset.
        if ((activeFactions & AuRoundVoteBranch.Govfor) != 0 &&
            govPlatoon == null &&
            !string.IsNullOrEmpty(planetComp.DefaultGovforPlatoon))
        {
            govPlatoon = _prototypeManager.Index<PlatoonPrototype>(planetComp.DefaultGovforPlatoon);
        }

        if ((activeFactions & AuRoundVoteBranch.Opfor) != 0 &&
            opPlatoon == null &&
            !string.IsNullOrEmpty(planetComp.DefaultOpforPlatoon))
        {
            opPlatoon = _prototypeManager.Index<PlatoonPrototype>(planetComp.DefaultOpforPlatoon);
        }

        // Store the resolved selections back onto the system so other systems can access them
        SelectedGovforPlatoon = govPlatoon;
        SelectedOpforPlatoon = opPlatoon;

        var includesShipSetup = planetComp.GovforInShip || planetComp.OpforInShip;
        var initialInventory = CaptureInitialSetupInventory(includesShipSetup);

        // --- SHIP VENDOR MARKER LOGIC ---
        if (includesShipSetup)
        {
            var usedShipMarkers = new HashSet<EntityUid>();
            foreach (var shipUid in initialInventory.Ships)
            {
                if (!TryComp(shipUid, out ShipFactionComponent? shipFaction) ||
                    !TryComp(shipUid, out TransformComponent? shipTransform))
                {
                    continue;
                }

                // Ensure any existing rotary phones that belong to this ship inherit the ship faction
                if (!string.IsNullOrEmpty(shipFaction.Faction))
                    SetPhonesFactionForParent(initialInventory, shipUid, shipTransform, shipFaction.Faction);

                PlatoonPrototype? shipPlatoon = null;
                if (shipFaction.Faction == "govfor" && planetComp.GovforInShip && govPlatoon != null)
                    shipPlatoon = govPlatoon;
                else if (shipFaction.Faction == "opfor" && planetComp.OpforInShip && opPlatoon != null)
                    shipPlatoon = opPlatoon;
                else
                    continue;

                var shipMarkers = initialInventory.GetShipMarkers(shipUid);
                for (var i = 0; i < shipMarkers.Count; i++)
                {
                    var markerUid = shipMarkers[i];
                    if (!TryComp(markerUid, out VendorMarkerComponent? markerComp) ||
                        !TryComp(markerUid, out TransformComponent? transform) ||
                        !markerComp.Ship ||
                        !IsMarkerOnShipOrZLevel(shipUid, shipTransform, transform) ||
                        !usedShipMarkers.Add(markerUid))
                    {
                        continue;
                    }

                    var doorProtoId = GetLockedDoorPrototype(markerComp.Class, shipFaction.Faction);

                    if (doorProtoId != null)
                    {
                        if (_prototypeManager.TryIndex(doorProtoId, out _))
                            _entityManager.SpawnAttachedTo(doorProtoId, transform.Coordinates, rotation: transform.LocalRotation);
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

                    if (markerComp.Class == PlatoonMarkerClass.RosterConsole)
                    {
                        string? rosterConsoleProtoId = null;
                        if (markerComp.Govfor)
                            rosterConsoleProtoId = "CMUGovforRosterConsole";
                        else if (markerComp.Opfor)
                            rosterConsoleProtoId = "CMUOpforRosterConsole";
                        else if (markerComp.Ship)
                        {
                            var parentUid = transform.ParentUid;
                            if (_entityManager.TryGetComponent<ShipFactionComponent>(parentUid, out var parentShipFaction))
                            {
                                rosterConsoleProtoId = parentShipFaction.Faction == "govfor"
                                    ? "CMUGovforRosterConsole"
                                    : parentShipFaction.Faction == "opfor"
                                        ? "CMUOpforRosterConsole"
                                        : null;
                            }
                        }
                        if (rosterConsoleProtoId != null && _prototypeManager.TryIndex(rosterConsoleProtoId, out _))
                        {
                            _entityManager.SpawnEntity(rosterConsoleProtoId, transform.Coordinates);
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

        // Process the planet-wide view in the original vendor component-query order.
        var usedMarkers = new HashSet<EntityUid>();
        foreach (var markerUid in initialInventory.VendorMarkers)
        {
            if (!TryComp(markerUid, out VendorMarkerComponent? markerComp) ||
                !TryComp(markerUid, out TransformComponent? transform))
            {
                continue;
            }

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

    internal PlatoonInitialSetupInventory CaptureInitialSetupInventory(bool includeShipSetup)
    {
        using var profile = _prof.Group("CMU Platoon Initial Setup Inventory");
        var inventory = new PlatoonInitialSetupInventory();
        var networkByMap = new Dictionary<EntityUid, EntityUid?>();

        if (includeShipSetup)
        {
            var shipQuery = AllEntityQuery<ShipFactionComponent>();
            while (shipQuery.MoveNext(out var ship, out _))
            {
                if (!TryComp(ship, out TransformComponent? transform))
                    continue;

                inventory.AddShip(
                    ship,
                    transform.GridUid,
                    transform.MapUid,
                    ResolveZNetworkOwner(transform.MapUid, networkByMap));
            }
        }

        var markerQuery = AllEntityQuery<VendorMarkerComponent>();
        while (markerQuery.MoveNext(out var marker, out _))
        {
            if (!TryComp(marker, out TransformComponent? transform))
                continue;

            inventory.AddVendorMarker(
                marker,
                transform.ParentUid,
                transform.GridUid,
                transform.MapUid,
                includeShipSetup
                    ? ResolveZNetworkOwner(transform.MapUid, networkByMap)
                    : null);
        }

        if (includeShipSetup)
        {
            var phoneQuery = AllEntityQuery<RotaryPhoneComponent>();
            while (phoneQuery.MoveNext(out var phone, out _))
            {
                if (!TryComp(phone, out TransformComponent? transform))
                    continue;

                inventory.AddPhone(phone, transform.ParentUid, transform.GridUid);
            }
        }

        if (_prof.IsEnabled)
        {
            _prof.WriteValue("CMU Platoon Initial Setup Ships", inventory.Ships.Count);
            _prof.WriteValue("CMU Platoon Initial Setup Vendor Markers", inventory.VendorMarkers.Count);
            _prof.WriteValue("CMU Platoon Initial Setup Phones", inventory.IndexedPhones);
            _prof.WriteValue("CMU Platoon Initial Setup Ship Marker Assignments", inventory.ShipMarkerAssignments);
            _prof.WriteValue("CMU Platoon Initial Setup Ship Phone Assignments", inventory.ShipPhoneAssignments);
        }

        return inventory;
    }

    private EntityUid? ResolveZNetworkOwner(
        EntityUid? map,
        Dictionary<EntityUid, EntityUid?> networkByMap)
    {
        if (map is not { } mapUid)
            return null;

        if (networkByMap.TryGetValue(mapUid, out var cached))
            return cached;

        EntityUid? network = null;
        if (_zLevels.TryGetZNetwork(mapUid, out var resolved))
            network = resolved.Value.Owner;

        networkByMap.Add(mapUid, network);
        return network;
    }

    private void HandlePlatoonShuttleSpawns(
        RMCPlanetMapPrototypeComponent planetComp,
        PlatoonPrototype? govPlatoon,
        PlatoonPrototype? opPlatoon)
    {
        if (govPlatoon == null && opPlatoon == null)
            return;

        using var profile = _prof.Group("CMU Platoon Shuttle Setup");
        var destinations = CaptureDestinationPool();
        var indexedEntities = 0;
        var indexedGrids = 0;

        LoadPlatoonShuttles(
            planetComp,
            govPlatoon,
            "govfor",
            planetComp.govfordropships,
            planetComp.govforfighters,
            destinations,
            ref indexedGrids,
            ref indexedEntities);

        LoadPlatoonShuttles(
            planetComp,
            opPlatoon,
            "opfor",
            planetComp.opfordropships,
            planetComp.opforfighters,
            destinations,
            ref indexedGrids,
            ref indexedEntities);

        if (_prof.IsEnabled)
        {
            _prof.WriteValue("CMU Platoon Shuttle Setup Destinations", destinations.Count);
            _prof.WriteValue("CMU Platoon Shuttle Setup Grids", indexedGrids);
            _prof.WriteValue("CMU Platoon Shuttle Setup Indexed Entities", indexedEntities);
        }
    }

    private void LoadPlatoonShuttles(
        RMCPlanetMapPrototypeComponent planetComp,
        PlatoonPrototype? platoon,
        string faction,
        int dropshipCount,
        int fighterCount,
        PlatoonDestinationPool destinations,
        ref int indexedGrids,
        ref int indexedEntities)
    {
        if (platoon == null)
            return;

        var dropships = platoon.CompatibleDropships.ToList();
        for (var i = 0; i < dropshipCount && dropships.Count > 0; i++)
        {
            var index = _random.Next(dropships.Count);
            var mapId = dropships[index];
            dropships.RemoveAt(index);

            if (!_mapLoader.TryLoadMap(mapId, out var loadedMap, out var grids))
                continue;

            var indexedDestinations = false;
            foreach (var grid in grids)
            {
                var gridMapId = _entityManager.GetComponent<TransformComponent>(grid).MapID;
                _mapSystem.InitializeMap(gridMapId);

                if (!indexedDestinations)
                {
                    AddDestinationsInScope(loadedMap.Value.Owner, destinations);
                    indexedDestinations = true;
                }

                var inventory = CaptureGridSetupInventory(grid);
                indexedGrids++;
                indexedEntities += inventory.IndexedEntities;
                PrepareLoadedShuttleGrid(inventory, faction, planetComp);
                SpawnShuttleConsoleMarkers(
                    inventory,
                    faction,
                    DropshipDestinationComponent.DestinationType.Dropship,
                    "dropshipshuttlevmarker");
                TryFlyShuttleToDestination(
                    inventory,
                    faction,
                    DropshipDestinationComponent.DestinationType.Dropship,
                    planetComp,
                    destinations);
            }
        }

        var fighters = platoon.CompatibleFighters.ToList();
        for (var i = 0; i < fighterCount && fighters.Count > 0; i++)
        {
            var index = _random.Next(fighters.Count);
            var fighterMap = fighters[index];
            fighters.RemoveAt(index);

            if (!_mapLoader.TryLoadGrid(fighterMap, out var loadedMap, out var grid))
                continue;

            AddDestinationsInScope(loadedMap.Value.Owner, destinations);
            var inventory = CaptureGridSetupInventory(grid.Value);
            indexedGrids++;
            indexedEntities += inventory.IndexedEntities;
            PrepareLoadedShuttleGrid(inventory, faction, planetComp);
            SpawnShuttleConsoleMarkers(
                inventory,
                faction,
                DropshipDestinationComponent.DestinationType.Figher,
                "dropshipfighterdestmarker");
            TryFlyShuttleToDestination(
                inventory,
                faction,
                DropshipDestinationComponent.DestinationType.Figher,
                planetComp,
                destinations);
        }
    }

    private void PrepareLoadedShuttleGrid(
        PlatoonGridSetupInventory inventory,
        string faction,
        RMCPlanetMapPrototypeComponent planetComp)
    {
        SetPhonesFactionOnGrid(inventory, faction);

        if (faction == "opfor" && planetComp.OpforInShip)
            OffsetLaddersOnGrid(inventory, 100);
    }

    private void SpawnShuttleConsoleMarkers(
        PlatoonGridSetupInventory inventory,
        string faction,
        DropshipDestinationComponent.DestinationType type,
        string navigationMarkerProtoId)
    {
        var navigationMarkers = inventory.GetMarkers(navigationMarkerProtoId);
        if (navigationMarkers.Count > 0)
        {
            var navigationProto = faction == "govfor"
                ? "CMComputerDropshipNavigationGovfor"
                : "CMComputerDropshipNavigationOpfor";
            for (var i = 0; i < navigationMarkers.Count; i++)
                SpawnWeaponsConsole(navigationProto, navigationMarkers[i], faction, type, inventory);
        }

        var weaponsMarkers = inventory.GetMarkers("dropshipweaponsvmarker");
        if (weaponsMarkers.Count == 0)
            return;

        var weaponsProto = faction == "govfor"
            ? "CMComputerDropshipWeaponsGovfor"
            : "CMComputerDropshipWeaponsOpfor";
        for (var i = 0; i < weaponsMarkers.Count; i++)
            SpawnWeaponsConsole(weaponsProto, weaponsMarkers[i], faction, type, inventory);
    }

    private void TryFlyShuttleToDestination(
        PlatoonGridSetupInventory inventory,
        string faction,
        DropshipDestinationComponent.DestinationType type,
        RMCPlanetMapPrototypeComponent planetComp,
        PlatoonDestinationPool destinations)
    {
        EntityUid? destination = null;
        if (UsesShipDestination(planetComp, faction))
            destination = FindDestination(faction, type, destinations, inventory.Grid);

        destination ??= FindDestination(faction, type, destinations);

        var navComputer = FindNavComputerOnGrid(inventory);
        if (destination == null || navComputer == null)
            return;

        var navComp = _entityManager.GetComponent<DropshipNavigationComputerComponent>(navComputer.Value);
        var navEntity = new Entity<DropshipNavigationComputerComponent>(navComputer.Value, navComp);
        _sharedDropshipSystem.FlyTo(navEntity, destination.Value, null);
    }

    internal PlatoonDestinationPool CaptureDestinationPool()
    {
        var destinations = new PlatoonDestinationPool();
        var query = AllEntityQuery<DropshipDestinationComponent>();
        while (query.MoveNext(out var destination, out _))
        {
            destinations.Add(destination);
        }

        return destinations;
    }

    private void AddDestinationsInScope(EntityUid root, PlatoonDestinationPool destinations)
    {
        var pending = new List<EntityUid> { root };
        while (pending.Count > 0)
        {
            var index = pending.Count - 1;
            var current = pending[index];
            pending.RemoveAt(index);

            if (!TryComp(current, out TransformComponent? transform))
                continue;

            if (HasComp<DropshipDestinationComponent>(current))
                destinations.Add(current);

            var children = transform.ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                pending.Add(child);
            }
        }
    }

    internal PlatoonGridSetupInventory CaptureGridSetupInventory(EntityUid grid)
    {
        var inventory = new PlatoonGridSetupInventory(grid);
        var pending = new List<EntityUid> { grid };
        while (pending.Count > 0)
        {
            var index = pending.Count - 1;
            var current = pending[index];
            pending.RemoveAt(index);

            if (!TryComp(current, out TransformComponent? transform))
                continue;

            if (current != grid && transform.GridUid != grid)
                continue;

            inventory.RecordEntity();
            if (HasComp<LadderComponent>(current))
                inventory.Ladders.Add(current);
            if (HasComp<DropshipNavigationComputerComponent>(current))
                inventory.NavigationComputers.Add(current);
            if (HasComp<RotaryPhoneComponent>(current))
                inventory.Phones.Add(current);

            if (HasComp<VendorMarkerComponent>(current) &&
                TryComp(current, out MetaDataComponent? metadata) &&
                metadata.EntityPrototype is { } prototype)
            {
                inventory.AddMarker(prototype.ID, current);
            }

            var children = transform.ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                pending.Add(child);
            }
        }

        return inventory;
    }

    internal EntityUid? FindDestination(
        string faction,
        DropshipDestinationComponent.DestinationType type,
        PlatoonDestinationPool destinations,
        EntityUid? gridUid = null)
    {
        var candidates = destinations.Candidates;
        candidates.Clear();
        foreach (var destination in destinations.Destinations)
        {
            if (destinations.IsUsed(destination) ||
                !TryComp(destination, out DropshipDestinationComponent? component))
            {
                continue;
            }

            if (component.FactionController != faction || component.Destinationtype != type)
                continue;

            if (gridUid != null &&
                (!TryComp(destination, out TransformComponent? transform) || transform.GridUid != gridUid))
            {
                continue;
            }

            candidates.Add(destination);
        }

        if (candidates.Count == 0)
            return null;

        // The old System.Random streams were not tied to the round seed, so there is no
        // round-seed-compatible destination sequence to preserve. Candidate ordering is
        // intentionally not a contract; both ECS component queries and transform children
        // have implementation-defined order, while every eligible destination remains uniform.
        var picked = candidates[_random.Next(candidates.Count)];
        destinations.MarkUsed(picked);
        return picked;
    }

    internal EntityUid? FindNavComputerOnGrid(PlatoonGridSetupInventory inventory)
    {
        foreach (var navigationComputer in inventory.NavigationComputers)
        {
            if (TryComp(navigationComputer, out DropshipNavigationComputerComponent? _) &&
                TryComp(navigationComputer, out TransformComponent? transform) &&
                transform.GridUid == inventory.Grid)
            {
                return navigationComputer;
            }
        }

        return null;
    }

    private void SpawnWeaponsConsole(
        string protoId,
        EntityUid markerUid,
        string faction,
        DropshipDestinationComponent.DestinationType type,
        PlatoonGridSetupInventory inventory)
    {
        if (!TryComp(markerUid, out VendorMarkerComponent? _) ||
            !TryComp(markerUid, out TransformComponent? transform) ||
            transform.GridUid != inventory.Grid)
        {
            return;
        }

        var console = _entityManager.SpawnEntity(protoId, transform.Coordinates);
        if (!_entityManager.HasComponent<WhitelistedShuttleComponent>(console))
            _entityManager.AddComponent<WhitelistedShuttleComponent>(console);

        var whitelist = _entityManager.GetComponent<WhitelistedShuttleComponent>(console);
        whitelist.Faction = faction;
        whitelist.ShuttleType = type;

        if (HasComp<DropshipNavigationComputerComponent>(console))
            inventory.NavigationComputers.Add(console);
    }

    private void SetPhonesFactionOnGrid(PlatoonGridSetupInventory inventory, string faction)
    {
        foreach (var phone in inventory.Phones)
        {
            if (!TryComp(phone, out RotaryPhoneComponent? phoneComponent) ||
                !TryComp(phone, out TransformComponent? transform) ||
                transform.GridUid != inventory.Grid)
            {
                continue;
            }

            phoneComponent.Faction = faction;
            Dirty(phone, phoneComponent);
        }
    }

    private void SetPhonesFactionForParent(
        PlatoonInitialSetupInventory inventory,
        EntityUid parent,
        TransformComponent parentTransform,
        string faction)
    {
        var parentGrid = parentTransform.GridUid;
        var phones = inventory.GetShipPhones(parent);
        for (var i = 0; i < phones.Count; i++)
        {
            var phone = phones[i];
            if (!TryComp(phone, out RotaryPhoneComponent? phoneComponent) ||
                !TryComp(phone, out TransformComponent? transform) ||
                transform.ParentUid != parent && transform.GridUid != parentGrid)
            {
                continue;
            }

            phoneComponent.Faction = faction;
            Dirty(phone, phoneComponent);
        }
    }

    private void OffsetLaddersOnGrid(PlatoonGridSetupInventory inventory, int offset)
    {
        foreach (var ladder in inventory.Ladders)
        {
            if (!TryComp(ladder, out LadderComponent? ladderComponent) ||
                !TryComp(ladder, out TransformComponent? transform) ||
                transform.GridUid != inventory.Grid ||
                ladderComponent.Id == null ||
                !int.TryParse(ladderComponent.Id, out var numeric))
            {
                continue;
            }

            ladderComponent.Id = (numeric + offset).ToString();
            Dirty(ladder, ladderComponent);
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
            PlatoonMarkerClass.LockedDoubleNormalDoor => FactionPrototype(
                faction,
                "CMDoubleDoorGovforLocked",
                "CMDoubleDoorOpforLocked"),
            PlatoonMarkerClass.LockedDoubleGlassDoor => FactionPrototype(
                faction,
                "CMDoubleDoorGovforGlassLocked",
                "CMDoubleDoorOpforGlassLocked"),
            PlatoonMarkerClass.LockedDoubleCommandDoor => FactionPrototype(
                faction,
                "CMDoubleDoorCommandGovforLocked",
                "CMDoubleDoorCommandOpforLocked"),
            PlatoonMarkerClass.LockedDoubleCommandGlassDoor => FactionPrototype(
                faction,
                "CMDoubleDoorCommandGovforGlassLocked",
                "CMDoubleDoorCommandOpforGlassLocked"),
            PlatoonMarkerClass.LockedDoubleSecurityDoor => FactionPrototype(
                faction,
                "CMDoubleDoorSecurityGovforLocked",
                "CMDoubleDoorSecurityOpforLocked"),
            PlatoonMarkerClass.LockedDoubleSecurityGlassDoor => FactionPrototype(
                faction,
                "CMDoubleDoorSecurityGovforGlassLocked",
                "CMDoubleDoorSecurityOpforGlassLocked"),
            PlatoonMarkerClass.LockedDoubleMedicalDoor => FactionPrototype(
                faction,
                "CMDoubleDoorMedicalGovforLocked",
                "CMDoubleDoorMedicalOpforLocked"),
            PlatoonMarkerClass.LockedDoubleMedicalGlassDoor => FactionPrototype(
                faction,
                "CMDoubleDoorMedicalGovforGlassLocked",
                "CMDoubleDoorMedicalOpforGlassLocked"),
            PlatoonMarkerClass.LockedDoubleEngineeringDoor => FactionPrototype(
                faction,
                "CMDoubleDoorEngineerGovforLocked",
                "CMDoubleDoorEngineerOpforLocked"),
            PlatoonMarkerClass.LockedDoubleEngineeringGlassDoor => FactionPrototype(
                faction,
                "CMDoubleDoorEngineerGovforGlassLocked",
                "CMDoubleDoorEngineerOpforGlassLocked"),
            PlatoonMarkerClass.LockedLogisticsDoor => FactionPrototype(
                faction,
                "CMAirlockLogisticsGovforLocked",
                "CMAirlockLogisticsOpforLocked"),
            PlatoonMarkerClass.LockedLogisticsGlassDoor => FactionPrototype(
                faction,
                "CMAirlockLogisticsGovforGlassLocked",
                "CMAirlockLogisticsOpforGlassLocked"),
            PlatoonMarkerClass.LockedDoubleLogisticsDoor => FactionPrototype(
                faction,
                "CMDoubleDoorLogisticsGovforLocked",
                "CMDoubleDoorLogisticsOpforLocked"),
            PlatoonMarkerClass.LockedDoubleLogisticsGlassDoor => FactionPrototype(
                faction,
                "CMDoubleDoorLogisticsGovforGlassLocked",
                "CMDoubleDoorLogisticsOpforGlassLocked"),
            PlatoonMarkerClass.LaptopCallsign => FactionPrototype(
                faction,
                "AU14ItemLaptopCallsignGOVFOR",
                "AU14ItemLaptopCallsignOPFOR"),
            PlatoonMarkerClass.CommsArrayShip => FactionPrototype(
                faction,
                "AU14CommsArrayShipGovfor",
                "AU14CommsArrayShipOpfor"),
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
