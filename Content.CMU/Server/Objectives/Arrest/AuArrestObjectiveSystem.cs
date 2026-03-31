using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Roles.Jobs;
using Content.Shared._RMC14.Synth;
using Content.Shared.AU14.Objectives;
using Content.Shared.AU14.Objectives.Arrest;
using Content.Shared.AU14.Objectives.Kill;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Mind.Components;
using Content.Shared.NPC.Components;
using Robust.Shared.Timing;

namespace Content.Server.AU14.Objectives.Arrest
{
    public sealed class AuArrestObjectiveSystem : EntitySystem
    {
        [Dependency] private readonly AuObjectiveSystem _objectiveSystem = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly JobSystem _jobSystem = default!;
        [Dependency] private readonly SharedCuffableSystem _cuffableSystem = default!;

        private static readonly ISawmill Sawmill = Logger.GetSawmill("au14-arrestobj");

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ArrestObjectiveTrackerComponent, ComponentStartup>(OnMobStateStartup);
            SubscribeLocalEvent<MarkedForArrestComponent, CuffedStateChangeEvent>(OnCuffStateChanged);
        }

        private void OnMobStateStartup(EntityUid uid, ArrestObjectiveTrackerComponent comp, ref ComponentStartup args)
        {
            Timer.Spawn(TimeSpan.FromSeconds(0.2), () =>
            {
                if (!EntityManager.EntityExists(uid))
                    return;
                TryMarkForArrestDelayed(uid);
            });
        }

        private string GetOppositeFaction(string faction, string? mode)
        {
            switch (mode?.ToLowerInvariant())
            {
                case "forceonforce":
                    if (faction == "govfor") return "opfor";
                    if (faction == "opfor") return "govfor";
                    break;
                case "distresssignal":
                    if (faction == "clf") return "govfor";
                    if (faction == "govfor") return "clf";
                    break;
                case "insurgency":
                    if (faction == "clf") return "govfor";
                    if (faction == "govfor") return "clf";
                    break;
            }
            return string.Empty;
        }

        private void TryMarkForArrestDelayed(EntityUid uid)
        {
            var meta = EntityManager.GetComponentOrNull<MetaDataComponent>(uid);
            var protoId = meta?.EntityPrototype?.ID ?? string.Empty;
            var factionComp = EntityManager.GetComponentOrNull<NpcFactionMemberComponent>(uid);
            var factions = factionComp?.Factions.Select(f => f.ToString().ToLowerInvariant()).ToHashSet() ?? new HashSet<string>();
            Sawmill.Info($"[ARREST OBJ TRACE] (DELAYED) Mob {uid} proto={protoId} factions=[{string.Join(",", factions)}]");

            var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
            var presetId = ticker.Preset?.ID?.ToLowerInvariant();

            var mindContainer = EntityManager.GetComponentOrNull<MindContainerComponent>(uid);
            var mind = mindContainer?.Mind;
            Sawmill.Info($"[ARREST OBJ DEBUG] TryMarkForArrestDelayed: Entity {uid} has MindContainerComponent: {mindContainer != null}, Mind: {mind != null}");

            var query = EntityManager.EntityQueryEnumerator<ArrestObjectiveComponent>();
            while (query.MoveNext(out var objUid, out var arrestObj))
            {
                if (EntityManager.EnsureComponent<AuObjectiveComponent>(objUid) is not { } auObj)
                    continue;

                // Mark for all applicable objectives, not just the first
                if (auObj.FactionNeutral)
                {
                    foreach (var faction in factions)
                    {
                        string opposite = GetOppositeFaction(faction, presetId);
                        if (string.IsNullOrEmpty(opposite))
                            continue;
                        var mark = EnsureComp<MarkedForArrestComponent>(uid);
                        mark.AssociatedObjectives[objUid] = opposite;
                        Sawmill.Info($"[ARREST OBJ SUCCESS] Mob {uid} marked for arrest with objective {objUid} for faction {opposite} (mode={presetId}).");
                    }
                }
                else
                {
                    Sawmill.Info($"[ARREST OBJ TRACE] (DELAYED) Mob {uid} proto={protoId} factions=[{string.Join(",", factions)}]");
                    Sawmill.Info($"[ARREST OBJ TRACE] Objective faction: {auObj.Faction.ToLowerInvariant()}");

                    var targetFaction = arrestObj.FactionToArrest.ToLowerInvariant();
                    if (factions.Contains(targetFaction))
                    {
                        Sawmill.Info($"[ARREST OBJ TRACE] Mob {uid} matches target faction {targetFaction} for objective {objUid}");
                        var mark = EnsureComp<MarkedForArrestComponent>(uid);
                        mark.AssociatedObjectives[objUid] = auObj.Faction.ToLowerInvariant();
                        // Cache job info if needed
                        if (!string.IsNullOrEmpty(arrestObj.SpecificJob))
                        {
                            string? jobId = null;
                            if (mind != null && _jobSystem.MindTryGetJob(mind.Value, out var jobPrototype))
                                jobId = jobPrototype.ID;
                            mark.AssociatedObjectiveJobs[objUid] = jobId;
                        }
                        else
                        {
                            mark.AssociatedObjectiveJobs[objUid] = null;
                        }
                    }
                    else
                    {
                        Sawmill.Info($"[ARREST OBJ TRACE] Mob {uid} does not match target faction {targetFaction} for objective {objUid}");
                    }
                }
            }
        }

        private void OnCuffStateChanged(EntityUid uid, MarkedForArrestComponent comp, ref CuffedStateChangeEvent args)
        {
            // Check if entity is cuffed
            if (!EntityManager.TryGetComponent<CuffableComponent>(uid, out var cuffable))
                return;

            if (!_cuffableSystem.IsCuffed((uid, cuffable), requireFullyCuffed: false))
                return;

            var mindContainer = EntityManager.GetComponentOrNull<MindContainerComponent>(uid);
            var mind = mindContainer?.Mind;
            Sawmill.Info($"[ARREST OBJ DEBUG] OnCuffStateChanged: Entity {uid} has MindContainerComponent: {mindContainer != null}, Mind: {mind != null}");

            var arrestedFactionComp = EntityManager.GetComponentOrNull<NpcFactionMemberComponent>(uid);
            var arrestedFactions = arrestedFactionComp?.Factions.Select(f => f.ToString().ToLowerInvariant()).ToHashSet() ?? new HashSet<string>();
            if (arrestedFactions.Count == 0)
                Sawmill.Warning($"[ARREST OBJ WARNING] Entity {uid} arrested but has no factions! Check prototype setup.");
            Sawmill.Info($"[ARREST OBJ DEBUG] Entity {uid} arrested. Factions: [{string.Join(",", arrestedFactions)}]");

            var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
            var presetId = ticker.Preset?.ID?.ToLowerInvariant();


            // To avoid modifying the dictionary while iterating, collect to remove after
            var objectivesToRemove = new List<EntityUid>();

            foreach (var (objectiveUid, factionToCredit) in comp.AssociatedObjectives)
            {
                if (!EntityManager.TryGetComponent<ArrestObjectiveComponent>(objectiveUid, out var arrestObj))
                    continue;
                if (!EntityManager.TryGetComponent<AuObjectiveComponent>(objectiveUid, out var auObj))
                    continue;
                if (!auObj.Active)
                    continue;

                var factionKey = factionToCredit.ToLowerInvariant();
                string targetFaction;
                if (auObj.FactionNeutral)
                {
                    targetFaction = GetOppositeFaction(factionKey, presetId);
                    if (string.IsNullOrEmpty(targetFaction))
                        continue;
                }
                else
                {
                    targetFaction = arrestObj.FactionToArrest.ToLowerInvariant();
                }

                // Check if already completed for this faction
                if (auObj.FactionNeutral)
                {
                    if (auObj.FactionStatuses.TryGetValue(factionKey, out var status) && status == AuObjectiveComponent.ObjectiveStatus.Completed)
                    {
                        Sawmill.Info($"[ARREST OBJ SKIP] Objective {objectiveUid} already completed for faction '{factionKey}'.");
                        objectivesToRemove.Add(objectiveUid);
                        continue;
                    }
                }
                else
                {
                    var assignedFaction = auObj.Faction.ToLowerInvariant();
                    if (auObj.FactionStatuses.TryGetValue(assignedFaction, out var status) && status == AuObjectiveComponent.ObjectiveStatus.Completed)
                    {
                        Sawmill.Info($"[ARREST OBJ SKIP] Objective {objectiveUid} already completed for faction '{assignedFaction}'.");
                        objectivesToRemove.Add(objectiveUid);
                        continue;
                    }
                }

                if (!auObj.FactionNeutral && !string.IsNullOrEmpty(arrestObj.SpecificJob))
                {
                    // Use cached job info from marking time
                    if (!comp.AssociatedObjectiveJobs.TryGetValue(objectiveUid, out var cachedJobId) ||
                        cachedJobId == null ||
                        cachedJobId.ToLowerInvariant() != arrestObj.SpecificJob.ToLowerInvariant())
                    {
                        Sawmill.Info($"[ARREST OBJ SKIP] Entity {uid} did not have required job '{arrestObj.SpecificJob}' for objective {objectiveUid} at marking time.");
                        continue;
                    }
                }

                if (arrestObj.SynthOnly)
                {
                    if (!EntityManager.HasComponent<SynthComponent>(uid))
                    {
                        Sawmill.Info($"[ARREST OBJ SKIP] Entity {uid} does not have SynthComponent for objective {objectiveUid}.");
                        continue;
                    }
                }

                if (!string.IsNullOrEmpty(arrestObj.MobToArrest))
                {
                    var meta = EntityManager.GetComponentOrNull<MetaDataComponent>(uid);
                    var protoId = meta?.EntityPrototype?.ID ?? string.Empty;

                    if (!string.Equals(protoId, arrestObj.MobToArrest, StringComparison.OrdinalIgnoreCase))
                    {
                        Sawmill.Info($"[ARREST OBJ SKIP] Entity {uid} does not match required mob prototype '{arrestObj.MobToArrest}' for objective {objectiveUid}.");
                        continue;
                    }
                }

                // Only increment if the arrested entity matches the target faction for the objective
                if (!arrestedFactions.Contains(targetFaction))
                {
                    Sawmill.Info($"[ARREST OBJ SKIP] Entity {uid} does not match target faction '{targetFaction}' for objective {objectiveUid} (mode={presetId}). Factions: [{string.Join(",", arrestedFactions)}]");
                    continue;
                }

                if (!arrestObj.AmountArrestedPerFaction.ContainsKey(factionKey))
                    arrestObj.AmountArrestedPerFaction[factionKey] = 0;

                // Prevent incrementing if already at or above required amount
                if (arrestObj.AmountArrestedPerFaction[factionKey] >= arrestObj.AmountToArrest)
                {
                    Sawmill.Info($"[ARREST OBJ SKIP] Faction '{factionToCredit}' already reached required arrests for objective {objectiveUid}.");
                    objectivesToRemove.Add(objectiveUid);
                    continue;
                }

                arrestObj.AmountArrestedPerFaction[factionKey]++;
                Sawmill.Info($"[ARREST OBJ UPDATE] Faction '{factionToCredit}' arrested entity {uid}. Total arrests: {arrestObj.AmountArrestedPerFaction[factionKey]} / {arrestObj.AmountToArrest}");

                // If RemoveKillMark is true, remove MarkedForKillComponent so this entity can't also count for kill objectives
                if (arrestObj.RemoveKillMark)
                    RemComp<MarkedForKillComponent>(uid);

                if (arrestObj.AmountArrestedPerFaction[factionKey] >= arrestObj.AmountToArrest)
                {
                    _objectiveSystem.CompleteObjectiveForFaction(objectiveUid, auObj, factionToCredit);
                    Sawmill.Info($"[ARREST OBJ COMPLETE] Objective {objectiveUid} completed for faction '{factionToCredit}'.");
                    objectivesToRemove.Add(objectiveUid);
                }
            }

            // Remove completed objectives from AssociatedObjectives
            foreach (var objUid in objectivesToRemove)
            {
                comp.AssociatedObjectives.Remove(objUid);
            }
        }

        public void ActivateArrestObjectiveIfNeeded(EntityUid uid, AuObjectiveComponent comp)
        {
            if (!EntityManager.TryGetComponent(uid, out ArrestObjectiveComponent? arrestObj))
                return;
            if (!arrestObj.SpawnMob || arrestObj.MobsSpawned || string.IsNullOrEmpty(arrestObj.MobToArrest) || arrestObj.AmountToSpawn <= 0)
                return;

            // Find all relevant markers
            var markers = new List<EntityUid>();
            var genericMarkers = new List<EntityUid>();
            var markerQuery = EntityManager.AllEntityQueryEnumerator<Content.Shared.AU14.Objectives.Fetch.FetchObjectiveMarkerComponent, TransformComponent>();
            while (markerQuery.MoveNext(out var markerUid, out var markerComp, out _))
            {
                if (!string.IsNullOrEmpty(arrestObj.SpawnMarker) && markerComp.FetchId == arrestObj.SpawnMarker)
                    markers.Add(markerUid);
                else if (string.IsNullOrEmpty(arrestObj.SpawnMarker) && markerComp.Generic)
                    genericMarkers.Add(markerUid);
            }
            if (markers.Count == 0)
                markers = genericMarkers;
            if (markers.Count == 0)
                return;

            // Spawn mobs round-robin at markers
            for (var i = 0; i < arrestObj.AmountToSpawn; i++)
            {
                var markerIndex = i % markers.Count;
                var markerUid = markers[markerIndex];
                var xform = EntityManager.GetComponent<TransformComponent>(markerUid);
                EntityManager.SpawnEntity(arrestObj.MobToArrest, xform.Coordinates);
            }
            arrestObj.MobsSpawned = true;
        }
    }
}








