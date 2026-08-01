using Content.Shared.AU14.Objectives;
using Content.Shared.AU14.Objectives.Destroy;
using Content.Shared.AU14.Objectives.Fetch;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.AU14.Objectives.Destroy;

public sealed partial class AuDestroyObjectiveSystem : EntitySystem
{
    [Dependency] private AuObjectiveSystem _objectiveSystem = default!;

    private ISawmill _logs = default!;

    // Index: proto id (lowercase) -> list of objective uids interested in that proto
    private readonly Dictionary<string, List<EntityUid>> _protoToObjectives = new(StringComparer.OrdinalIgnoreCase);
    // Objectives that accept any entity (UseAnyEntity == true)
    private readonly List<EntityUid> _wildcardObjectives = new();

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("obj-destroy");
        SubscribeLocalEvent<DestroyObjectiveComponent, ComponentStartup>(OnObjectiveStartup);
        SubscribeLocalEvent<MarkedForDestroyComponent, EntityTerminatingEvent>(OnMarkedEntityDestroyed);
        SubscribeLocalEvent<DestroyObjectiveTrackerComponent, ComponentStartup>(OnTrackerStartup);

        // Subscribe to future entity meta component startups to index newly spawned entities efficiently
        SubscribeLocalEvent<MetaDataComponent, ComponentStartup>(OnEntityMetaStartup);
    }

    private void OnObjectiveStartup(EntityUid uid, DestroyObjectiveComponent component, ref ComponentStartup _) => StartupDestroyObjective(uid, component);

    public void ActivateDestroyObjectiveIfNeeded(EntityUid uid, AuObjectiveComponent comp)
    {
        if (!TryComp(uid, out DestroyObjectiveComponent? destroyComp))
            return;
        if (!comp.Active || destroyComp.EntitiesSpawned)
            return;

        StartupDestroyObjective(uid, destroyComp);
    }

    private void StartupDestroyObjective(EntityUid uid, DestroyObjectiveComponent component)
    {
        if (component.EntitiesSpawned)
            return;
        var objcomp = EnsureComp<AuObjectiveComponent>(uid);
        if (!objcomp.Active)
            return;

        // Destroy objectives cannot be faction-neutral
        if (objcomp.FactionNeutral)
        {
            _logs.Warning($"[DESTROY OBJ] Objective ({uid}) is faction-neutral which is invalid for destroy objectives. Deactivating...");
            objcomp.Active = false;
            return;
        }

        var entityToSpawn = component.EntityToDestroy;
        var markerId = component.SpawnMarker;
        var amount = component.AmountToSpawn;

        var markers = new List<EntityUid>();
        var genericMarkers = new List<EntityUid>();
        var objMap = Transform(uid).MapID;
        var markerQuery = AllEntityQuery<FetchObjectiveMarkerComponent, TransformComponent>();
        while (markerQuery.MoveNext(out var markerUid, out var markerComp, out var markerXform))
        {
            if (markerComp.Used || markerXform.MapID != objMap)
                continue;
            if (!string.IsNullOrEmpty(markerId) && markerComp.FetchId == markerId)
                markers.Add(markerUid);
            else if (markerComp.Generic)
                genericMarkers.Add(markerUid);
        }

        if (markers.Count == 0)
            markers = genericMarkers;

        if (markers.Count == 0 || string.IsNullOrEmpty(entityToSpawn))
            return;

        int toSpawn = Math.Min(amount, markers.Count);
        for (var i = 0; i < toSpawn; i++)
        {
            var markerUid = markers[i];
            var markerComp = Comp<FetchObjectiveMarkerComponent>(markerUid);
            if (markerComp.Used)
                continue;
            var xform = Comp<TransformComponent>(markerUid);
            var ent = Spawn(entityToSpawn, xform.Coordinates);
            var tracker = EnsureComp<DestroyObjectiveTrackerComponent>(ent);
            tracker.ObjectiveUid = uid;
            markerComp.Used = true;
            // spawnOther removed by design
        }
        component.EntitiesSpawned = true;

        // Register interest in proto or wildcard for efficient marking
        RegisterObjectiveInterest(uid, component);

        // Initial scan: only check entities of the protos we're interested in OR wildcard ones
        var objXform = Comp<TransformComponent>(uid);
        var metaQuery = AllEntityQuery<MetaDataComponent, TransformComponent>();
        while (metaQuery.MoveNext(out var entUid, out var meta, out var entXform))
        {
            if (entUid == uid)
                continue;
            if (entXform.MapID != objMap)
                continue;
            var proto = meta.EntityPrototype?.ID ?? string.Empty;
            if (component.UseAnyEntity)
            {
                if (!string.IsNullOrEmpty(component.EntityToDestroy) && !string.Equals(component.EntityToDestroy, proto, StringComparison.OrdinalIgnoreCase))
                    continue;
                var mark = EnsureComp<MarkedForDestroyComponent>(entUid);
                mark.AssociatedObjectives[uid] = objcomp.Faction.ToLowerInvariant();
                continue;
            }

            if (!string.IsNullOrEmpty(component.EntityToDestroy))
            {
                if (string.Equals(component.EntityToDestroy, proto, StringComparison.OrdinalIgnoreCase))
                {
                    var mark = EnsureComp<MarkedForDestroyComponent>(entUid);
                    mark.AssociatedObjectives[uid] = objcomp.Faction.ToLowerInvariant();
                }
            }
        }
    }

    private void RegisterObjectiveInterest(EntityUid uid, DestroyObjectiveComponent comp)
    {
        // Remove existing registration if present to avoid duplicates
        UnregisterObjectiveInterest(uid);

        if (comp.UseAnyEntity)
        {
            _wildcardObjectives.Add(uid);
            return;
        }

        if (!string.IsNullOrEmpty(comp.EntityToDestroy))
        {
            var key = comp.EntityToDestroy.ToLowerInvariant();
            if (!_protoToObjectives.TryGetValue(key, out var list))
            {
                list = new List<EntityUid>();
                _protoToObjectives[key] = list;
            }
            list.Add(uid);
        }
    }

    private void UnregisterObjectiveInterest(EntityUid uid)
    {
        _wildcardObjectives.Remove(uid);
        foreach (var kv in _protoToObjectives)
        {
            kv.Value.Remove(uid);
        }
    }

    private void OnEntityMetaStartup(EntityUid uid, MetaDataComponent comp, ref ComponentStartup args)
    {
        // Avoid marking objectives themselves
        var proto = comp.EntityPrototype?.ID ?? string.Empty;
        if (string.IsNullOrEmpty(proto))
            return;

        var protoKey = proto.ToLowerInvariant();

        // Mark for specific objectives
        if (_protoToObjectives.TryGetValue(protoKey, out var objectives))
        {
            foreach (var objUid in objectives)
            {
                if (!TryComp(objUid, out AuObjectiveComponent? auObj) || !auObj.Active)
                    continue;
                // Check map compatibility
                if (Transform(uid).MapID != Transform(objUid).MapID)
                    continue;
                var mark = EnsureComp<MarkedForDestroyComponent>(uid);
                mark.AssociatedObjectives[objUid] = auObj.Faction.ToLowerInvariant();
            }
        }

        // Mark for wildcard objectives
        if (_wildcardObjectives.Count > 0)
        {
            foreach (var objUid in _wildcardObjectives)
            {
                if (!TryComp(objUid, out AuObjectiveComponent? auObj) || !auObj.Active)
                    continue;
                // Map compatibility guard
                if (Transform(uid).MapID != Transform(objUid).MapID)
                    continue;
                // If the wildcard objective also specifies a proto filter, respect it
                if (TryComp(objUid, out DestroyObjectiveComponent? destroyComp)
                    && !string.IsNullOrEmpty(destroyComp.EntityToDestroy)
                    && !string.Equals(destroyComp.EntityToDestroy, proto, StringComparison.OrdinalIgnoreCase))
                    continue;
                var mark = EnsureComp<MarkedForDestroyComponent>(uid);
                mark.AssociatedObjectives[objUid] = auObj.Faction.ToLowerInvariant();
            }
        }
    }

    private void OnTrackerStartup(EntityUid uid, DestroyObjectiveTrackerComponent comp, ref ComponentStartup args)
    {
        Timer.Spawn(TimeSpan.FromMilliseconds(200), () =>
        {
            if (!Exists(uid))
                return;
            TryMarkForDestroyDelayed(uid);
        });
    }

    private void TryMarkForDestroyDelayed(EntityUid uid)
    {
        TryComp(uid, out MetaDataComponent? meta);
        var protoId = meta?.EntityPrototype?.ID ?? string.Empty;
        if (string.IsNullOrEmpty(protoId))
            return;

        var protoKey = protoId.ToLowerInvariant();

        // First handle specific proto objectives
        if (_protoToObjectives.TryGetValue(protoKey, out var objList))
        {
            foreach (var objUid in objList)
            {
                if (!TryComp(objUid, out AuObjectiveComponent? auObj) || !auObj.Active)
                    continue;
                if (Transform(uid).MapID != Transform(objUid).MapID)
                    continue;

                var mark = EnsureComp<MarkedForDestroyComponent>(uid);
                mark.AssociatedObjectives[objUid] = auObj.Faction.ToLowerInvariant();
            }
        }

        // Then handle wildcard objectives
        foreach (var objUid in _wildcardObjectives)
        {
            if (!TryComp(objUid, out AuObjectiveComponent? auObj) || !auObj.Active)
                continue;
            if (Transform(uid).MapID != Transform(objUid).MapID)
                continue;
            if (TryComp(objUid, out DestroyObjectiveComponent? destroyComp)
                && !string.IsNullOrEmpty(destroyComp.EntityToDestroy)
                && !string.Equals(destroyComp.EntityToDestroy, protoId, StringComparison.OrdinalIgnoreCase))
                continue;
            var mark = EnsureComp<MarkedForDestroyComponent>(uid);
            mark.AssociatedObjectives[objUid] = auObj.Faction.ToLowerInvariant();
        }
    }

    private void OnMarkedEntityDestroyed(EntityUid uid, MarkedForDestroyComponent comp, ref EntityTerminatingEvent args)
    {
        TryComp(uid, out MetaDataComponent? meta);
        var objectivesToRemove = new List<EntityUid>();
        foreach (var kv in comp.AssociatedObjectives)
        {
            var objectiveUid = kv.Key;
            var factionToCredit = kv.Value;

            if (!TryComp(objectiveUid, out DestroyObjectiveComponent? destroyComp))
                continue;
            var auObj = EnsureComp<AuObjectiveComponent>(objectiveUid);
            var factionKey = factionToCredit.ToLowerInvariant();

            destroyComp.AmountDestroyed++;
#if DEBUG
            _logs.Debug($"[DESTROY DEBUG] Objective ({objectiveUid}) counted destruction of proto '{meta?.EntityPrototype?.ID ?? string.Empty}' for faction '{factionKey}'. Total: {destroyComp.AmountDestroyed}/{destroyComp.AmountToDestroy}");
#endif
            if (destroyComp.AmountDestroyed >= destroyComp.AmountToDestroy)
            {
                _logs.Info($"[DESTROY DEBUG] Objective ({objectiveUid}) completed for faction '{factionKey}'!");
                _objectiveSystem.CompleteObjectiveForFaction(objectiveUid, auObj, factionToCredit);
                objectivesToRemove.Add(objectiveUid);

                // Clean up indexing so future entities don't get marked
                UnregisterObjectiveInterest(objectiveUid);
            }
        }

        foreach (var objUid in objectivesToRemove)
        {
            comp.AssociatedObjectives.Remove(objUid);
        }
    }
}
