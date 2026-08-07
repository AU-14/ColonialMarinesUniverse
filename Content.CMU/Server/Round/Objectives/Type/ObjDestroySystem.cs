using Content.Shared._CMU14.Round.Objectives.Component;
using Content.Server._CMU14.Round.Objectives.Component;
using Content.Shared._CMU14.Round.Objectives.Type;
using Robust.Shared.Map;

namespace Content.Server._CMU14.Round.Objectives.Type;

public sealed partial class ObjDestroySystem : ObjectiveSystem
{
    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("obj-destroy");
        SubscribeLocalEvent<DestroyObjectiveComponent, ObjectiveActivatedEvent>(OnActivated);
        SubscribeLocalEvent<DestroyObjectiveComponent, ObjectiveResetEvent>(OnReset);
        SubscribeLocalEvent<ObjectiveWatchedEntityStartupEvent>(OnEntityMetaStartup);
        SubscribeLocalEvent<DestroyMarkedForComponent, EntityTerminatingEvent>(OnMarkedEntityDestroyed);
    }

    private void OnActivated(EntityUid uid, DestroyObjectiveComponent destroyComp, ref ObjectiveActivatedEvent _)
    {
        if (!TryComp(uid, out CMUObjectiveComponent? comp) || !comp.Active)
            return;

        ActivateDestroyObjective(uid, comp);
    }

    private void OnReset(EntityUid uid, DestroyObjectiveComponent comp, ref ObjectiveResetEvent args)
    {
        CleanupSpawnedByObjective(uid, ent =>
        {
            if (!TryComp(ent, out DestroyMarkedForComponent? marked))
                return;

            marked.AssociatedObjectives.Remove(uid);
            if (marked.AssociatedObjectives.Count == 0)
                RemComp<DestroyMarkedForComponent>(ent);
        });

        comp.AmountDestroyedPerFaction.Clear();
        comp.HasSpawned = false;
    }

    private void ActivateDestroyObjective(EntityUid uid, CMUObjectiveComponent comp)
    {
        var destroyComp = Comp<DestroyObjectiveComponent>(uid);
        if (destroyComp.HasSpawned)
            return;

        var objMap = Transform(uid).MapID;
        var markers = ResolveMarkers(objMap, destroyComp.SpawnMarkerId);
        var spawned = SpawnEntitiesAtMarkers(destroyComp.TargetPrototype, destroyComp.SpawnCount, markers);
        destroyComp.HasSpawned = true;

        foreach (var ent in spawned)
            EnsureComp<ObjSpawnedByComponent>(ent).ObjectiveUid = uid;

        ObjInt.RegisterInterest(uid, objMap,
            keys: string.IsNullOrEmpty(destroyComp.TargetPrototype) ? null : new[] { destroyComp.TargetPrototype },
            wildcard: destroyComp.UseAnyEntity);

        MarkExistingEntities(uid, destroyComp, comp, objMap);
    }

    private void MarkExistingEntities(EntityUid uid, DestroyObjectiveComponent comp, CMUObjectiveComponent auComp, MapId objMap)
    {
        var query = AllEntityQuery<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var meta, out var xform))
        {
            if (ent == uid || xform.MapID != objMap)
                continue;

            var proto = meta.EntityPrototype?.ID ?? string.Empty;
            if (comp.UseAnyEntity)
            {
                if (!string.IsNullOrEmpty(comp.TargetPrototype)
                        && !string.Equals(comp.TargetPrototype, proto, StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            else if (!string.Equals(comp.TargetPrototype, proto, StringComparison.OrdinalIgnoreCase))
                continue;

            var creditFaction = GetDestroyCreditFaction(auComp);
            if (creditFaction == null)
                continue;

            var mark = EnsureComp<DestroyMarkedForComponent>(ent);
            mark.AssociatedObjectives[uid] = creditFaction;
        }
    }

    private static string? GetDestroyCreditFaction(CMUObjectiveComponent auComp)
    {
        if (auComp.FactionNeutral || string.IsNullOrEmpty(auComp.Faction))
            return null;

        return auComp.Faction.ToLowerInvariant();
    }

    private void OnEntityMetaStartup(ObjectiveWatchedEntityStartupEvent ev)
    {
        var uid = ev.Uid;
        if (!TryComp(uid, out MetaDataComponent? meta))
            return;

        var proto = meta.EntityPrototype?.ID;
        if (string.IsNullOrEmpty(proto))
            return;

        var map = Transform(uid).MapID;
        var interested = ObjInt.GetInterestedObjectives(map, [proto]);
        foreach (var objUid in interested)
        {
            if (!TryComp(objUid, out CMUObjectiveComponent? auComp) || !auComp.Active)
                continue;

            if (TryComp(objUid, out DestroyObjectiveComponent? destroyComp)
                    && !string.IsNullOrEmpty(destroyComp.TargetPrototype)
                    && !string.Equals(destroyComp.TargetPrototype, proto, StringComparison.OrdinalIgnoreCase))
                continue;

            if (TryComp(uid, out DestroyMarkedForComponent? existing) && existing.AssociatedObjectives.ContainsKey(objUid))
                continue;

            var creditFaction = GetDestroyCreditFaction(auComp);
            if (creditFaction == null)
                continue;

            var mark = EnsureComp<DestroyMarkedForComponent>(uid);
            mark.AssociatedObjectives[objUid] = creditFaction;
        }
    }

    private void OnMarkedEntityDestroyed(EntityUid uid, DestroyMarkedForComponent comp, ref EntityTerminatingEvent args)
    {
        var objectivesToRemove = new List<EntityUid>();
        foreach (var (objectiveUid, factionToCredit) in comp.AssociatedObjectives)
        {
            if (!TryComp(objectiveUid, out DestroyObjectiveComponent? destroyComp)
                    || !TryComp(objectiveUid, out CMUObjectiveComponent? auComp))
                continue;

            if (!TryCreditObjective(objectiveUid, auComp, destroyComp.AmountDestroyedPerFaction,
                    factionToCredit.ToLowerInvariant(), destroyComp.DestroyCount))
                continue;

            objectivesToRemove.Add(objectiveUid);
        }
        foreach (var o in objectivesToRemove)
            comp.AssociatedObjectives.Remove(o);
    }
}
