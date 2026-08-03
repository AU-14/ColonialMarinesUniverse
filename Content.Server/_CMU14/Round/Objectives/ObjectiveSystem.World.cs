using Content.Shared._CMU14.Round.Objectives.Component;
using Robust.Shared.Map;

namespace Content.Server._CMU14.Round.Objectives;

public abstract partial class ObjectiveSystem
{
    protected (List<EntityUid> Specific, List<EntityUid> Generic) FindMarkers(MapId map, string? markerId)
    {
        var specific = new List<EntityUid>();
        var generic = new List<EntityUid>();
        var searchMaps = GetZNetworkMapIds(map);

        var query = AllEntityQuery<CMUObjectiveMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var markerUid, out var markerComp, out var markerXform))
        {
            if (markerComp.Used || !searchMaps.Contains(markerXform.MapID))
                continue;

            if (!string.IsNullOrEmpty(markerId) && markerComp.FetchId == markerId)
                specific.Add(markerUid);
            else if (markerComp.Generic)
                generic.Add(markerUid);
        }

        return (specific, generic);
    }

    protected List<EntityUid> ResolveMarkers(MapId map, string? markerId)
    {
        var (specific, generic) = FindMarkers(map, markerId);
        return specific.Count > 0 ? specific : generic;
    }

    protected void MarkMarkerUsed(EntityUid markerUid)
    {
        if (TryComp(markerUid, out CMUObjectiveMarkerComponent? markerComp))
            markerComp.Used = true;
    }

    protected List<EntityUid> SpawnEntitiesAtMarkers(
        string? prototypeId,
        int amountToSpawn,
        List<EntityUid> markers,
        bool shuffle = false)
    {
        var spawned = new List<EntityUid>();

        if (string.IsNullOrEmpty(prototypeId) || markers.Count == 0 || amountToSpawn <= 0)
            return spawned;

        if (shuffle && markers.Count > 1)
        {
            var rng = new Random();
            var n = markers.Count;
            while (n > 1)
            {
                n--;
                var k = rng.Next(n + 1);
                (markers[n], markers[k]) = (markers[k], markers[n]);
            }
        }

        var toSpawn = Math.Min(amountToSpawn, markers.Count);
        for (var i = 0; i < toSpawn; i++)
        {
            var markerUid = markers[i];
            if (TryComp(markerUid, out CMUObjectiveMarkerComponent? markerComp) && markerComp.Used)
                continue;

            var xform = Comp<TransformComponent>(markerUid);
            var ent = Spawn(prototypeId, xform.Coordinates);
            spawned.Add(ent);
            MarkMarkerUsed(markerUid);
        }

        if (spawned.Count > 0)
            _logs.Debug($"[OBJ-SPAWN] Spawned {spawned.Count}x '{prototypeId}' at markers (of {markers.Count} available).");

        return spawned;
    }

    protected List<EntityUid> SpawnEntitiesAtMarkersWithReuse(
        string? prototypeId,
        int amountToSpawn,
        List<EntityUid> markers)
    {
        var spawned = new List<EntityUid>();

        if (string.IsNullOrEmpty(prototypeId) || markers.Count == 0 || amountToSpawn <= 0)
            return spawned;

        for (var i = 0; i < amountToSpawn; i++)
        {
            var markerUid = markers[i % markers.Count];
            var xform = Comp<TransformComponent>(markerUid);
            var ent = Spawn(prototypeId, xform.Coordinates);
            spawned.Add(ent);
        }

        if (spawned.Count > 0)
            _logs.Debug($"[OBJ-SPAWN] Spawned {spawned.Count}x '{prototypeId}' at markers (reusing across {markers.Count} available).");

        return spawned;
    }
}
