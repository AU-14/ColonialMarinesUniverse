using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Roles.Jobs;
using Content.Shared._RMC14.Synth;
using Content.Shared.AU14.Objectives;
using Content.Shared.AU14.Objectives.Fetch;
using Content.Shared.AU14.Objectives.Arrest;
using Content.Shared.AU14.Objectives.Kill;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Mind.Components;
using Content.Shared.NPC.Components;
using Robust.Shared.Timing;

namespace Content.Server.AU14.Objectives.Arrest
{
    public sealed partial class AuArrestObjectiveSystem : EntitySystem
    {
        [Dependency] private AuObjectiveSystem _objectiveSystem = default!;
        [Dependency] private GameTicker _gameTicker = default!;
        [Dependency] private JobSystem _jobSystem = default!;
        [Dependency] private SharedCuffableSystem _cuffableSystem = default!;

        private ISawmill _logs = default!;
        private bool _shuttingDown;

        public override void Initialize()
        {
            base.Initialize();
            _logs = Logger.GetSawmill("obj-arrest");
            SubscribeLocalEvent<ArrestObjectiveTrackerComponent, ComponentStartup>(OnMobStateStartup);
            SubscribeLocalEvent<MarkedForArrestComponent, CuffedStateChangeEvent>(OnCuffStateChanged);
        }

        public override void Shutdown()
        {
            _shuttingDown = true;
            base.Shutdown();
        }

        private void OnMobStateStartup(EntityUid uid, ArrestObjectiveTrackerComponent comp, ref ComponentStartup args)
        {
            Timer.Spawn(TimeSpan.FromSeconds(0.2), () =>
            {
                if (_shuttingDown || !Exists(uid))
                    return;

                TryMarkForArrestDelayed(uid);
            });
        }

        private void TryMarkForArrestDelayed(EntityUid uid)
        {
            if (_shuttingDown) return;
            if (HasComp<MarkedForArrestComponent>(uid)) return;

            TryComp(uid, out MetaDataComponent? meta);
            var protoId = meta?.EntityPrototype?.ID ?? string.Empty;
            TryComp(uid, out NpcFactionMemberComponent? factionComp);
            var factions = factionComp?.Factions.Select(f => f.ToString().ToLowerInvariant()).ToHashSet() ?? new HashSet<string>();
            var presetId = _gameTicker.Preset?.ID.ToLowerInvariant();
            TryComp(uid, out MindContainerComponent? mindContainer);
            var mind = mindContainer?.Mind;
#if DEBUG
            _logs.Debug($"[ARREST START] DELAYED - Mob ({uid}) proto='{protoId}' factions=[{string.Join(",", factions)}] - has MindContainerComponent: {mindContainer != null}, Mind: {mind != null}");
#endif
            var query = EntityQueryEnumerator<ArrestObjectiveComponent>();
            while (query.MoveNext(out var objUid, out var arrestObj))
            {
                if (EnsureComp<AuObjectiveComponent>(objUid) is not { } auObj)
                    continue;

                // Mark for all applicable objectives, not just the first
                if (auObj.FactionNeutral)
                {
                    foreach (var faction in factions)
                    {
                        string opposite = _objectiveSystem.GetOppositeFaction(faction, presetId);
                        if (string.IsNullOrEmpty(opposite))
                            continue;
                        var mark = EnsureComp<MarkedForArrestComponent>(uid);
                        mark.AssociatedObjectives[objUid] = opposite;
                        _logs.Info($"[ARREST SUCCESS] Mob ({uid}) marked for arrest with objective {objUid} for faction {opposite} (mode={presetId}).");
                    }
                }
                else
                {
#if DEBUG
                    _logs.Debug($"[ARREST TRACE] DELAYED - Mob ({uid}) proto='{protoId}' factions=[{string.Join(",", factions)}]");
                    _logs.Debug($"[ARREST TRACE]   Objective faction: {(string.IsNullOrEmpty(auObj.Faction) ? "null" : auObj.Faction.ToLowerInvariant())}");
#endif
                    var targetFaction = arrestObj.FactionToArrest.ToLowerInvariant();
                    if (factions.Contains(targetFaction))
                    {
#if DEBUG
                        _logs.Debug($"[ARREST MATCH] Mob ({uid}) MATCHES target faction '{targetFaction}' for objective {objUid}");
#endif
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
                        _logs.Debug($"[ARREST MATCH] Mob ({uid}) does NOT match target faction '{targetFaction}' for objective {objUid}");
                    }
                }
            }
        }

        private void OnCuffStateChanged(EntityUid uid, MarkedForArrestComponent comp, ref CuffedStateChangeEvent args)
        {
            // Check if entity is cuffed
            if (!TryComp<CuffableComponent>(uid, out var cuffable))
                return;

            if (!_cuffableSystem.IsCuffed((uid, cuffable), requireFullyCuffed: false))
                return;

            TryComp(uid, out MindContainerComponent? mindContainer);
            var mind = mindContainer?.Mind;
#if DEBUG
            _logs.Debug($"[ARREST DEBUG] OnCuffStateChanged: Entity ({uid}) has MindContainerComponent: {mindContainer != null}, Mind: {mind != null}");
#endif
            TryComp(uid, out NpcFactionMemberComponent? arrestedFactionComp);
            var arrestedFactions = arrestedFactionComp?.Factions.Select(f => f.ToString().ToLowerInvariant()).ToHashSet() ?? new HashSet<string>();
            if (arrestedFactions.Count == 0)
                _logs.Warning($"[ARREST WARN] Entity ({uid}) arrested but has no factions! Check prototype setup.");
            _logs.Debug($"[ARREST DEBUG]   Entity ({uid}) arrested. Factions: [{string.Join(",", arrestedFactions)}]");

            var presetId = _gameTicker.Preset?.ID.ToLowerInvariant();

            // To avoid modifying the dictionary while iterating, collect to remove after
            var objectivesToRemove = new List<EntityUid>();

            foreach (var (objectiveUid, factionToCredit) in comp.AssociatedObjectives)
            {
                if (!TryComp<ArrestObjectiveComponent>(objectiveUid, out var arrestObj))
                    continue;
                if (!TryComp<AuObjectiveComponent>(objectiveUid, out var auObj))
                    continue;
                if (!auObj.Active)
                    continue;

                var factionKey = factionToCredit.ToLowerInvariant();
                string targetFaction;
                if (auObj.FactionNeutral)
                {
                    targetFaction = _objectiveSystem.GetOppositeFaction(factionKey, presetId);
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
#if DEBUG
                        _logs.Debug($"[ARREST SKIP] Objective {objectiveUid} already completed for faction '{factionKey}'.");
#endif
                        objectivesToRemove.Add(objectiveUid);
                        continue;
                    }
                }
                else
                {
                    var assignedFaction = auObj.Faction.ToLowerInvariant();
                    if (auObj.FactionStatuses.TryGetValue(assignedFaction, out var status) && status == AuObjectiveComponent.ObjectiveStatus.Completed)
                    {
#if DEBUG
                        _logs.Debug($"[ARREST SKIP] Objective {objectiveUid} already completed for faction '{assignedFaction}'.");
#endif
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
                        _logs.Warning($"[ARREST SKIP]   Entity ({uid}) did NOT have required job '{arrestObj.SpecificJob}' for objective {objectiveUid} at marking time.");
                        continue;
                    }
                }

                if (arrestObj.SynthOnly)
                {
                    if (!HasComp<SynthComponent>(uid))
                    {
                        _logs.Warning($"[ARREST SKIP]   Entity ({uid}) does NOT have SynthComponent for objective {objectiveUid}.");
                        continue;
                    }
                }

                if (!string.IsNullOrEmpty(arrestObj.MobToArrest))
                {
                    TryComp(uid, out MetaDataComponent? meta);
                    var protoId = meta?.EntityPrototype?.ID ?? string.Empty;

                    if (!string.Equals(protoId, arrestObj.MobToArrest, StringComparison.OrdinalIgnoreCase))
                    {
                        _logs.Warning($"[ARREST SKIP]   Entity ({uid}) does NOT match required mob prototype '{arrestObj.MobToArrest}' for objective {objectiveUid}.");
                        continue;
                    }
                }

                // Only increment if the arrested entity matches the target faction for the objective
                if (!arrestedFactions.Contains(targetFaction))
                {
                    _logs.Warning($"[ARREST SKIP]   Entity ({uid}) does NOT match target faction '{targetFaction}' for objective {objectiveUid} (mode={presetId}). Factions: [{string.Join(",", arrestedFactions)}]");
                    continue;
                }

                arrestObj.AmountArrestedPerFaction.TryAdd(factionKey, 0);

                // Prevent incrementing if already at or above required amount
                if (arrestObj.AmountArrestedPerFaction[factionKey] >= arrestObj.AmountToArrest)
                {
                    _logs.Warning($"[ARREST SKIP]   Faction '{factionToCredit}' already reached required arrests for objective {objectiveUid}.");
                    objectivesToRemove.Add(objectiveUid);
                    continue;
                }

                arrestObj.AmountArrestedPerFaction[factionKey]++;
#if DEBUG
                _logs.Debug($"[ARREST UPDATE]   Faction '{factionToCredit}' arrested entity ({uid}). Total arrests: {arrestObj.AmountArrestedPerFaction[factionKey]} / {arrestObj.AmountToArrest}");
#endif
                // If RemoveKillMark is true, remove MarkedForKillComponent so this entity can't also count for kill objectives
                if (arrestObj.RemoveKillMark)
                    RemComp<MarkedForKillComponent>(uid);

                if (arrestObj.AmountArrestedPerFaction[factionKey] < arrestObj.AmountToArrest)
                    continue;

                _objectiveSystem.CompleteObjectiveForFaction(objectiveUid, auObj, factionToCredit);
                _logs.Info($"[ARREST COMPLETE]   Objective {objectiveUid} completed for faction '{factionToCredit}'.");
                objectivesToRemove.Add(objectiveUid);
            }

            // Remove completed objectives from AssociatedObjectives
            foreach (var objUid in objectivesToRemove)
            {
                comp.AssociatedObjectives.Remove(objUid);
            }
        }

        public void ActivateArrestObjectiveIfNeeded(EntityUid uid, AuObjectiveComponent comp)
        {
            if (!TryComp(uid, out ArrestObjectiveComponent? arrestObj))
                return;
            if (!arrestObj.SpawnMob || arrestObj.MobsSpawned || string.IsNullOrEmpty(arrestObj.MobToArrest) || arrestObj.AmountToSpawn <= 0)
                return;

            // Find all relevant markers
            var markers = new List<EntityUid>();
            var genericMarkers = new List<EntityUid>();
            var objMap = Transform(uid).MapID;
            var markerQuery = AllEntityQuery<FetchObjectiveMarkerComponent, TransformComponent>();
            while (markerQuery.MoveNext(out var markerUid, out var markerComp, out var markerXform))
            {
                if (markerComp.Used || markerXform.MapID != objMap)
                    continue;

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
                var xform = Comp<TransformComponent>(markerUid);
                Spawn(arrestObj.MobToArrest, xform.Coordinates);
            }
            arrestObj.MobsSpawned = true;
        }
    }
}
