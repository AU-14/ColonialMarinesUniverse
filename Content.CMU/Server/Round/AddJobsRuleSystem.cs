using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Server.Station.Components;
using Content.Shared.AU14.util;
using Content.Shared.GameTicking.Components;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Server.AU14.Round;

[UsedImplicitly]
public sealed class AddJobsRuleSystem : GameRuleSystem<AddJobsRuleComponent>
{
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly AuRoundSystem _auRoundSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly PlatoonSpawnRuleSystem _platoonSpawnRule = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    protected override void Started(EntityUid uid, AddJobsRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        PlatoonPrototype? platoon = null;
        var planet = _auRoundSystem.GetSelectedPlanet();
        var protoMgr = IoCManager.Resolve<IPrototypeManager>();
        var platoonSpawnRule = _platoonSpawnRule;
        // Determine which side's platoon to use
        if (component.ShipFaction != null && component.ShipFaction.ToLower() == "opfor")
        {
            platoon = platoonSpawnRule.SelectedOpforPlatoon;
        }
        else
        {
            // Prefer the selected govfor platoon if available
            platoon = platoonSpawnRule.SelectedGovforPlatoon;
            if (platoon == null && planet != null && planet.PlatoonsGovfor.Count > 0)
            {
                if (protoMgr.TryIndex<PlatoonPrototype>(planet.PlatoonsGovfor[0], out var foundPlatoon))
                    platoon = foundPlatoon;
            }
        }

        // If the platoon has a jobSlotOverride, use ONLY those jobs and skip all other job logic
        if (platoon != null && platoon.JobSlotOverride.Count > 0)
        {
            var jobsToAdd = new Dictionary<ProtoId<JobPrototype>, int>();
            var team = (component.ShipFaction != null && component.ShipFaction.ToLower() == "opfor") ? "Opfor" : "GOVFOR";
            foreach (var (jobClass, slotCount) in platoon.JobSlotOverride)
            {
                var jobId = $"AU14Job{team}{jobClass}";
                if (protoMgr.TryIndex<JobPrototype>(jobId, out var proto))
                    jobsToAdd[proto.ID] = slotCount;
                else
                    Logger.Warning($"[AddJobsRuleSystem] Could not find job prototype: {jobId}");
            }
            component.Jobs = jobsToAdd;
        }

        // If there are no jobs to add, return early
        if (component.Jobs == null || component.Jobs.Count == 0)
            return;


        if (planet != null && !string.IsNullOrEmpty(component.ShipFaction))
        {
            var faction = component.ShipFaction.ToLower();
            var addToShip = false;
            var addToPlanet = false;

            if (faction == "govfor")
            {
                addToShip = planet.GovforInShip;
                addToPlanet = !planet.GovforInShip;
            }
            else if (faction == "opfor")
            {
                addToShip = planet.OpforInShip;
                addToPlanet = !planet.OpforInShip;
            }

            if (addToShip && component.AddToShip)
            {
                // Find the ship entity with ShipFactionComponent matching the faction
                foreach (var (shipUid, shipFaction) in EntityManager.EntityQuery<ShipFactionComponent>(true).Select(s => (s.Owner, s)))
                {
                    if (string.IsNullOrEmpty(shipFaction.Faction) || shipFaction.Faction.ToLower() != faction)
                        continue;
                    // Find the station entity that owns this ship
                    var stationUid = _stationSystem.GetOwningStation(shipUid);
                    if (stationUid == null || !EntityManager.EntityExists(stationUid.Value))
                        continue;
                    var stationJobs = EntityManager.GetComponentOrNull<StationJobsComponent>(stationUid.Value);
                    if (stationJobs == null)
                        continue;
                    foreach (var entry in component.Jobs)
                    {
                        var jobId = entry.Key;
                        var amount = entry.Value;
                        _stationJobs.TryAdjustJobSlot(stationUid.Value, jobId.ToString(), amount, true, false, stationJobs);
                    }
                    // Only add to the first matching ship's station
                    break;
                }
            }

            if (addToPlanet)
            {
                // Get the main map id for the round
                var mapId = _gameTicker.DefaultMap;
                // Use StationSystem to get the correct station entity for the map
                var stationUid = _stationSystem.GetStationInMap(mapId);
                if (stationUid != null && EntityManager.EntityExists(stationUid.Value))
                {
                    var stationJobs = EntityManager.GetComponentOrNull<StationJobsComponent>(stationUid.Value);
                    if (stationJobs != null)
                    {
                        foreach (var entry in component.Jobs)
                        {
                            var jobId = entry.Key;
                            var amount = entry.Value;
                            _stationJobs.TryAdjustJobSlot(stationUid.Value, jobId.ToString(), amount, true, false, stationJobs);
                        }
                    }
                }
            }
            return;
        }

        if (planet != null)
        {
            var addToPlanet = true;
            // Check if we should add to the planet instead of the ship
            if (component.ShipFaction != null && component.ShipFaction.ToLower() == "opfor")
            {
                // Opfor always adds to the ship
                addToPlanet = false;
            }
            else if (component.ShipFaction != null && component.ShipFaction.ToLower() == "govfor")
            {
                // Govfor adds to the planet only if the planet is not set to spawn in the ship
                addToPlanet = !planet.GovforInShip;
            }

            if (addToPlanet)
            {
                // Get the main map id for the round
                var mapId = _gameTicker.DefaultMap;
                // Use StationSystem to get the correct station entity for the map
                var stationUid = _stationSystem.GetStationInMap(mapId);
                if (stationUid != null && EntityManager.EntityExists(stationUid.Value))
                {
                    var stationJobs = EntityManager.GetComponentOrNull<StationJobsComponent>(stationUid.Value);
                    if (stationJobs != null)
                    {
                        foreach (var entry in component.Jobs)
                        {
                            var jobId = entry.Key;
                            var amount = entry.Value;
                            _stationJobs.TryAdjustJobSlot(stationUid.Value, jobId.ToString(), amount, true, false, stationJobs);
                        }
                    }
                }
            }
        }
    }
}
