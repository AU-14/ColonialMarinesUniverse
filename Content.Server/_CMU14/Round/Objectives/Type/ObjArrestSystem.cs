using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Roles.Jobs;
using Content.Shared._CMU14.Round.Objectives.Type;
using Content.Shared._CMU14.Round.Objectives.Components;
using Content.Server._CMU14.Round.Objectives.Components;
using Content.Shared._RMC14.Synth;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Mind.Components;
using Robust.Shared.Map;

namespace Content.Server._CMU14.Round.Objectives.Type;

public sealed partial class ObjArrestSystem : ObjectiveSystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private JobSystem _jobSystem = default!;
    [Dependency] private SharedCuffableSystem _cuffableSystem = default!;
    private bool _shuttingDown;

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("obj-arrest");
        _shuttingDown = false;
        SubscribeLocalEvent<ArrestObjectiveComponent, ObjectiveActivatedEvent>(OnActivated);
        SubscribeLocalEvent<ArrestObjectiveComponent, ObjectiveResetEvent>(OnReset);
        SubscribeLocalEvent<ObjectiveWatchedEntityStartupEvent>(OnEntityMetaStartup);
        SubscribeLocalEvent<ArrestMarkedForComponent, CuffedStateChangeEvent>(OnCuffStateChanged);
    }

    public override void Shutdown()
    {
        _shuttingDown = true;
        base.Shutdown();
    }

    private void OnActivated(EntityUid uid, ArrestObjectiveComponent arrestComp, ref ObjectiveActivatedEvent _)
    {
        if (!TryComp(uid, out CMUObjectiveComponent? comp) || !comp.Active || arrestComp.HasSpawned)
            return;

        if (arrestComp.SpawnMob && !string.IsNullOrEmpty(arrestComp.TargetPrototype))
            ActivateArrestObjective(uid, arrestComp);

        var objMap = Transform(uid).MapID;
        ObjInt.RegisterInterest(uid, objMap,
            keys: string.IsNullOrEmpty(arrestComp.FactionToArrest) ? null : new[] { arrestComp.FactionToArrest.ToLowerInvariant() },
            wildcard: comp.FactionNeutral);

        MarkExistingEntities(uid, arrestComp, comp, objMap);
    }

    private void OnReset(EntityUid uid, ArrestObjectiveComponent comp, ref ObjectiveResetEvent args)
    {
        comp.AmountArrestedPerFaction.Clear();
        if (!comp.RespawnOnRepeat)
            return;

        CleanupSpawnedByObjective(uid, ent =>
        {
            if (!TryComp(ent, out ArrestMarkedForComponent? marked))
                return;

            marked.AssociatedObjectives.Remove(uid);
            marked.AssociatedObjectiveJobs.Remove(uid);
            marked.CreditedObjectives.Remove(uid);
        });

        comp.HasSpawned = false;
    }

    private void ActivateArrestObjective(EntityUid uid, ArrestObjectiveComponent arrestComp)
    {
        var objMap = Transform(uid).MapID;
        var markers = ResolveMarkers(objMap, arrestComp.SpawnMarkerId);
        var spawned = SpawnEntitiesAtMarkersWithReuse(arrestComp.TargetPrototype, arrestComp.SpawnCount, markers);
        foreach (var ent in spawned)
            EnsureComp<ObjSpawnedByComponent>(ent).ObjectiveUid = uid;
        arrestComp.HasSpawned = true;
    }

    private void OnEntityMetaStartup(ObjectiveWatchedEntityStartupEvent ev)
    {
        var uid = ev.Uid;
        if (_shuttingDown) return;
        if (HasComp<ArrestMarkedForComponent>(uid)) return;
        if (!TryComp(uid, out MetaDataComponent? meta)) return;

        var protoId = meta.EntityPrototype?.ID ?? string.Empty;
        var factions = new List<string>();
        if (TryComp<NpcFactionMemberComponent>(uid, out var factionComp))
            factions.AddRange(factionComp.Factions.Select(f => f.ToString().ToLowerInvariant()));

        var map = Transform(uid).MapID;
        var interested = ObjInt.GetInterestedObjectives(map, factions);

        foreach (var objUid in interested)
        {
            if (!TryComp(objUid, out ArrestObjectiveComponent? arrestComp)
                    || !TryComp(objUid, out CMUObjectiveComponent? auComp) || !auComp.Active)
                continue;

            string? creditFaction = GetCreditFaction(auComp, factions, arrestComp.FactionToArrest, _gameTicker.Preset?.ID, ObjCtrl);
            if (creditFaction == null) continue;

            string? jobId = null;
            if (!string.IsNullOrEmpty(arrestComp.SpecificJob))
            {
                if (TryComp<MindContainerComponent>(uid, out var mindCont)
                        && _jobSystem.MindTryGetJob(mindCont.Mind, out var jobProto))
                    jobId = jobProto.ID;

                if (jobId == null || !jobId.Equals(arrestComp.SpecificJob, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            if (arrestComp.SynthOnly && !HasComp<SynthComponent>(uid)) continue;
            if (!string.IsNullOrEmpty(arrestComp.TargetPrototype) && !protoId.Equals(arrestComp.TargetPrototype, StringComparison.OrdinalIgnoreCase))
                continue;

            var mark = EnsureComp<ArrestMarkedForComponent>(uid);
            mark.AssociatedObjectives[objUid] = creditFaction;
            if (!string.IsNullOrEmpty(arrestComp.SpecificJob))
                mark.AssociatedObjectiveJobs[objUid] = jobId;
        }
    }

    private void MarkExistingEntities(EntityUid uid, ArrestObjectiveComponent comp, CMUObjectiveComponent auComp, MapId objMap)
    {
        var query = AllEntityQuery<MetaDataComponent, TransformComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var ent, out var meta, out var xform, out var factionComp))
        {
            if (ent == uid || xform.MapID != objMap)
                continue;

            var factions = factionComp.Factions.Select(f => f.ToString().ToLowerInvariant()).ToList();
            if (factions.Count == 0) continue;

            if (!string.IsNullOrEmpty(comp.TargetPrototype) && meta.EntityPrototype?.ID != comp.TargetPrototype)
                continue;

            string? creditFaction = GetCreditFaction(auComp, factions, comp.FactionToArrest, _gameTicker.Preset?.ID, ObjCtrl);
            if (creditFaction == null) continue;

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

            var mark = EnsureComp<ArrestMarkedForComponent>(ent);
            mark.AssociatedObjectives[uid] = creditFaction;
            if (!string.IsNullOrEmpty(comp.SpecificJob))
                mark.AssociatedObjectiveJobs[uid] = jobId;
        }
    }

    private void OnCuffStateChanged(EntityUid uid, ArrestMarkedForComponent comp, ref CuffedStateChangeEvent args)
    {
        if (!TryComp<CuffableComponent>(uid, out var cuffable) || !_cuffableSystem.IsCuffed((uid, cuffable), requireFullyCuffed: false))
            return;

        var objectivesToRemove = new List<EntityUid>();
        foreach (var (objectiveUid, factionToCredit) in comp.AssociatedObjectives)
        {
            if (!TryComp(objectiveUid, out ArrestObjectiveComponent? arrestComp)
                    || !TryComp(objectiveUid, out CMUObjectiveComponent? auComp))
                continue;

            if (!comp.CreditedObjectives.Add(objectiveUid))
                continue;

            if (TryCreditObjective(objectiveUid, auComp, arrestComp.AmountArrestedPerFaction,
                    factionToCredit.ToLowerInvariant(), arrestComp.ArrestCount))
                objectivesToRemove.Add(objectiveUid);
        }

        foreach (var o in objectivesToRemove)
        {
            comp.AssociatedObjectives.Remove(o);
            comp.AssociatedObjectiveJobs.Remove(o);
            comp.CreditedObjectives.Remove(o);
        }

        if (HasComp<KillMarkedForComponent>(uid) && objectivesToRemove.Any(o => TryComp(o, out ArrestObjectiveComponent? a) && a.RemoveKillMark))
            RemComp<KillMarkedForComponent>(uid);
    }
}
