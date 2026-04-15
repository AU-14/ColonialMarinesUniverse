using System.Linq;
using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Server.Station.Components;
using Content.Shared.AU14;
using Content.Shared.AU14.Threats;
using Content.Shared.AU14.util;
using Content.Shared.GameTicking.Components;
using Content.Shared.Roles;
using Robust.Server.Player;
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
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    protected override void Started(EntityUid uid, AddJobsRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {



        PlatoonPrototype? platoon = null;
        var planet = _auRoundSystem.GetSelectedPlanet();
        var protoMgr = IoCManager.Resolve<IPrototypeManager>();
        var platoonSpawnRule = _platoonSpawnRule;

        var presetId = _gameTicker.CurrentPreset?.ID ?? _gameTicker.Preset?.ID;
        var isDistressPreset = !string.IsNullOrEmpty(presetId) && (
            presetId.Equals("distresssignal", StringComparison.InvariantCultureIgnoreCase)
        );
        var isColonyFallPreset = !string.IsNullOrEmpty(presetId) && presetId.Equals("ColonyFall", StringComparison.InvariantCultureIgnoreCase);

        if (component.ShipFaction != null && component.ShipFaction.ToLower() == "opfor")
        {
            platoon = platoonSpawnRule.SelectedOpforPlatoon;
        }
        else
        {
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

        // --- Job Scaling Logic ---
        // ForceOnForce: read from planet's JobScalingFof
        // Insurgency: read from planet's JobScalingIns
        // ColonyFall / Distress: read from ThreatPrototype.JobScaling
        // (Entity/threat spawn scaling is handled separately via PartySpawnPrototype.Scaling)
        {
            var playerCount = _playerManager.PlayerCount;
            JobScalePrototype? scaleDef = null;

            var isInsurgency = !string.IsNullOrEmpty(presetId) &&
                               presetId.Equals("insurgency", StringComparison.InvariantCultureIgnoreCase);
            var isFof = !string.IsNullOrEmpty(presetId) &&
                        presetId.Equals("forceonforce", StringComparison.InvariantCultureIgnoreCase);

            if (isDistressPreset || isColonyFallPreset)
            {
                // ColonyFall / Distress — scaling comes from the selected threat
                var threat = _auRoundSystem._selectedthreat;
                if (threat?.JobScaling != null)
                    protoMgr.TryIndex<JobScalePrototype>(threat.JobScaling.Value, out scaleDef);
            }
            else if (isFof)
            {
                // ForceOnForce — scaling comes from planet's FOF field
                if (planet?.JobScalingFof != null)
                    protoMgr.TryIndex<JobScalePrototype>(planet.JobScalingFof.Value, out scaleDef);
            }
            else if (isInsurgency)
            {
                // Insurgency — scaling comes from planet's Insurgency field
                if (planet?.JobScalingIns != null)
                    protoMgr.TryIndex<JobScalePrototype>(planet.JobScalingIns.Value, out scaleDef);
            }

            if (scaleDef != null)
            {
                component.Jobs ??= new Dictionary<ProtoId<JobPrototype>, int>();

                // Track which jobs in the scale definition are NOT part of component.Jobs
                // (i.e. colony jobs that live on the station already). We'll scale those directly on the station.
                var stationOnlyScaling = new Dictionary<ProtoId<JobPrototype>, JobScaleEntry>();

                foreach (var (jobId, entry) in scaleDef.Jobs)
                {
                    var jobProtoId = new ProtoId<JobPrototype>(jobId);
                    var isComponentJob = component.Jobs.ContainsKey(jobProtoId);

                    if (isComponentJob)
                    {
                        // Job managed by this component — scale in component.Jobs as before
                        component.Jobs.TryGetValue(jobProtoId, out var existingSlots);
                        var baseSlots = entry.Benchmark ?? existingSlots;
                        var extra = JobScaling.CalculateExtraSlots(playerCount, entry);
                        var scaledSlots = JobScaling.CalculateScaledSlots(playerCount, existingSlots, entry);

                        component.Jobs[jobProtoId] = scaledSlots;
                        Logger.Info($"[AddJobsRuleSystem] Job scaling (component): {jobId} => {scaledSlots} slots " +
                                    $"(base={baseSlots}, extra={extra}, players={playerCount}, " +
                                    $"benchmark={entry.Benchmark?.ToString() ?? "null"}, " +
                                    $"maximum={entry.Maximum?.ToString() ?? "null"}, " +
                                    $"scale={entry.Scale}, threshold={entry.WhenToBeginScaling})");
                    }
                    else
                    {
                        // Colony job — not in component.Jobs, need to scale directly on the station
                        var extra = JobScaling.CalculateExtraSlots(playerCount, entry);
                        stationOnlyScaling[jobProtoId] = entry;

                        Logger.Info($"[AddJobsRuleSystem] Job scaling (station): {jobId} => " +
                                    $"(extra={extra}, players={playerCount}, benchmark={entry.Benchmark?.ToString() ?? "null"}, " +
                                    $"maximum={entry.Maximum?.ToString() ?? "null"}, " +
                                    $"scale={entry.Scale}, threshold={entry.WhenToBeginScaling})");
                    }
                }

                // Apply station-only scaling directly to the station's job list
                if (stationOnlyScaling.Count > 0)
                {
                    var mapId = _gameTicker.DefaultMap;
                    var stationUid = _stationSystem.GetStationInMap(mapId);
                    if (stationUid != null && EntityManager.EntityExists(stationUid.Value))
                    {
                        var stationJobs = EntityManager.GetComponentOrNull<StationJobsComponent>(stationUid.Value);
                        if (stationJobs != null)
                        {
                            foreach (var (jobProtoId, entry) in stationOnlyScaling)
                            {
                                _stationJobs.TryGetJobSlot(stationUid.Value, jobProtoId.ToString(), out var existingMaybe, stationJobs);
                                if (existingMaybe == null)
                                {
                                    // Job doesn't exist on station yet — it will be added (and scaled) by its owning rule.
                                    // Never pre-seed it here, or the owning rule will add on top and double the count.
                                    continue;
                                }

                                var existingSlots = existingMaybe.Value;
                                var scaledSlots = JobScaling.CalculateScaledSlots(playerCount, existingSlots, entry);

                                if (entry.Benchmark != null)
                                {
                                    // Benchmark set: override to absolute value (with optional maximum cap).
                                    _stationJobs.TrySetJobSlot(stationUid.Value, jobProtoId.ToString(), scaledSlots, true, stationJobs);
                                }
                                else
                                {
                                    // No benchmark: adjust to the computed absolute value (with optional maximum cap).
                                    var delta = scaledSlots - existingSlots;
                                    if (delta != 0)
                                        _stationJobs.TryAdjustJobSlot(stationUid.Value, jobProtoId.ToString(), delta, false, false, stationJobs);
                                }
                            }
                        }
                    }
                }
            }
        }
        // --- END: Job Scaling Logic ---

        // If there are no jobs to add, return early
        if (component.Jobs == null || component.Jobs.Count == 0)
            return;

        // If this is ColonyFall, don't add GOVFOR jobs
        if (isColonyFallPreset && !string.IsNullOrEmpty(component.ShipFaction) && component.ShipFaction.Equals("govfor", StringComparison.InvariantCultureIgnoreCase))
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
                        // Also update the round-start setup slots so readied players can spawn on these jobs.
                        try
                        {
                            // Compute current round-start amount (if any) and add to it.
                            if (stationJobs.SetupAvailableJobs.TryGetValue(jobId, out var arr) && arr.Length > 0)
                            {
                                var existing = arr[0];
                                _stationJobs.SetRoundStartJobSlot(stationUid.Value, jobId, existing + amount, stationJobs);
                            }
                            else
                            {
                                _stationJobs.SetRoundStartJobSlot(stationUid.Value, jobId, amount, stationJobs);
                            }
                        }
                        catch
                        {
                            // If anything goes wrong, fall back to not crashing the rule system.
                        }
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
                        if (isDistressPreset)
                        {
                            var existing = stationJobs.JobList.Keys.ToList();
                            foreach (var jobKey in existing)
                            {
                                _stationJobs.TrySetJobSlot(stationUid.Value, jobKey.ToString(), 0, false, stationJobs);
                            }
                        }

                        foreach (var entry in component.Jobs)
                        {
                            var jobId = entry.Key;
                            var amount = entry.Value;
                            _stationJobs.TryAdjustJobSlot(stationUid.Value, jobId.ToString(), amount, true, false, stationJobs);
                            // Keep round-start setup in sync so readied players see these jobs.
                            try
                            {
                                if (stationJobs.SetupAvailableJobs.TryGetValue(jobId, out var arr) && arr.Length > 0)
                                {
                                    var existing = arr[0];
                                    _stationJobs.SetRoundStartJobSlot(stationUid.Value, jobId, existing + amount, stationJobs);
                                }
                                else
                                {
                                    _stationJobs.SetRoundStartJobSlot(stationUid.Value, jobId, amount, stationJobs);
                                }
                            }
                            catch
                            {
                            }
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
                        if (isDistressPreset)
                        {
                            var existing = stationJobs.JobList.Keys.ToList();
                            foreach (var jobKey in existing)
                            {
                                _stationJobs.TrySetJobSlot(stationUid.Value, jobKey.ToString(), 0, false, stationJobs);
                            }
                        }

                        foreach (var entry in component.Jobs)
                        {
                            var jobId = entry.Key;
                            var amount = entry.Value;
                            _stationJobs.TryAdjustJobSlot(stationUid.Value, jobId.ToString(), amount, true, false, stationJobs);
                            // Keep round-start setup in sync so readied players see these jobs.
                            try
                            {
                                if (stationJobs.SetupAvailableJobs.TryGetValue(jobId, out var arr) && arr.Length > 0)
                                {
                                    var existing = arr[0];
                                    _stationJobs.SetRoundStartJobSlot(stationUid.Value, jobId, existing + amount, stationJobs);
                                }
                                else
                                {
                                    _stationJobs.SetRoundStartJobSlot(stationUid.Value, jobId, amount, stationJobs);
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
        }
    }
}


