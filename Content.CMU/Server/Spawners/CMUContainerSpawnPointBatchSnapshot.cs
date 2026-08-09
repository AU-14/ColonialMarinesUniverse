using Content.Server.Spawners.Components;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Spawners.EntitySystems;

internal readonly record struct CMUContainerSpawnPointEntry(
    EntityUid Uid,
    EntityUid? Station,
    ProtoId<JobPrototype>? Job,
    SpawnPointType SpawnType,
    string ContainerId,
    int Order = -1);

/// <summary>
/// Immutable-for-one-batch container spawn-point metadata in component-query order.
/// </summary>
internal sealed class CMUContainerSpawnPointBatchSnapshot
{
    private readonly StationBucket _allStations = new();
    private readonly Dictionary<EntityUid, StationBucket> _stations = new();
    private int _count;

    public int Count => _count;

    public void Add(CMUContainerSpawnPointEntry entry)
    {
        entry = entry with { Order = _count++ };
        _allStations.Add(entry);

        if (entry.Station is not { } station)
            return;

        if (!_stations.TryGetValue(station, out var bucket))
        {
            bucket = new StationBucket();
            _stations.Add(station, bucket);
        }

        bucket.Add(entry);
    }

    public void Clear()
    {
        _allStations.Clear();
        _stations.Clear();
        _count = 0;
    }

    /// <summary>
    /// Copies only candidates matching the requested station, round phase, and job, in component-query order.
    /// A null requested station retains the original all-stations behavior.
    /// </summary>
    public void CopyCandidates(
        EntityUid? station,
        ProtoId<JobPrototype>? job,
        bool inRound,
        List<CMUContainerSpawnPointEntry> destination)
    {
        destination.Clear();

        if (station is not { } stationUid)
        {
            _allStations.CopyCandidates(job, inRound, destination);
            return;
        }

        if (_stations.TryGetValue(stationUid, out var bucket))
            bucket.CopyCandidates(job, inRound, destination);
    }

    public static bool Matches(
        EntityUid? candidateStation,
        ProtoId<JobPrototype>? candidateJob,
        SpawnPointType spawnType,
        EntityUid? station,
        ProtoId<JobPrototype>? job,
        bool inRound)
    {
        if (station != null && candidateStation != station)
            return false;

        if (spawnType == SpawnPointType.Unset)
            return candidateJob == null || candidateJob == job;

        if (inRound)
            return spawnType == SpawnPointType.LateJoin;

        return spawnType == SpawnPointType.Job &&
               (job == null || candidateJob == job);
    }

    private sealed class StationBucket
    {
        private readonly List<CMUContainerSpawnPointEntry> _lateJoin = [];
        private readonly List<CMUContainerSpawnPointEntry> _roundStart = [];
        private readonly Dictionary<ProtoId<JobPrototype>, List<CMUContainerSpawnPointEntry>> _roundStartByJob = new();
        private readonly List<CMUContainerSpawnPointEntry> _unsetAnyJob = [];
        private readonly Dictionary<ProtoId<JobPrototype>, List<CMUContainerSpawnPointEntry>> _unsetByJob = new();

        public void Add(CMUContainerSpawnPointEntry entry)
        {
            switch (entry.SpawnType)
            {
                case SpawnPointType.Unset when entry.Job is { } job:
                    GetOrAdd(_unsetByJob, job).Add(entry);
                    break;
                case SpawnPointType.Unset:
                    _unsetAnyJob.Add(entry);
                    break;
                case SpawnPointType.Job:
                    _roundStart.Add(entry);
                    if (entry.Job is { } roundStartJob)
                        GetOrAdd(_roundStartByJob, roundStartJob).Add(entry);
                    break;
                case SpawnPointType.LateJoin:
                    _lateJoin.Add(entry);
                    break;
            }
        }

        public void Clear()
        {
            _lateJoin.Clear();
            _roundStart.Clear();
            _roundStartByJob.Clear();
            _unsetAnyJob.Clear();
            _unsetByJob.Clear();
        }

        public void CopyCandidates(
            ProtoId<JobPrototype>? job,
            bool inRound,
            List<CMUContainerSpawnPointEntry> destination)
        {
            List<CMUContainerSpawnPointEntry>? unsetForJob = null;
            List<CMUContainerSpawnPointEntry>? phaseCandidates;

            if (job is { } jobId)
            {
                _unsetByJob.TryGetValue(jobId, out unsetForJob);
                if (inRound)
                    phaseCandidates = _lateJoin;
                else
                    _roundStartByJob.TryGetValue(jobId, out phaseCandidates);
            }
            else
            {
                phaseCandidates = inRound ? _lateJoin : _roundStart;
            }

            MergeOrdered(_unsetAnyJob, unsetForJob, phaseCandidates, destination);
        }

        private static List<CMUContainerSpawnPointEntry> GetOrAdd(
            Dictionary<ProtoId<JobPrototype>, List<CMUContainerSpawnPointEntry>> buckets,
            ProtoId<JobPrototype> job)
        {
            if (buckets.TryGetValue(job, out var entries))
                return entries;

            entries = [];
            buckets.Add(job, entries);
            return entries;
        }

        private static void MergeOrdered(
            List<CMUContainerSpawnPointEntry> first,
            List<CMUContainerSpawnPointEntry>? second,
            List<CMUContainerSpawnPointEntry>? third,
            List<CMUContainerSpawnPointEntry> destination)
        {
            var firstIndex = 0;
            var secondIndex = 0;
            var thirdIndex = 0;

            while (firstIndex < first.Count ||
                   secondIndex < (second?.Count ?? 0) ||
                   thirdIndex < (third?.Count ?? 0))
            {
                var source = 0;
                var nextOrder = int.MaxValue;

                if (firstIndex < first.Count)
                {
                    source = 1;
                    nextOrder = first[firstIndex].Order;
                }

                if (second != null &&
                    secondIndex < second.Count &&
                    second[secondIndex].Order < nextOrder)
                {
                    source = 2;
                    nextOrder = second[secondIndex].Order;
                }

                if (third != null &&
                    thirdIndex < third.Count &&
                    third[thirdIndex].Order < nextOrder)
                {
                    source = 3;
                }

                switch (source)
                {
                    case 1:
                        destination.Add(first[firstIndex++]);
                        break;
                    case 2:
                        destination.Add(second![secondIndex++]);
                        break;
                    case 3:
                        destination.Add(third![thirdIndex++]);
                        break;
                }
            }
        }
    }
}
