using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Roles.Jobs;
using Content.Shared._RMC14.Synth;
using Content.Shared.AU14.Objectives;
using Content.Shared.AU14.Objectives.Arrest;
using Content.Shared.AU14.Objectives.Fetch;
using Content.Shared.AU14.Objectives.Kill;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Robust.Shared.Timing;

namespace Content.Server.AU14.Objectives.Kill
{
    public sealed partial class AuKillObjectiveSystem : EntitySystem
    {
        [Dependency] private AuObjectiveSystem _objectiveSystem = default!;
        [Dependency] private GameTicker _gameTicker = default!;
        [Dependency] private JobSystem _jobSystem = default!;

        private ISawmill _logs = default!;
        private bool _shuttingDown;

        public override void Initialize()
        {
            base.Initialize();
            _logs = Logger.GetSawmill("obj-kill");
            _shuttingDown = false;
            SubscribeLocalEvent<KillObjectiveTrackerComponent, ComponentStartup>(OnMobStateStartup);
            SubscribeLocalEvent<MarkedForKillComponent, MobStateChangedEvent>(OnMobStateChanged);
        }

        public override void Shutdown()
        {
            _shuttingDown = true;
            base.Shutdown();
        }

        private void OnMobStateStartup(EntityUid uid, KillObjectiveTrackerComponent comp, ref ComponentStartup args)
        {
            Timer.Spawn(TimeSpan.FromSeconds(0.2), () =>
            {
                if (_shuttingDown || !Exists(uid))
                    return;

                TryMarkForKillDelayed(uid);
            });
        }

        private void TryMarkForKillDelayed(EntityUid uid)
        {

            if (_shuttingDown) return;
            if (HasComp<MarkedForKillComponent>(uid)) return;

            TryComp(uid, out MetaDataComponent? meta);
            var protoId = meta?.EntityPrototype?.ID ?? string.Empty;
            TryComp(uid, out NpcFactionMemberComponent? factionComp);
            var factions = factionComp?.Factions.Select(f => f.ToString().ToLowerInvariant()).ToHashSet() ?? new HashSet<string>();
            var presetId = _gameTicker.Preset?.ID.ToLowerInvariant();
            TryComp(uid, out MindContainerComponent? mindContainer);
            var mind = mindContainer?.Mind;
#if DEBUG
            _logs.Debug($"[KILL START] DELAYED - Mob ({uid}) proto='{protoId}' factions=[{string.Join(",", factions)}] - has MindContainerComponent: {mindContainer != null}, Mind: {mind != null}");
#endif
            var query = EntityQueryEnumerator<KillObjectiveComponent>();
            while (query.MoveNext(out var objUid, out var killObj))
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
                        var mark = EnsureComp<MarkedForKillComponent>(uid);
                        mark.AssociatedObjectives[objUid] = opposite;
                        _logs.Info($"[KILL SUCCESS] Mob ({uid}) marked for kill with objective {objUid} for faction {opposite} (mode={presetId}).");
                    }
                    // Do not continue here; allow other objectives to be processed
                }
                else
                {
#if DEBUG
                    _logs.Debug($"[KILL TRACE]   Mob ({uid}) proto={protoId} factions=[{string.Join(",", factions)}]");
                    _logs.Debug($"[KILL TRACE]     Objective faction: {(string.IsNullOrEmpty(auObj.Faction) ? "null/empty" : auObj.Faction.ToLowerInvariant())}");
#endif
                    var targetFaction = killObj.FactionToKill.ToLowerInvariant();
                    if (factions.Contains(targetFaction))
                    {
#if DEBUG
                        _logs.Debug($"[KILL MATCH]   Mob ({uid}) MATCHES target faction '{targetFaction}' for objective {objUid}");
#endif
                        var mark = EnsureComp<MarkedForKillComponent>(uid);
                        mark.AssociatedObjectives[objUid] = auObj.Faction.ToLowerInvariant();
                        // Cache job info if needed
                        if (!string.IsNullOrEmpty(killObj.SpecificJob))
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
                        _logs.Debug($"[KILL MATCH]   Mob ({uid}) does NOT match target faction '{targetFaction}' for objective {objUid}");
                    }
                }
            }
        }

        private void OnMobStateChanged(EntityUid uid, MarkedForKillComponent comp, ref MobStateChangedEvent args)
        {
            if (args.NewMobState != MobState.Dead)
                return;

            TryComp(uid, out MindContainerComponent? mindContainer);
            var mind = mindContainer?.Mind;
#if DEBUG
            _logs.Debug($"[KILL DEBUG] OnMobStateChanged: Entity ({uid}) has MindContainerComponent: {mindContainer != null}, Mind: {mind != null}");
#endif
            TryComp(uid, out NpcFactionMemberComponent? killedFactionComp);
            var killedFactions = killedFactionComp?.Factions.Select(f => f.ToString().ToLowerInvariant()).ToHashSet() ?? new HashSet<string>();
            if (killedFactions.Count == 0)
                _logs.Warning($"[KILL WARN] Entity ({uid}) killed but has no factions! Check prototype setup.");
            _logs.Debug($"[KILL DEBUG]   Entity ({uid}) killed. Factions: [{string.Join(",", killedFactions)}]");

            var presetId = _gameTicker.Preset?.ID.ToLowerInvariant();

            // To avoid modifying the dictionary while iterating, collect to remove after
            var objectivesToRemove = new List<EntityUid>();

            foreach (var (objectiveUid, factionToCredit) in comp.AssociatedObjectives)
            {
                if (!TryComp<KillObjectiveComponent>(objectiveUid, out var killObj))
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
                    targetFaction = killObj.FactionToKill.ToLowerInvariant();
                }

                // Check if already completed for this faction
                if (auObj.FactionNeutral)
                {
                    if (auObj.FactionStatuses.TryGetValue(factionKey, out var status) && status == AuObjectiveComponent.ObjectiveStatus.Completed)
                    {
                        _logs.Warning($"[KILL SKIP]   Objective {objectiveUid} already completed for faction '{factionKey}'.");
                        objectivesToRemove.Add(objectiveUid);
                        continue;
                    }
                }
                else
                {
                    var assignedFaction = auObj.Faction.ToLowerInvariant();
                    if (auObj.FactionStatuses.TryGetValue(assignedFaction, out var status) && status == AuObjectiveComponent.ObjectiveStatus.Completed)
                    {
                        _logs.Warning($"[KILL SKIP]   Objective {objectiveUid} already completed for faction '{assignedFaction}'.");
                        objectivesToRemove.Add(objectiveUid);
                        continue;
                    }
                }

                if (!auObj.FactionNeutral && !string.IsNullOrEmpty(killObj.SpecificJob))
                {
                    // Use cached job info from marking time
                    if (!comp.AssociatedObjectiveJobs.TryGetValue(objectiveUid, out var cachedJobId) ||
                        cachedJobId == null ||
                        cachedJobId.ToLowerInvariant() != killObj.SpecificJob.ToLowerInvariant())
                    {
                        _logs.Warning($"[KILL SKIP]   Entity ({uid}) did NOT have required job '{killObj.SpecificJob}' for objective {objectiveUid} at marking time.");
                        continue;
                    }
                }

                if (killObj.SynthOnly)
                {
                    if (!HasComp<SynthComponent>(uid))
                    {
                        _logs.Warning($"[KILL SKIP]   Entity ({uid}) does NOT have SynthComponent for objective {objectiveUid}.");
                        continue;
                    }
                }

                if (!string.IsNullOrEmpty(killObj.MobToKill))
                {
                    TryComp(uid, out MetaDataComponent? meta);
                    var protoId = meta?.EntityPrototype?.ID ?? string.Empty;

                    if (!string.Equals(protoId, killObj.MobToKill, StringComparison.OrdinalIgnoreCase))
                    {
                        _logs.Warning($"[KILL SKIP]   Entity ({uid}) does NOT match required mob prototype '{killObj.MobToKill}' for objective {objectiveUid}.");
                        continue;
                    }
                }

                // Only increment if the killed entity matches the target faction for the objective
                if (!killedFactions.Contains(targetFaction))
                {
                    _logs.Warning($"[KILL SKIP]   Entity ({uid}) does NOT match target faction '{targetFaction}' for objective {objectiveUid} (mode={presetId}). Factions: [{string.Join(",", killedFactions)}]");
                    continue;
                }

                killObj.AmountKilledPerFaction.TryAdd(factionKey, 0);

                // Prevent incrementing if already at or above required amount
                if (killObj.AmountKilledPerFaction[factionKey] >= killObj.AmountToKill)
                {
                    _logs.Warning($"[KILL SKIP]   Faction '{factionToCredit}' already reached required kills for objective {objectiveUid}.");
                    objectivesToRemove.Add(objectiveUid);
                    continue;
                }

                killObj.AmountKilledPerFaction[factionKey]++;
#if DEBUG
                _logs.Debug($"[KILL UPDATE]   Faction '{factionToCredit}' killed entity ({uid}). Total kills: {killObj.AmountKilledPerFaction[factionKey]} / {killObj.AmountToKill}");
#endif
                // If CountArrest is true, remove MarkedForArrestComponent so this entity can't also count for arrest objectives
                if (killObj.CountArrest)
                    RemComp<MarkedForArrestComponent>(uid);

                if (killObj.AmountKilledPerFaction[factionKey] >= killObj.AmountToKill)
                {
                    _objectiveSystem.CompleteObjectiveForFaction(objectiveUid, auObj, factionToCredit);
                    _logs.Info($"[KILL COMPLETE]   Objective {objectiveUid} completed for faction '{factionToCredit}'.");
                    objectivesToRemove.Add(objectiveUid);
                }
            }

            // Remove completed objectives from AssociatedObjectives
            foreach (var objUid in objectivesToRemove)
            {
                comp.AssociatedObjectives.Remove(objUid);
            }
        }

        public void ActivateKillObjectiveIfNeeded(EntityUid uid, AuObjectiveComponent _)
        {
            if (!TryComp(uid, out KillObjectiveComponent? killObj))
                return;
            if (!killObj.SpawnMob || killObj.MobsSpawned || string.IsNullOrEmpty(killObj.MobToKill) || killObj.AmountToSpawn <= 0)
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

                if (!string.IsNullOrEmpty(killObj.SpawnMarker) && markerComp.FetchId == killObj.SpawnMarker)
                    markers.Add(markerUid);
                else if (string.IsNullOrEmpty(killObj.SpawnMarker) && markerComp.Generic)
                    genericMarkers.Add(markerUid);
            }

            if (markers.Count == 0)
                markers = genericMarkers;
            if (markers.Count == 0)
                return;

            // Spawn mobs round-robin at markers
            for (var i = 0; i < killObj.AmountToSpawn; i++)
            {
                var markerIndex = i % markers.Count;
                var markerUid = markers[markerIndex];
                var xform = Comp<TransformComponent>(markerUid);
                Spawn(killObj.MobToKill, xform.Coordinates);
            }
            killObj.MobsSpawned = true;
        }
    }
}
