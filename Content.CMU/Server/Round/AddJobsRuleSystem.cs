using System.Linq;
using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Server.Station.Components;
using Content.Shared.AU14;
using Content.Shared._CMU14.Threats;
using Content.Shared.AU14.util;
using Content.Shared.CMU.Round;
using Content.Shared.GameTicking.Components;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Server.AU14.Round;

[UsedImplicitly]
public sealed partial class AddJobsRuleSystem : GameRuleSystem<AddJobsRuleComponent>
{
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private CMURoundDirectorSystem _roundDirector = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSystem _stationSystem = default!;

    protected override void Started(EntityUid uid, AddJobsRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        PlatoonPrototype? platoon = null;
        var planet = _auRoundSystem.GetSelectedPlanet();

        var presetId = _gameTicker.CurrentPreset?.ID ?? _gameTicker.Preset?.ID;
        var isDistressPreset = !string.IsNullOrEmpty(presetId) &&
                               presetId.Equals("distresssignal", StringComparison.OrdinalIgnoreCase);
        var isColonyFallPreset = !string.IsNullOrEmpty(presetId) &&
                                 presetId.Equals("ColonyFall", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(component.ShipFaction, "opfor", StringComparison.OrdinalIgnoreCase))
        {
            _roundDirector.TryGetLegacyForceProjection(RoundSide.Opfor, out platoon);
        }
        else
        {
            _roundDirector.TryGetLegacyForceProjection(RoundSide.Govfor, out platoon);
            if (platoon == null && planet != null && planet.PlatoonsGovfor.Count > 0)
            {
                if (_prototypeManager.TryIndex<PlatoonPrototype>(planet.PlatoonsGovfor[0], out var foundPlatoon))
                    platoon = foundPlatoon;
            }
        }

        // If the platoon has a jobSlotOverride, use ONLY those jobs and skip all other job logic
        if (platoon != null && platoon.JobSlotOverride.Count > 0)
        {
            var jobsToAdd = new Dictionary<ProtoId<JobPrototype>, int>();
            var team = component.ShipFaction != null && component.ShipFaction.Equals("opfor", StringComparison.OrdinalIgnoreCase)
                ? "OPFOR"
                : "GOVFOR";
            foreach (var (jobClass, slotCount) in platoon.JobSlotOverride)
            {
                var jobId = $"AU14Job{team}{jobClass}";
                if (_prototypeManager.TryIndex<JobPrototype>(jobId, out var proto))
                    jobsToAdd[proto.ID] = slotCount;
                else
                    Logger.GetSawmill("content").Warning($"[AddJobsRuleSystem] Could not find job prototype: {jobId}");
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
                               presetId.Equals("insurgency", StringComparison.OrdinalIgnoreCase);
            var isFof = !string.IsNullOrEmpty(presetId) &&
                        presetId.Equals("forceonforce", StringComparison.OrdinalIgnoreCase);

            if (isDistressPreset || isColonyFallPreset)
            {
                // ColonyFall / Distress — scaling comes from the selected threat
                var threat = _auRoundSystem.SelectedThreat;
                if (threat?.JobScaling != null)
                    _prototypeManager.TryIndex<JobScalePrototype>(threat.JobScaling.Value, out scaleDef);
            }
            else if (isFof)
            {
                // ForceOnForce — scaling comes from planet's FOF field
                if (planet?.JobScalingFof != null)
                    _prototypeManager.TryIndex<JobScalePrototype>(planet.JobScalingFof.Value, out scaleDef);
            }
            else if (isInsurgency)
            {
                // Insurgency — scaling comes from planet's Insurgency field
                if (planet?.JobScalingIns != null)
                    _prototypeManager.TryIndex<JobScalePrototype>(planet.JobScalingIns.Value, out scaleDef);
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
                        Logger.GetSawmill("content").Info($"[AddJobsRuleSystem] Job scaling (component): {jobId} => {scaledSlots} slots " +
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

                        Logger.GetSawmill("content").Info($"[AddJobsRuleSystem] Job scaling (station): {jobId} => " +
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
                    if (stationUid != null && Exists(stationUid.Value))
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
                                    // Benchmarks replace the configured slot count with one absolute value.
                                    _stationJobs.TrySetJobSlotAndRoundStart(stationUid.Value,
                                        jobProtoId,
                                        scaledSlots,
                                        false,
                                        stationJobs);
                                }
                                else
                                {
                                    // Additive scaling must preserve any intentional difference between
                                    // round-start and mid-round slot counts.
                                    var delta = scaledSlots - existingSlots;
                                    if (delta != 0)
                                    {
                                        _stationJobs.TryAdjustJobSlotAndRoundStart(stationUid.Value,
                                            jobProtoId,
                                            delta,
                                            false,
                                            stationJobs);
                                    }
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
        if (isColonyFallPreset &&
            string.Equals(component.ShipFaction, "govfor", StringComparison.OrdinalIgnoreCase))
            return;

        if (planet != null && !string.IsNullOrEmpty(component.ShipFaction))
        {
            var faction = component.ShipFaction;
            var addToShip = false;
            var addToPlanet = false;

            if (faction.Equals("govfor", StringComparison.OrdinalIgnoreCase))
            {
                addToShip = planet.GovforInShip;
                addToPlanet = !planet.GovforInShip;
            }
            else if (faction.Equals("opfor", StringComparison.OrdinalIgnoreCase))
            {
                addToShip = planet.OpforInShip;
                addToPlanet = !planet.OpforInShip;
            }

            if (addToShip && component.AddToShip)
            {
                // Find the ship entity with ShipFactionComponent matching the faction
                var query = AllEntityQuery<ShipFactionComponent>();
                while (query.MoveNext(out var shipUid, out var shipFaction))
                {
                    if (!string.Equals(shipFaction.Faction, faction, StringComparison.OrdinalIgnoreCase))
                        continue;
                    // Find the station entity that owns this ship
                    var stationUid = _stationSystem.GetOwningStation(shipUid);
                    if (stationUid == null || !Exists(stationUid.Value))
                        continue;
                    var stationJobs = EntityManager.GetComponentOrNull<StationJobsComponent>(stationUid.Value);
                    if (stationJobs == null)
                        continue;

                    foreach (var entry in component.Jobs)
                    {
                        var jobId = entry.Key;
                        var amount = entry.Value;
                        _stationJobs.TryAdjustJobSlotAndRoundStart(stationUid.Value,
                            jobId,
                            amount,
                            true,
                            stationJobs);
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
                if (stationUid != null && Exists(stationUid.Value))
                {
                    var stationJobs = EntityManager.GetComponentOrNull<StationJobsComponent>(stationUid.Value);
                    if (stationJobs != null)
                    {
                        foreach (var entry in component.Jobs)
                        {
                            var jobId = entry.Key;
                            var amount = entry.Value;
                            _stationJobs.TryAdjustJobSlotAndRoundStart(stationUid.Value,
                                jobId,
                                amount,
                                true,
                                stationJobs);
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
            if (string.Equals(component.ShipFaction, "opfor", StringComparison.OrdinalIgnoreCase))
            {
                // Opfor always adds to the ship
                addToPlanet = false;
            }
            else if (string.Equals(component.ShipFaction, "govfor", StringComparison.OrdinalIgnoreCase))
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
                if (stationUid != null && Exists(stationUid.Value))
                {
                    var stationJobs = EntityManager.GetComponentOrNull<StationJobsComponent>(stationUid.Value);
                    if (stationJobs != null)
                    {
                        if (isDistressPreset)
                        {
                            var existing = stationJobs.JobList.Keys.ToList();
                            foreach (var jobKey in existing)
                            {
                                _stationJobs.TrySetJobSlotAndRoundStart(stationUid.Value,
                                    new ProtoId<JobPrototype>(jobKey),
                                    0,
                                    false,
                                    stationJobs);
                            }
                        }

                        foreach (var entry in component.Jobs)
                        {
                            var jobId = entry.Key;
                            var amount = entry.Value;
                            _stationJobs.TryAdjustJobSlotAndRoundStart(stationUid.Value,
                                jobId,
                                amount,
                                true,
                                stationJobs);
                        }
                    }
                }
            }
        }
    }
}
