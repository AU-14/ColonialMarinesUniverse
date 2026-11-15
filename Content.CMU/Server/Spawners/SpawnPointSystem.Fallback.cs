using Content.Server.Spawners.Components;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Server.Spawners.EntitySystems;

public sealed partial class SpawnPointSystem
{
    // the job's own marker isn't on this station's map (prey presets, odd planets, ship jobs on a
    // planet round). try that job's marker anywhere, then any job or late-join marker on the
    // player's station, then any spawn point at all, picked at random so nobody stacks on one tile
    private void CMUCollectFallbackSpawns(ProtoId<JobPrototype>? job, EntityUid? station, List<EntityCoordinates> into)
    {
        if (job != null)
        {
            var jobPoints = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            while (jobPoints.MoveNext(out var uid, out var spawnPoint, out var xform))
            {
                if (xform.MapID != MapId.Nullspace &&
                    spawnPoint.SpawnType == SpawnPointType.Job &&
                    (spawnPoint.Job == job || CMUTakesJob(uid, job)))
                {
                    into.Add(xform.Coordinates);
                }
            }

            if (into.Count > 0)
                return;
        }

        if (station != null)
        {
            var stationPoints = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            while (stationPoints.MoveNext(out var uid, out var spawnPoint, out var xform))
            {
                if (xform.MapID != MapId.Nullspace &&
                    spawnPoint.SpawnType != SpawnPointType.Observer &&
                    _stationSystem.GetOwningStation(uid, xform) == station)
                {
                    into.Add(xform.Coordinates);
                }
            }

            if (into.Count > 0)
                return;
        }

        var anyPoints = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (anyPoints.MoveNext(out _, out var spawnPoint, out var xform))
        {
            if (xform.MapID != MapId.Nullspace && spawnPoint.SpawnType != SpawnPointType.Observer)
                into.Add(xform.Coordinates);
        }
    }
}
