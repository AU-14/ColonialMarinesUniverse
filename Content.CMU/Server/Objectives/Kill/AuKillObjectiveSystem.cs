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

            var query = EntityManager.EntityQueryEnumerator<KillObjectiveComponent>();
            while (query.MoveNext(out var objUid, out var killObj))
            {
                if (EntityManager.EnsureComponent<AuObjectiveComponent>(objUid) is not { } auObj)
                    continue;

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
                        continue;
                }

                Sawmill.Info($"[KILL OBJ TRACE] (DELAYED) Mob {uid} proto={protoId} factions=[{string.Join(",", factions)}]");
                Sawmill.Info($"[KILL OBJ TRACE] Objective faction: {auObj.Faction.ToLowerInvariant()}");

                if(factions.Contains(auObj.Faction.ToLowerInvariant()))
                {
                    Sawmill.Info($"[KILL OBJ TRACE] Mob {uid} matches objective {objUid} for faction {auObj.Faction}");
                    var mark = EnsureComp<MarkedForKillComponent>(uid);
                    mark.AssociatedObjectives[objUid] = auObj.Faction.ToLowerInvariant();
                }
                else
                {
                    Sawmill.Info($"[KILL OBJ TRACE] Mob {uid} does not match objective {objUid} for faction {auObj.Faction}");
                }
            }
        }

        private void OnMobStateChanged(EntityUid uid, MarkedForKillComponent comp, ref MobStateChangedEvent args)
        {
            if (args.NewMobState != MobState.Dead)
                return;

            var killedFactionComp = EntityManager.GetComponentOrNull<NpcFactionMemberComponent>(uid);
            var killedFactions = killedFactionComp?.Factions.Select(f => f.ToString().ToLowerInvariant()).ToHashSet() ?? new HashSet<string>();
            if (killedFactions.Count == 0)
                Sawmill.Warning($"[KILL OBJ WARNING] Entity {uid} killed but has no factions! Check prototype setup.");
            Sawmill.Info($"[KILL OBJ DEBUG] Entity {uid} killed. Factions: [{string.Join(",", killedFactions)}]");

            var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
            var presetId = ticker.Preset?.ID?.ToLowerInvariant();

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

                if (!auObj.FactionNeutral && !string.IsNullOrEmpty(killObj.SpecificJob))
                {
                    // Retrieve the MindContainerComponent from the killed entity
                    if (!_entityManager.TryGetComponent<MindContainerComponent>(uid, out var mindContainer) || mindContainer.Mind == null)
                    {
                        Sawmill.Info($"[KILL OBJ SKIP] Entity {uid} does not have a MindContainerComponent or Mind for objective {objectiveUid}.");
                        continue;
                    }
                    // Only increment if the killed entity has the correct job
                    if (!_jobSystem.MindTryGetJob(mindContainer.Mind.Value, out var jobPrototype) || jobPrototype.ID?.ToLowerInvariant() != killObj.SpecificJob.ToLowerInvariant())
                    {
                        Sawmill.Info($"[KILL OBJ SKIP] Entity {uid} does not have required job '{killObj.SpecificJob}' for objective {objectiveUid}.");
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

                killObj.AmountKilledPerFaction[factionKey]++;
                Sawmill.Info($"[KILL OBJ UPDATE] Faction '{factionToCredit}' killed entity {uid}. Total kills: {killObj.AmountKilledPerFaction[factionKey]} / {killObj.AmountToKill}");

                if (killObj.AmountKilledPerFaction[factionKey] >= killObj.AmountToKill)
                {
                    _objectiveSystem.CompleteObjectiveForFaction(objectiveUid, auObj, factionToCredit);
                    Sawmill.Info($"[KILL OBJ COMPLETE] Objective {objectiveUid} completed for faction '{factionToCredit}'.");

                    comp.AssociatedObjectives.Remove(objectiveUid);
                }
            }
        }
    }
}
