using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Roles.Jobs;
using Content.Shared.CMU14.Round.Objectives.Type;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Server.CMU14.Round.Objectives.Components;
using Content.Shared._RMC14.Synth;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared._RMC14.Marines;
using Content.Shared.Projectiles;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Shared.Mind.Components;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Round.Objectives.Type;

public sealed partial class ObjKillSystem : ObjectiveSystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private JobSystem _jobSystem = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedContainerSystem _container = default!;
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
        if (!TryComp(uid, out CMUObjectiveComponent? comp) || !comp.Active)
            return;

        if (!killComp.HasSpawned && killComp.SpawnMob && !string.IsNullOrEmpty(killComp.TargetPrototype))
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
        if (!TryComp(uid, out MetaDataComponent? meta)) return;

        var protoId = meta.EntityPrototype?.ID ?? string.Empty;
        var factions = new List<string>();
        if (TryComp<NpcFactionMemberComponent>(uid, out var factionComp))
            factions.AddRange(GetFactionsWithAncestors(factionComp));

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
                if (TryComp<ThreatComponent>(uid, out var threat) && threat.ObjectiveJob is { } threatJob
                    && threatJob.Id.Equals(killComp.SpecificJob, StringComparison.OrdinalIgnoreCase))
                    jobId = threatJob.Id;
                else if (TryComp<MindContainerComponent>(uid, out var mindCont) &&
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
        var searchMaps = _zLevels.GetAllNetworkMapIds(objMap);
        var query = AllEntityQuery<MetaDataComponent, TransformComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var ent, out var meta, out var xform, out var factionComp))
        {
            if (ent == uid || !searchMaps.Contains(xform.MapID))
                continue;

            var factions = GetFactionsWithAncestors(factionComp);
            if (factions.Count == 0) continue;

            if (!string.IsNullOrEmpty(comp.TargetPrototype) && meta.EntityPrototype?.ID != comp.TargetPrototype)
                continue;

            string? creditFaction = GetCreditFaction(auComp, factions, comp.FactionToKill, _gameTicker.Preset?.ID, ObjCtrl);
            if (creditFaction == null)
                continue;

            string? jobId = null;
            if (!string.IsNullOrEmpty(comp.SpecificJob))
            {
                if (TryComp<ThreatComponent>(ent, out var threat) && threat.ObjectiveJob is { } threatJob
                    && threatJob.Id.Equals(comp.SpecificJob, StringComparison.OrdinalIgnoreCase))
                    jobId = threatJob.Id;
                else if (TryComp<MindContainerComponent>(ent, out var mindCont)
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

    /// <summary>
    /// An entity's factions plus every faction they inherit from, lowercased. Xenos, apes and other
    /// threat creatures carry a child faction (e.g. RMCXeno) whose parent is THREAT, so a "kill THREAT"
    /// objective has to look up the chain or it only ever sees the few bodies tagged at round start.
    /// </summary>
    private List<string> GetFactionsWithAncestors(NpcFactionMemberComponent factionComp)
    {
        var result = new List<string>();
        var pending = new Stack<string>();
        foreach (var faction in factionComp.Factions)
            pending.Push(faction.Id);

        while (pending.TryPop(out var id))
        {
            var key = id.ToLowerInvariant();
            if (result.Contains(key))
                continue;

            result.Add(key);
            if (_proto.TryIndex<NpcFactionPrototype>(id, out var proto) && proto.Parents is { } parents)
            {
                foreach (var parent in parents)
                    pending.Push(parent);
            }
        }

        return result;
    }

    private void OnMobStateChanged(EntityUid uid, KillMarkedForComponent comp, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || TerminatingOrDeleted(uid))
            return;

        var origin = args.Origin;
        var objectivesToRemove = new List<EntityUid>();
        foreach (var (objectiveUid, factionToCredit) in comp.AssociatedObjectives)
        {
            if (!TryComp(objectiveUid, out KillObjectiveComponent? killComp)
                    || !TryComp(objectiveUid, out CMUObjectiveComponent? auComp))
                continue;

            // Completed or capped objectives stay marked on entities; without this check their
            // counters keep climbing on every death long after the objective stopped scoring.
            if (!auComp.Active)
                continue;

            // Only a kill by the credited faction counts. The body stays marked, so if it's revived and
            // then killed by that faction later, it still scores.
            if (killComp.RequireFactionKill && !IsKillerInFaction(origin, factionToCredit.ToLowerInvariant()))
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

    /// <summary>
    /// Whether whatever caused a death belongs to <paramref name="faction"/>. Follows a projectile back to its
    /// shooter, and a held or worn weapon back to its wielder.
    /// </summary>
    private bool IsKillerInFaction(EntityUid? origin, string faction)
    {
        if (origin is not { } current)
            return false;

        for (var depth = 0; depth < 4 && !TerminatingOrDeleted(current); depth++)
        {
            if (TryComp(current, out NpcFactionMemberComponent? factions) &&
                GetFactionsWithAncestors(factions).Contains(faction))
            {
                return true;
            }

            if (TryComp(current, out MarineComponent? marine) &&
                string.Equals(marine.Faction, faction, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (TryComp(current, out ProjectileComponent? projectile) && projectile.Shooter is { } shooter)
            {
                current = shooter;
                continue;
            }

            if (!_container.TryGetContainingContainer(current, out var container))
                return false;

            current = container.Owner;
        }

        return false;
    }

}
