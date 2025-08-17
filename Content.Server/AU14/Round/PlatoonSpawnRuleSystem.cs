using System.Linq;
using Content.Server.AU14.VendorMarker;
using Robust.Shared.Prototypes;
using Content.Server.GameTicking.Rules;
using Content.Server.Maps;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.util;
using Content.Shared.GameTicking.Components;

namespace Content.Server.AU14.Round;

public sealed class PlatoonSpawnRuleSystem : GameRuleSystem<PlatoonSpawnRuleComponent>
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly AuRoundSystem _auRoundSystem = default!;
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
        if (planetComp != null && (planetComp.GovforInShip || planetComp.OpforInShip))
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
                        if (_prototypeManager.TryIndex<EntityPrototype>(doorProtoId, out var doorProto))
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

            if (!platoon.VendorMarkersByClass.TryGetValue(markerClass, out var vendorProtoId))
                continue;

            if (!_prototypeManager.TryIndex<EntityPrototype>(vendorProtoId, out var vendorProto))
                continue;

            _entityManager.SpawnEntity(vendorProto.ID, transform.Coordinates);
        }
    }
}
