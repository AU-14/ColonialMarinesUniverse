using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Roles.Jobs;
using Content.Shared._CMU14.Round.Objectives.Type;
using Content.Shared._CMU14.Round.Objectives.Component;
using Content.Server._CMU14.Round.Objectives.Component;
using Content.Shared._RMC14.Synth;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Content.Shared.Mind.Components;
using Robust.Shared.Map;

namespace Content.Server._CMU14.Round.Objectives.Type;

public sealed partial class ObjKillSystem : ObjectiveSystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private JobSystem _jobSystem = default!;
    private bool _shuttingDown;

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("obj-kill");
        _shuttingDown = false;
        SubscribeLocalEvent<KillObjectiveComponent, ObjectiveActivatedEvent>(OnActivated);
        SubscribeLocalEvent<KillObjectiveComponent, ObjectiveResetEvent>(OnReset);
        SubscribeLocalEvent<ObjectiveWatchedEntityStartupEvent>(OnEntityMetaStartup);
        SubscribeLocalEvent<KillMarkedForComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Shutdown()
    {
        _shuttingDown = true;
        base.Shutdown();
    }

    private void OnActivated(EntityUid uid, KillObjectiveComponent killComp, ref ObjectiveActivatedEvent _)
    {
        if (!TryComp(uid, out CMUObjectiveComponent? comp) || !comp.Active || killComp.HasSpawned)
            return;

        if (killComp.SpawnMob && !string.IsNullOrEmpty(killComp.TargetPrototype))
            ActivateKillObjective(uid, killComp);

        var objMap = Transform(uid).MapID;
        ObjInt.RegisterInterest(uid, objMap,
            keys: string.IsNullOrEmpty(killComp.FactionToKill) ? null : new[] { killComp.FactionToKill.ToLowerInvariant() },
            wildcard: comp.FactionNeutral);

        MarkExistingEntities(uid, killComp, comp, objMap);
    }

    private void OnReset(EntityUid uid, KillObjectiveComponent comp, ref ObjectiveResetEvent args)
    {
        comp.AmountKilledPerFaction.Clear();
        if (!comp.RespawnOnRepeat)
            return;

        CleanupSpawnedByObjective(uid, ent =>
        {
            if (!TryComp(ent, out KillMarkedForComponent? marked))
                return;

            marked.AssociatedObjectives.Remove(uid);
            marked.AssociatedObjectiveJobs.Remove(uid);
            marked.CreditedObjectives.Remove(uid);
        });

        comp.HasSpawned = false;
    }

    private void ActivateKillObjective(EntityUid uid, KillObjectiveComponent killComp)
    {
        var objMap = Transform(uid).MapID;
        var markers = ResolveMarkers(objMap, killComp.SpawnMarkerId);
        var spawned = SpawnEntitiesAtMarkersWithReuse(killComp.TargetPrototype, killComp.SpawnCount, markers);
        foreach (var ent in spawned)
            EnsureComp<ObjSpawnedByComponent>(ent).ObjectiveUid = uid;
        killComp.HasSpawned = true;
    }

    private void OnEntityMetaStartup(ObjectiveWatchedEntityStartupEvent ev)
    {
        var uid = ev.Uid;
        if (_shuttingDown) return;
        if (HasComp<KillMarkedForComponent>(uid)) return;
        if (!TryComp(uid, out MetaDataComponent? meta)) return;

        var protoId = meta.EntityPrototype?.ID ?? string.Empty;
        var factions = new List<string>();
        if (TryComp<NpcFactionMemberComponent>(uid, out var factionComp))
            factions.AddRange(factionComp.Factions.Select(f => f.ToString().ToLowerInvariant()));

        var map = Transform(uid).MapID;
        var interested = ObjInt.GetInterestedObjectives(map, factions);

        foreach (var objUid in interested)
        {
            if (!TryComp(objUid, out KillObjectiveComponent? killComp)
                    || !TryComp(objUid, out CMUObjectiveComponent? auComp) || !auComp.Active)
                continue;

            string? creditFaction = GetCreditFaction(auComp, factions, killComp.FactionToKill, _gameTicker.Preset?.ID, ObjCtrl);
            if (creditFaction == null)
                continue;

            string? jobId = null;
            if (!string.IsNullOrEmpty(killComp.SpecificJob))
            {
                if (TryComp<MindContainerComponent>(uid, out var mindCont) &&
                    _jobSystem.MindTryGetJob(mindCont.Mind, out var jobProto))
                    jobId = jobProto.ID;

                if (jobId == null || !jobId.Equals(killComp.SpecificJob, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            if (killComp.SynthOnly && !HasComp<SynthComponent>(uid)) continue;
            if (!string.IsNullOrEmpty(killComp.TargetPrototype) && !protoId.Equals(killComp.TargetPrototype, StringComparison.OrdinalIgnoreCase))
                continue;

            var mark = EnsureComp<KillMarkedForComponent>(uid);
            mark.AssociatedObjectives[objUid] = creditFaction;
            if (!string.IsNullOrEmpty(killComp.SpecificJob))
                mark.AssociatedObjectiveJobs[objUid] = jobId;
        }
    }

    private void MarkExistingEntities(EntityUid uid, KillObjectiveComponent comp, CMUObjectiveComponent auComp, MapId objMap)
    {
        var searchMaps = GetZNetworkMapIds(objMap);
        var query = AllEntityQuery<MetaDataComponent, TransformComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var ent, out var meta, out var xform, out var factionComp))
        {
            if (ent == uid || !searchMaps.Contains(xform.MapID))
                continue;

            var factions = factionComp.Factions.Select(f => f.ToString().ToLowerInvariant()).ToList();
            if (factions.Count == 0) continue;

            if (!string.IsNullOrEmpty(comp.TargetPrototype) && meta.EntityPrototype?.ID != comp.TargetPrototype)
                continue;

            string? creditFaction = GetCreditFaction(auComp, factions, comp.FactionToKill, _gameTicker.Preset?.ID, ObjCtrl);
            if (creditFaction == null)
                continue;

            string? jobId = null;
            if (!string.IsNullOrEmpty(comp.SpecificJob))
            {
                if (TryComp<MindContainerComponent>(ent, out var mindCont)
                        && _jobSystem.MindTryGetJob(mindCont.Mind, out var jobProto))
                    jobId = jobProto.ID;

                if (jobId == null || !jobId.Equals(comp.SpecificJob, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            if (comp.SynthOnly && !HasComp<SynthComponent>(ent)) continue;

            var mark = EnsureComp<KillMarkedForComponent>(ent);
            mark.AssociatedObjectives[uid] = creditFaction;
            if (!string.IsNullOrEmpty(comp.SpecificJob))
                mark.AssociatedObjectiveJobs[uid] = jobId;
        }
    }

    private void OnMobStateChanged(EntityUid uid, KillMarkedForComponent comp, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var objectivesToRemove = new List<EntityUid>();
        foreach (var (objectiveUid, factionToCredit) in comp.AssociatedObjectives)
        {
            if (!TryComp(objectiveUid, out KillObjectiveComponent? killComp)
                    || !TryComp(objectiveUid, out CMUObjectiveComponent? auComp))
                continue;

            if (!comp.CreditedObjectives.Add(objectiveUid))
                continue;

            if (TryCreditObjective(objectiveUid, auComp, killComp.AmountKilledPerFaction,
                    factionToCredit.ToLowerInvariant(), killComp.KillCount))
                objectivesToRemove.Add(objectiveUid);
        }

        foreach (var o in objectivesToRemove)
        {
            comp.AssociatedObjectives.Remove(o);
            comp.AssociatedObjectiveJobs.Remove(o);
            comp.CreditedObjectives.Remove(o);
        }

        if (HasComp<ArrestMarkedForComponent>(uid) && objectivesToRemove.Any(o => TryComp(o, out KillObjectiveComponent? k) && k.CountArrest))
            RemComp<ArrestMarkedForComponent>(uid);
    }

}
