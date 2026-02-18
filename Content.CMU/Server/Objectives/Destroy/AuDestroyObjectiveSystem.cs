using System;
using System.Collections.Generic;
using Content.Shared.AU14.Objectives.Destroy;
using Content.Shared.AU14.Objectives;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Log;
using System.Runtime.CompilerServices;
using Robust.Shared.Timing;

namespace Content.Server.AU14.Objectives.Destroy;

public sealed class AuDestroyObjectiveSystem : EntitySystem
{
    [Robust.Shared.IoC.Dependency] private readonly IEntityManager _entManager = default!;
    [Robust.Shared.IoC.Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Robust.Shared.IoC.Dependency] private readonly AuObjectiveSystem _objectiveSystem = default!;
    [Robust.Shared.IoC.Dependency] private readonly SharedTransformSystem _xformSys = default!;

    private static readonly ISawmill Sawmill = Logger.GetSawmill("au14-destroyobj");

    // Index: proto id (lowercase) -> list of objective uids interested in that proto
    private readonly Dictionary<string, List<EntityUid>> _protoToObjectives = new(StringComparer.OrdinalIgnoreCase);
    // Objectives that accept any entity (UseAnyEntity == true)
    private readonly List<EntityUid> _wildcardObjectives = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DestroyObjectiveComponent, ComponentStartup>(OnObjectiveStartup);
        SubscribeLocalEvent<MarkedForDestroyComponent, EntityTerminatingEvent>(OnMarkedEntityDestroyed);
        SubscribeLocalEvent<DestroyObjectiveTrackerComponent, ComponentStartup>(OnTrackerStartup);

        // Subscribe to future entity meta component startups to index newly spawned entities efficiently
        SubscribeLocalEvent<MetaDataComponent, ComponentStartup>(OnEntityMetaStartup);
    }

    public void ActivateDestroyObjectiveIfNeeded(EntityUid uid, AuObjectiveComponent comp)
    {
        if (!_entManager.TryGetComponent(uid, out DestroyObjectiveComponent? destroyComp))
            return;
        if (!comp.Active || destroyComp.EntitiesSpawned)
            return;
        OnObjectiveStartup(uid, destroyComp, ref Unsafe.NullRef<ComponentStartup>());
    }

    private void OnObjectiveStartup(EntityUid uid, DestroyObjectiveComponent component, ref ComponentStartup args)
    {
        if (component.EntitiesSpawned)
            return;
        var objcomp = EnsureComp<AuObjectiveComponent>(uid);
        if (!objcomp.Active)
            return;

        // Destroy objectives cannot be faction-neutral
        if (objcomp.FactionNeutral)
        {
            Sawmill.Warning($"[DESTROY OBJ] Objective {uid} is faction-neutral which is invalid for destroy objectives. Deactivating.");
            objcomp.Active = false;
            return;
        }

        var entityToSpawn = component.EntityToDestroy;
        var markerId = component.SpawnMarker;
        var amount = component.AmountToSpawn;

        var markers = new List<EntityUid>();
        var genericMarkers = new List<EntityUid>();
        var markerQuery = EntityManager.AllEntityQueryEnumerator<Content.Shared.AU14.Objectives.Fetch.FetchObjectiveMarkerComponent, TransformComponent>();
        while (markerQuery.MoveNext(out var markerUid, out var markerComp, out _))
        {
            if (markerComp.Used)
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
            var markerComp = EntityManager.GetComponent<Content.Shared.AU14.Objectives.Fetch.FetchObjectiveMarkerComponent>(markerUid);
            if (markerComp.Used)
                continue;
            var xform = EntityManager.GetComponent<TransformComponent>(markerUid);
            var ent = EntityManager.SpawnEntity(entityToSpawn, xform.Coordinates);
            var tracker = _entManager.EnsureComponent<DestroyObjectiveTrackerComponent>(ent);
            tracker.ObjectiveUid = uid;
            markerComp.Used = true;
            // spawnOther removed by design
        }
        component.EntitiesSpawned = true;

        // Register interest in proto or wildcard for efficient marking
        RegisterObjectiveInterest(uid, component, objcomp);

        // Initial scan: only check entities of the protos we're interested in OR wildcard ones
        var objXform = EntityManager.GetComponent<TransformComponent>(uid);
        var objMap = objXform.MapID;

        var metaQuery = EntityManager.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
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
                var mark = _entManager.EnsureComponent<MarkedForDestroyComponent>(entUid);
                mark.AssociatedObjectives[uid] = objcomp.Faction.ToLowerInvariant();
                continue;
            }

            if (!string.IsNullOrEmpty(component.EntityToDestroy))
            {
                if (string.Equals(component.EntityToDestroy, proto, StringComparison.OrdinalIgnoreCase))
                {
                    var mark = _entManager.EnsureComponent<MarkedForDestroyComponent>(entUid);
                    mark.AssociatedObjectives[uid] = objcomp.Faction.ToLowerInvariant();
                }
            }
        }
    }

    private void RegisterObjectiveInterest(EntityUid uid, DestroyObjectiveComponent comp, AuObjectiveComponent auComp)
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
                var auObj = EntityManager.GetComponentOrNull<AuObjectiveComponent>(objUid);
                if (auObj == null || !auObj.Active)
                    continue;
                var mark = _entManager.EnsureComponent<MarkedForDestroyComponent>(uid);
                mark.AssociatedObjectives[objUid] = auObj.Faction.ToLowerInvariant();
            }
        }

        // Mark for wildcard objectives
        if (_wildcardObjectives.Count > 0)
        {
            foreach (var objUid in _wildcardObjectives)
            {
                var auObj = EntityManager.GetComponentOrNull<AuObjectiveComponent>(objUid);
                if (auObj == null || !auObj.Active)
                    continue;
                // If the wildcard objective also specifies a proto filter, respect it
                var destroyComp = EntityManager.GetComponentOrNull<DestroyObjectiveComponent>(objUid);
                if (destroyComp != null && !string.IsNullOrEmpty(destroyComp.EntityToDestroy) && !string.Equals(destroyComp.EntityToDestroy, proto, StringComparison.OrdinalIgnoreCase))
                    continue;
                var mark = _entManager.EnsureComponent<MarkedForDestroyComponent>(uid);
                mark.AssociatedObjectives[objUid] = auObj.Faction.ToLowerInvariant();
            }
        }
    }

    private void OnTrackerStartup(EntityUid uid, DestroyObjectiveTrackerComponent comp, ref ComponentStartup args)
    {
        Timer.Spawn(TimeSpan.FromMilliseconds(200), () =>
        {
            if (!EntityManager.EntityExists(uid))
                return;
            TryMarkForDestroyDelayed(uid);
        });
    }

    private void TryMarkForDestroyDelayed(EntityUid uid)
    {
        var meta = EntityManager.GetComponentOrNull<MetaDataComponent>(uid);
        var protoId = meta?.EntityPrototype?.ID ?? string.Empty;
        if (string.IsNullOrEmpty(protoId))
            return;

        var protoKey = protoId.ToLowerInvariant();

        // First handle specific proto objectives
        if (_protoToObjectives.TryGetValue(protoKey, out var objList))
        {
            foreach (var objUid in objList)
            {
                var auObj = EntityManager.GetComponentOrNull<AuObjectiveComponent>(objUid);
                if (auObj == null || !auObj.Active)
                    continue;
                var mark = _entManager.EnsureComponent<MarkedForDestroyComponent>(uid);
                mark.AssociatedObjectives[objUid] = auObj.Faction.ToLowerInvariant();
            }
        }

        // Then handle wildcard objectives
        foreach (var objUid in _wildcardObjectives)
        {
            var auObj = EntityManager.GetComponentOrNull<AuObjectiveComponent>(objUid);
            if (auObj == null || !auObj.Active)
                continue;
            var destroyComp = EntityManager.GetComponentOrNull<DestroyObjectiveComponent>(objUid);
            if (destroyComp != null && !string.IsNullOrEmpty(destroyComp.EntityToDestroy) && !string.Equals(destroyComp.EntityToDestroy, protoId, StringComparison.OrdinalIgnoreCase))
                continue;
            var mark = _entManager.EnsureComponent<MarkedForDestroyComponent>(uid);
            mark.AssociatedObjectives[objUid] = auObj.Faction.ToLowerInvariant();
        }
    }

    private void OnMarkedEntityDestroyed(EntityUid uid, MarkedForDestroyComponent comp, ref EntityTerminatingEvent args)
    {
        var meta = EntityManager.GetComponentOrNull<MetaDataComponent>(uid);
        var protoId = meta?.EntityPrototype?.ID ?? string.Empty;

        var objectivesToRemove = new List<EntityUid>();
        foreach (var kv in comp.AssociatedObjectives)
        {
            var objectiveUid = kv.Key;
            var factionToCredit = kv.Value;

            if (!EntityManager.TryGetComponent(objectiveUid, out DestroyObjectiveComponent? destroyComp))
                continue;
            var auObj = EnsureComp<AuObjectiveComponent>(objectiveUid);
            var factionKey = factionToCredit.ToLowerInvariant();

            destroyComp.AmountDestroyed++;
            Sawmill.Info($"[DESTROY DEBUG] Objective {objectiveUid} counted destruction of proto {protoId} for faction {factionKey}. Total: {destroyComp.AmountDestroyed}/{destroyComp.AmountToDestroy}");

            if (destroyComp.AmountDestroyed >= destroyComp.AmountToDestroy)
            {
                Sawmill.Info($"[DESTROY DEBUG] Objective {objectiveUid} completed for faction {factionKey}!");
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

