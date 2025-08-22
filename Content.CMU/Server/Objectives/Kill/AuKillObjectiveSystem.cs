using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Roles.Jobs;
using Content.Shared._RMC14.Synth;
using Content.Shared.AU14.Objectives.Kill;
using Content.Shared.Mobs;
using Content.Shared.AU14.Objectives;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Roles;
using Robust.Shared.Timing;

namespace Content.Server.AU14.Objectives.Kill
{
    public sealed class AuKillObjectiveSystem : EntitySystem
    {
        [Dependency] private readonly AuObjectiveSystem _objectiveSystem = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly JobSystem _jobSystem = default!;

        private static readonly ISawmill Sawmill = Logger.GetSawmill("au14-killobj");

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<KillObjectiveTrackerComponent, ComponentStartup>(OnMobStateStartup);
            SubscribeLocalEvent<MarkedForKillComponent, MobStateChangedEvent>(OnMobStateChanged);
        }

        private void OnMobStateStartup(EntityUid uid, KillObjectiveTrackerComponent comp, ref ComponentStartup args)
        {
            Timer.Spawn(TimeSpan.FromSeconds(0.2), () =>
            {
                if (!EntityManager.EntityExists(uid))
                    return;
                TryMarkForKillDelayed(uid);
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
                case "distresssingal":
                    if (faction == "clf") return "govfor";
                    if (faction == "govfor") return "clf";
                    break;
            }
            return string.Empty;
        }

        private void TryMarkForKillDelayed(EntityUid uid)
        {
            var meta = EntityManager.GetComponentOrNull<MetaDataComponent>(uid);
            var protoId = meta?.EntityPrototype?.ID ?? string.Empty;
            var factionComp = EntityManager.GetComponentOrNull<NpcFactionMemberComponent>(uid);
            var factions = factionComp?.Factions.Select(f => f.ToString().ToLowerInvariant()).ToHashSet() ?? new HashSet<string>();
            Sawmill.Info($"[KILL OBJ TRACE] (DELAYED) Mob {uid} proto={protoId} factions=[{string.Join(",", factions)}]");

            var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
            var presetId = ticker.Preset?.ID?.ToLowerInvariant();

            var mindContainer = EntityManager.GetComponentOrNull<MindContainerComponent>(uid);
            var mind = mindContainer?.Mind;
            Sawmill.Info($"[KILL OBJ DEBUG] TryMarkForKillDelayed: Entity {uid} has MindContainerComponent: {mindContainer != null}, Mind: {mind != null}");

            var query = EntityManager.EntityQueryEnumerator<KillObjectiveComponent>();
            while (query.MoveNext(out var objUid, out var killObj))
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
                        var mark = EnsureComp<MarkedForKillComponent>(uid);
                        mark.AssociatedObjectives[objUid] = opposite;
                        Sawmill.Info($"[KILL OBJ SUCCESS] Mob {uid} marked for kill with objective {objUid} for faction {opposite} (mode={presetId}).");
                    }
                    // Do not continue here; allow other objectives to be processed
                }
                else
                {
                    Sawmill.Info($"[KILL OBJ TRACE] (DELAYED) Mob {uid} proto={protoId} factions=[{string.Join(",", factions)}]");
                    Sawmill.Info($"[KILL OBJ TRACE] Objective faction: {auObj.Faction.ToLowerInvariant()}");

                    var targetFaction = killObj.FactionToKill.ToLowerInvariant();
                    if (factions.Contains(targetFaction))
                    {
                        Sawmill.Info($"[KILL OBJ TRACE] Mob {uid} matches target faction {targetFaction} for objective {objUid}");
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
                        Sawmill.Info($"[KILL OBJ TRACE] Mob {uid} does not match target faction {targetFaction} for objective {objUid}");
                    }
                }
            }
        }

        private void OnMobStateChanged(EntityUid uid, MarkedForKillComponent comp, ref MobStateChangedEvent args)
        {
            if (args.NewMobState != MobState.Dead)
                return;

            var mindContainer = EntityManager.GetComponentOrNull<MindContainerComponent>(uid);
            var mind = mindContainer?.Mind;
            Sawmill.Info($"[KILL OBJ DEBUG] OnMobStateChanged: Entity {uid} has MindContainerComponent: {mindContainer != null}, Mind: {mind != null}");

            var killedFactionComp = EntityManager.GetComponentOrNull<NpcFactionMemberComponent>(uid);
            var killedFactions = killedFactionComp?.Factions.Select(f => f.ToString().ToLowerInvariant()).ToHashSet() ?? new HashSet<string>();
            if (killedFactions.Count == 0)
                Sawmill.Warning($"[KILL OBJ WARNING] Entity {uid} killed but has no factions! Check prototype setup.");
            Sawmill.Info($"[KILL OBJ DEBUG] Entity {uid} killed. Factions: [{string.Join(",", killedFactions)}]");

            var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
            var presetId = ticker.Preset?.ID?.ToLowerInvariant();

            // To avoid modifying the dictionary while iterating, collect to remove after
            var objectivesToRemove = new List<EntityUid>();

            foreach (var (objectiveUid, factionToCredit) in comp.AssociatedObjectives)
            {
                if (!EntityManager.TryGetComponent<KillObjectiveComponent>(objectiveUid, out var killObj))
                    continue;
                if (!EntityManager.TryGetComponent<AuObjectiveComponent>(objectiveUid, out var auObj))
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
                    targetFaction = killObj.FactionToKill.ToLowerInvariant();
                }

                // Check if already completed for this faction
                if (auObj.FactionNeutral)
                {
                    if (auObj.FactionStatuses.TryGetValue(factionKey, out var status) && status == AuObjectiveComponent.ObjectiveStatus.Completed)
                    {
                        Sawmill.Info($"[KILL OBJ SKIP] Objective {objectiveUid} already completed for faction '{factionKey}'.");
                        objectivesToRemove.Add(objectiveUid);
                        continue;
                    }
                }
                else
                {
                    var assignedFaction = auObj.Faction.ToLowerInvariant();
                    if (auObj.FactionStatuses.TryGetValue(assignedFaction, out var status) && status == AuObjectiveComponent.ObjectiveStatus.Completed)
                    {
                        Sawmill.Info($"[KILL OBJ SKIP] Objective {objectiveUid} already completed for faction '{assignedFaction}'.");
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
                        Sawmill.Info($"[KILL OBJ SKIP] Entity {uid} did not have required job '{killObj.SpecificJob}' for objective {objectiveUid} at marking time.");
                        continue;
                    }
                }

                if (killObj.SynthOnly)
                {
                    if (!EntityManager.HasComponent<SynthComponent>(uid))
                    {
                        Sawmill.Info($"[KILL OBJ SKIP] Entity {uid} does not have SynthComponent for objective {objectiveUid}.");
                        continue;
                    }
                }

                if (!string.IsNullOrEmpty(killObj.MobToKill))
                {
                    var meta = EntityManager.GetComponentOrNull<MetaDataComponent>(uid);
                    var protoId = meta?.EntityPrototype?.ID ?? string.Empty;

                    if (!string.Equals(protoId, killObj.MobToKill, StringComparison.OrdinalIgnoreCase))
                    {
                        Sawmill.Info($"[KILL OBJ SKIP] Entity {uid} does not match required mob prototype '{killObj.MobToKill}' for objective {objectiveUid}.");
                        continue;
                    }
                }

                // Only increment if the killed entity matches the target faction for the objective
                if (!killedFactions.Contains(targetFaction))
                {
                    Sawmill.Info($"[KILL OBJ SKIP] Entity {uid} does not match target faction '{targetFaction}' for objective {objectiveUid} (mode={presetId}). Factions: [{string.Join(",", killedFactions)}]");
                    continue;
                }

                if (!killObj.AmountKilledPerFaction.ContainsKey(factionKey))
                    killObj.AmountKilledPerFaction[factionKey] = 0;

                // Prevent incrementing if already at or above required amount
                if (killObj.AmountKilledPerFaction[factionKey] >= killObj.AmountToKill)
                {
                    Sawmill.Info($"[KILL OBJ SKIP] Faction '{factionToCredit}' already reached required kills for objective {objectiveUid}.");
                    objectivesToRemove.Add(objectiveUid);
                    continue;
                }

                killObj.AmountKilledPerFaction[factionKey]++;
                Sawmill.Info($"[KILL OBJ UPDATE] Faction '{factionToCredit}' killed entity {uid}. Total kills: {killObj.AmountKilledPerFaction[factionKey]} / {killObj.AmountToKill}");

                if (killObj.AmountKilledPerFaction[factionKey] >= killObj.AmountToKill)
                {
                    _objectiveSystem.CompleteObjectiveForFaction(objectiveUid, auObj, factionToCredit);
                    Sawmill.Info($"[KILL OBJ COMPLETE] Objective {objectiveUid} completed for faction '{factionToCredit}'.");
                    objectivesToRemove.Add(objectiveUid);
                }
            }

            // Remove completed objectives from AssociatedObjectives
            foreach (var objUid in objectivesToRemove)
            {
                comp.AssociatedObjectives.Remove(objUid);
            }
        }
    }
}
