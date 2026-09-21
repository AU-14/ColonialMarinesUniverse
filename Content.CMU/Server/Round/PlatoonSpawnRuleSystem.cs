using System.Linq;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.Callsigns;
using Content.Server.CMU14.VendorMarker;
using Robust.Shared.Prototypes;
using Content.Server.GameTicking.Rules;
using Content.Server.Maps;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Round;
using Content.Shared.CMU14.util;
using Content.Shared.GameTicking.Components;
using Robust.Shared.EntitySerialization.Systems;
using Content.Server._RMC14.Requisitions;
using Content.Shared._RMC14.Telephone;
using Content.Shared._RMC14.SupplyDrop;
using Content.Shared._RMC14.Ladder;
using Content.Shared.CMU14;

namespace Content.Server.CMU14.Round;

public sealed partial class PlatoonSpawnRuleSystem : GameRuleSystem<PlatoonSpawnRuleComponent>
{
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private SharedDropshipSystem _sharedDropshipSystem = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;
    [Dependency] private SharedSupplyDropSystem _supplyDrop = default!;
    [Dependency] private MultiDeckDropshipSystem _multiDeck = default!;
    [Dependency] private SharedOverwatchConsoleSystem _overwatch = default!;
    [Dependency] private SharedTacticalMapSystem _tacticalMap = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

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

                    if (markerComp.Class == PlatoonMarkerClass.DropshipDestination)
                    {
                        string dropshipDestinationProtoId = "CMDropshipDestinationHome";
                        var dropshipEntity = _entityManager.SpawnAttachedTo(dropshipDestinationProtoId, transform.Coordinates, rotation: transform.LocalRotation);
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
                            // SpawnEntity has no rotation parameter, so spawn attached to keep the marker's rotation
                            var spawned = _entityManager.SpawnAttachedTo(vendorProto.ID, transform.Coordinates, rotation: transform.LocalRotation);
                            SetRequisitionsVendorAccess(spawned, markerComp.Class, shipFaction.Faction);
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

                }

                if (shipFaction.Faction == "opfor")
                    ConvertGovforEntitiesToOpfor(shipUid, shipTransform);
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

            // --- VENDOR MARKER LOGIC ---
            if (!TryResolvePlatoonVendor(platoon, markerComp.Class, out var vendorProtoId))
                continue;
            if (!_prototypeManager.TryIndex<EntityPrototype>(vendorProtoId, out var vendorProto))
                continue;
            var spawnedEnt = _entityManager.SpawnAttachedTo(vendorProto.ID, transform.Coordinates, rotation: transform.LocalRotation);
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
                var launched = TryFlyShuttleToDestination(
                    grid,
                    faction,
                    DropshipDestinationComponent.DestinationType.Dropship,
                    planetComp,
                    usedDestinations,
                    destinationRandom);
                if (!launched && HasComp<MultiDeckDropshipComponent>(grid))
                {
                    // A large ship must not consume a round slot when no home pad
                    // has enough clearance. Try another compatible design instead.
                    QueueDel(grid);
                    i--;
                }
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

        if (HasComp<MultiDeckDropshipComponent>(grid))
        {
            var shipFaction = EnsureComp<ShipFactionComponent>(grid);
            shipFaction.Faction = faction;
            Dirty(grid, shipFaction);
            var children = Transform(grid).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (TryComp<AU14CallsignConsoleComponent>(child, out var callsigns))
                {
                    callsigns.Faction = faction;
                    Dirty(child, callsigns);
                }
                if (TryComp<TacticalMapComputerComponent>(child, out var tactical))
                    _tacticalMap.SetComputerFaction((child, tactical), faction);
                if (TryComp<OverwatchConsoleComponent>(child, out var overwatch))
                    _overwatch.SetGroup((child, overwatch), faction.ToUpperInvariant());
                if (faction == "opfor" && TryComp<AccessReaderComponent>(child, out var access))
                {
                    var groups = access.AccessLists.Select(group => group.Select(id =>
                        new ProtoId<AccessLevelPrototype>(id.Id.Replace("AU14AccessGovfor", "AU14AccessOpfor"))).ToHashSet()).ToList();
                    _accessReader.TrySetAccesses((child, access), groups);
                }
                if (!HasComp<DropshipNavigationComputerComponent>(child) &&
                    !HasComp<DropshipTerminalWeaponsComponent>(child))
                    continue;
                var whitelist = EnsureComp<WhitelistedShuttleComponent>(child);
                whitelist.Faction = faction;
                whitelist.ShuttleType = DropshipDestinationComponent.DestinationType.Dropship;
                Dirty(child, whitelist);
            }
        }

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

    private bool TryFlyShuttleToDestination(
        EntityUid grid,
        string faction,
        DropshipDestinationComponent.DestinationType type,
        RMCPlanetMapPrototypeComponent planetComp,
        HashSet<EntityUid> usedDestinations,
        Random destinationRandom)
    {
        EntityUid? destination = null;
        if (HasComp<MultiDeckDropshipComponent>(grid))
            destination = FindMultiDeckDestination(grid, faction, planetComp, usedDestinations, destinationRandom);
        else if (UsesShipDestination(planetComp, faction))
            destination = FindDestination(faction, type, usedDestinations, destinationRandom, grid);

        if (!HasComp<MultiDeckDropshipComponent>(grid))
            destination ??= FindDestination(faction, type, usedDestinations, destinationRandom);

        var navComputer = FindNavComputerOnGrid(grid);
        if (destination == null || navComputer == null)
            return false;

        var navComp = _entityManager.GetComponent<DropshipNavigationComputerComponent>(navComputer.Value);
        var navEntity = new Entity<DropshipNavigationComputerComponent>(navComputer.Value, navComp);
        if (!_sharedDropshipSystem.FlyTo(navEntity, destination.Value, null))
            return false;
        usedDestinations.Add(destination.Value);
        return true;
    }

    private EntityUid? FindMultiDeckDestination(EntityUid dropship, string faction,
        RMCPlanetMapPrototypeComponent planet, HashSet<EntityUid> used, Random random)
    {
        var candidates = new List<EntityUid>();
        var destinations = AllEntityQuery<DropshipDestinationComponent, TransformComponent>();
        while (destinations.MoveNext(out var uid, out var destination, out var transform))
        {
            if (used.Contains(uid) || destination.Ship != null || destination.FactionController != faction ||
                destination.Destinationtype != DropshipDestinationComponent.DestinationType.Dropship)
                continue;

            if (UsesShipDestination(planet, faction))
            {
                var atHome = false;
                var carriers = AllEntityQuery<ShipFactionComponent, TransformComponent>();
                while (carriers.MoveNext(out var carrier, out var owner, out var carrierTransform))
                {
                    if (owner.Faction == faction && !HasComp<DropshipComponent>(carrier) &&
                        IsMarkerOnShipOrZLevel(carrier, carrierTransform, transform))
                    {
                        atHome = true;
                        break;
                    }
                }
                if (!atHome)
                    continue;
            }

            if (_multiDeck.IsLandingClear(dropship, transform.Coordinates, _transform.GetWorldRotation(uid)))
                candidates.Add(uid);
        }
        return candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
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
        var console = _entityManager.SpawnAttachedTo(protoId, transform.Coordinates, rotation: transform.LocalRotation);
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

    private static readonly ProtoId<FactionSwapSetPrototype> OpforShipSwaps = "OpforShipSwaps";

    private void ConvertGovforEntitiesToOpfor(EntityUid shipUid, TransformComponent shipTransform)
    {
        if (!_prototypeManager.TryIndex(OpforShipSwaps, out FactionSwapSetPrototype? swapSet))
            return;

        var swaps = swapSet.Swaps;
        // Covers markers spawned above and entities baked into the map; swapping (not editing
        // components) so MapInit-derived state such as alliance controllable factions is correct.
        // Membership uses the ship's whole z-network: decks load as separate grids and a bare
        // GridUid check silently skips every deck but the one that got the faction tag.
        var toSwap = new List<(EntityUid uid, EntProtoId opforProtoId, TransformComponent transform)>();
        var supplyDrops = new List<EntityUid>();
        var query = AllEntityQuery<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var meta, out var transform))
        {
            if (!IsMarkerOnShipOrZLevel(shipUid, shipTransform, transform) || meta.EntityPrototype is not { } proto)
                continue;

            if (swaps.TryGetValue(proto.ID, out var opforProtoId))
                toSwap.Add((uid, opforProtoId, transform));
            else if (proto.ID == "RMCSupplyDropConsole")
                supplyDrops.Add(uid);
        }

        foreach (var (uid, opforProtoId, transform) in toSwap)
        {
            // A renamed target must leave the Govfor entity in place, not delete it unreplaced
            if (!_prototypeManager.TryIndex<EntityPrototype>(opforProtoId, out _))
                continue;

            _entityManager.SpawnAttachedTo(opforProtoId, transform.Coordinates, rotation: transform.LocalRotation);
            _entityManager.DeleteEntity(uid);
        }

        // No Opfor supply drop console prototype exists; retarget the marine squad binding so
        // launches resolve the ship pads via SquadOpfor's supplyDropPadSquad.
        foreach (var uid in supplyDrops)
            _supplyDrop.SetSquad(uid, "SquadOpfor");
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

    private void SetRequisitionsVendorAccess(EntityUid vendor, PlatoonMarkerClass markerClass, string faction)
    {
        if (markerClass != PlatoonMarkerClass.ReqVend ||
            !TryComp<AccessReaderComponent>(vendor, out var accessReader))
        {
            return;
        }

        ProtoId<AccessLevelPrototype>? access = faction switch
        {
            "govfor" => "AU14AccessGovforReq",
            "opfor" => "AU14AccessOpforReq",
            _ => null,
        };

        if (access is { } factionAccess)
            _accessReader.TrySetAccesses((vendor, accessReader), new List<ProtoId<AccessLevelPrototype>> { factionAccess });
    }

    protected override void Ended(EntityUid uid, PlatoonSpawnRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        // Clear selections on rule end/restart so they don't persist across restarts
        SelectedGovforPlatoon = null;
        SelectedOpforPlatoon = null;
    }
}
