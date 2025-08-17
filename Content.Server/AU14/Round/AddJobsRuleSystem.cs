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

namespace Content.Server.AU14.Round;

[UsedImplicitly]
public sealed class AddJobsRuleSystem : GameRuleSystem<AddJobsRuleComponent>
{
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly AuRoundSystem _auRoundSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly PlatoonSpawnRuleSystem _platoonSpawnRule = default!;

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
        else if (planet != null && planet.PlatoonsGovfor != null && planet.PlatoonsGovfor.Count > 0)
        {
            // Use the first govfor platoon as fallback
            if (protoMgr.TryIndex<PlatoonPrototype>(planet.PlatoonsGovfor[0], out var foundPlatoon))
                platoon = foundPlatoon;
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


        if (planet != null && component.AddToShip && !string.IsNullOrEmpty(component.ShipFaction))
        {
            // Check if the planet wants to spawn in ship for this faction
            bool spawnInShip = component.ShipFaction == "govfor" && planet.GovforInShip;
            if (component.ShipFaction == "opfor" && planet.OpforInShip)
                spawnInShip = true;

            if (spawnInShip)
            {
                // Find the ship entity with ShipFactionComponent matching the faction
                foreach (var (shipUid, shipFaction) in EntityManager.EntityQuery<ShipFactionComponent>(true).Select(s => (s.Owner, s)))
                {
                    if (shipFaction.Faction != component.ShipFaction)
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
                    return;
                }
            }
        }

        // --- Default: Add jobs to the planet's station as before ---
        if (planet != null)
        {
            foreach (var (planetUid, station) in EntityManager.EntityQuery<StationJobsComponent>(true).Select(s => (s.Owner, s)))
            {
                if (!EntityManager.HasComponent(planetUid, planet.GetType()))
                    continue;
                foreach (var entry in component.Jobs)
                {
                    var jobId = entry.Key;
                    var amount = entry.Value;
                    _stationJobs.TryAdjustJobSlot(planetUid, jobId.ToString(), amount, true, false, station);
                }
                break;
            }
        }
    }
}
