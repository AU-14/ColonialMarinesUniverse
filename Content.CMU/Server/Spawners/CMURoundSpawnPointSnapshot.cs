using Content.Server.Spawners.Components;
using Content.Shared._CMU14.Round.Roles;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Spawners;

internal enum CMUGenericSpawnSelection : byte
{
    None,
    Preferred,
    Fallback,
}

internal readonly record struct CMURoundSpawnPointEntry(
    EntityUid Uid,
    EntityUid? Station,
    ProtoId<JobPrototype>? Job,
    SpawnPointType SpawnType,
    bool OnAnyShip,
    bool OnGovforShip,
    bool OnOpforShip);

/// <summary>
/// Immutable-for-one-batch lookup data compiled from round-start spawn points.
/// </summary>
internal sealed class CMURoundSpawnPointSnapshot
{
    private readonly FactionSpawnBucket _govfor = new();
    private readonly GenericSpawnBucket _global = new();
    private readonly FactionSpawnBucket _opfor = new();
    private readonly Dictionary<EntityUid, CMURoundSpawnPointEntry> _entriesByUid = new();
    private readonly Dictionary<EntityUid, GenericSpawnBucket> _stations = new();
    private readonly List<CMURoundSpawnPointEntry> _entries = [];

    private EntityUid? _fallback;

    public IReadOnlyList<CMURoundSpawnPointEntry> Entries => _entries;

    public void Add(CMURoundSpawnPointEntry entry)
    {
        _entries.Add(entry);
        _entriesByUid[entry.Uid] = entry;
        _fallback ??= entry.Uid;
        _global.Add(entry);

        if (entry.Station is { } station)
        {
            if (!_stations.TryGetValue(station, out var stationBucket))
            {
                stationBucket = new GenericSpawnBucket();
                _stations.Add(station, stationBucket);
            }

            stationBucket.Add(entry);
        }

        if (entry.OnGovforShip)
            _govfor.AddShip(entry);
        if (entry.OnOpforShip)
            _opfor.AddShip(entry);

        if (entry.OnAnyShip)
            return;

        _govfor.AddPlanet(entry, SpawnPointType.LateJoinGovfor);
        _opfor.AddPlanet(entry, SpawnPointType.LateJoinOpfor);
    }

    public bool TryGetEntry(EntityUid uid, out CMURoundSpawnPointEntry entry)
    {
        return _entriesByUid.TryGetValue(uid, out entry);
    }

    public void Clear()
    {
        _entries.Clear();
        _entriesByUid.Clear();
        _stations.Clear();
        _global.Clear();
        _govfor.Clear();
        _opfor.Clear();
        _fallback = null;
    }

    public CMUGenericSpawnSelection PickGeneric(
        EntityUid? station,
        ProtoId<JobPrototype>? job,
        bool inRound,
        IRobustRandom random,
        out EntityUid uid)
    {
        GenericSpawnBucket? bucket = _global;
        if (station is { } stationUid)
        {
            if (_stations.TryGetValue(stationUid, out var stationBucket))
                bucket = stationBucket;
            else
                bucket = null;
        }

        if (bucket != null && bucket.TryPick(job, inRound, random, out uid))
            return CMUGenericSpawnSelection.Preferred;

        if (_fallback is { } fallback)
        {
            // The legacy one-item Pick consumed RNG state before returning its fallback.
            random.Next(1);
            uid = fallback;
            return CMUGenericSpawnSelection.Fallback;
        }

        uid = default;
        return CMUGenericSpawnSelection.None;
    }

    public bool TryPickFactionShip(
        RoundJobSide side,
        ProtoId<JobPrototype>? job,
        IRobustRandom random,
        out EntityUid uid)
    {
        var bucket = GetFactionBucket(side);
        if (bucket != null)
            return bucket.TryPickShip(job, random, out uid);

        uid = default;
        return false;
    }

    public bool TryPickFactionPlanet(
        RoundJobSide side,
        ProtoId<JobPrototype>? job,
        IRobustRandom random,
        out EntityUid uid)
    {
        var bucket = GetFactionBucket(side);
        if (bucket != null)
            return bucket.TryPickPlanet(job, random, out uid);

        uid = default;
        return false;
    }

    private FactionSpawnBucket? GetFactionBucket(RoundJobSide side)
    {
        return side switch
        {
            RoundJobSide.Govfor => _govfor,
            RoundJobSide.Opfor => _opfor,
            _ => null,
        };
    }

    private static bool TryPick(
        IReadOnlyList<EntityUid> candidates,
        IRobustRandom random,
        out EntityUid uid)
    {
        if (candidates.Count == 0)
        {
            uid = default;
            return false;
        }

        uid = candidates[random.Next(candidates.Count)];
        return true;
    }

    private sealed class GenericSpawnBucket
    {
        private readonly Dictionary<ProtoId<JobPrototype>, List<OrderedSpawnCandidate>> _roundStartByJob = new();
        private readonly List<EntityUid> _lateJoin = [];
        private readonly List<EntityUid> _roundStartAll = [];
        private readonly List<OrderedSpawnCandidate> _roundStartAnyJob = [];

        public void Add(CMURoundSpawnPointEntry entry)
        {
            if (entry.SpawnType == SpawnPointType.LateJoin)
                _lateJoin.Add(entry.Uid);

            if (entry.SpawnType != SpawnPointType.Job)
                return;

            var candidate = new OrderedSpawnCandidate(entry.Uid, _roundStartAll.Count);
            _roundStartAll.Add(entry.Uid);
            if (entry.Job is not { } job)
            {
                _roundStartAnyJob.Add(candidate);
                return;
            }

            GetOrAdd(_roundStartByJob, job).Add(candidate);
        }

        public void Clear()
        {
            _lateJoin.Clear();
            _roundStartAll.Clear();
            _roundStartAnyJob.Clear();
            _roundStartByJob.Clear();
        }

        public bool TryPick(
            ProtoId<JobPrototype>? job,
            bool inRound,
            IRobustRandom random,
            out EntityUid uid)
        {
            if (inRound)
                return CMURoundSpawnPointSnapshot.TryPick(_lateJoin, random, out uid);

            if (job is not { } jobId)
                return CMURoundSpawnPointSnapshot.TryPick(_roundStartAll, random, out uid);

            if (_roundStartByJob.TryGetValue(jobId, out var exact))
                return TryPickMerged(exact, _roundStartAnyJob, random, out uid);

            return TryPickOrdered(_roundStartAnyJob, random, out uid);
        }

        private static bool TryPickOrdered(
            IReadOnlyList<OrderedSpawnCandidate> candidates,
            IRobustRandom random,
            out EntityUid uid)
        {
            if (candidates.Count == 0)
            {
                uid = default;
                return false;
            }

            uid = candidates[random.Next(candidates.Count)].Uid;
            return true;
        }

        private static bool TryPickMerged(
            IReadOnlyList<OrderedSpawnCandidate> exact,
            IReadOnlyList<OrderedSpawnCandidate> wildcard,
            IRobustRandom random,
            out EntityUid uid)
        {
            var count = exact.Count + wildcard.Count;
            if (count == 0)
            {
                uid = default;
                return false;
            }

            // Binary-partition the two ordered lists so seeded picks match the legacy query-order list.
            var take = random.Next(count) + 1;
            var low = Math.Max(0, take - wildcard.Count);
            var high = Math.Min(take, exact.Count);
            while (low <= high)
            {
                var exactCount = (low + high) / 2;
                var wildcardCount = take - exactCount;
                var exactLeft = exactCount == 0 ? int.MinValue : exact[exactCount - 1].Order;
                var exactRight = exactCount == exact.Count ? int.MaxValue : exact[exactCount].Order;
                var wildcardLeft = wildcardCount == 0 ? int.MinValue : wildcard[wildcardCount - 1].Order;
                var wildcardRight = wildcardCount == wildcard.Count ? int.MaxValue : wildcard[wildcardCount].Order;

                if (exactLeft <= wildcardRight && wildcardLeft <= exactRight)
                {
                    uid = exactLeft >= wildcardLeft
                        ? exact[exactCount - 1].Uid
                        : wildcard[wildcardCount - 1].Uid;
                    return true;
                }

                if (exactLeft > wildcardRight)
                    high = exactCount - 1;
                else
                    low = exactCount + 1;
            }

            throw new InvalidOperationException("Unable to select a merged spawn-point candidate.");
        }

        private readonly record struct OrderedSpawnCandidate(
            EntityUid Uid,
            int Order);
    }

    private sealed class FactionSpawnBucket
    {
        private readonly Dictionary<ProtoId<JobPrototype>, List<EntityUid>> _planetByJob = new();
        private readonly Dictionary<ProtoId<JobPrototype>, List<EntityUid>> _shipByJob = new();
        private readonly List<EntityUid> _planetAll = [];
        private readonly List<EntityUid> _planetFallback = [];
        private readonly List<EntityUid> _shipAll = [];

        public void AddShip(CMURoundSpawnPointEntry entry)
        {
            if (entry.SpawnType == SpawnPointType.Observer)
                return;

            _shipAll.Add(entry.Uid);
            if (entry.Job is { } job)
                GetOrAdd(_shipByJob, job).Add(entry.Uid);
        }

        public void AddPlanet(CMURoundSpawnPointEntry entry, SpawnPointType fallbackType)
        {
            if (entry.SpawnType is SpawnPointType.Job or SpawnPointType.Unset)
            {
                _planetAll.Add(entry.Uid);
                if (entry.Job is { } job)
                    GetOrAdd(_planetByJob, job).Add(entry.Uid);
            }

            if (entry.SpawnType == fallbackType)
                _planetFallback.Add(entry.Uid);
        }

        public void Clear()
        {
            _planetAll.Clear();
            _planetFallback.Clear();
            _planetByJob.Clear();
            _shipAll.Clear();
            _shipByJob.Clear();
        }

        public bool TryPickShip(
            ProtoId<JobPrototype>? job,
            IRobustRandom random,
            out EntityUid uid)
        {
            if (job is { } jobId &&
                _shipByJob.TryGetValue(jobId, out var exact) &&
                exact.Count > 0)
            {
                return CMURoundSpawnPointSnapshot.TryPick(exact, random, out uid);
            }

            return CMURoundSpawnPointSnapshot.TryPick(_shipAll, random, out uid);
        }

        public bool TryPickPlanet(
            ProtoId<JobPrototype>? job,
            IRobustRandom random,
            out EntityUid uid)
        {
            if (job == null)
            {
                if (CMURoundSpawnPointSnapshot.TryPick(_planetAll, random, out uid))
                    return true;
            }
            else if (_planetByJob.TryGetValue(job.Value, out var exact) &&
                     CMURoundSpawnPointSnapshot.TryPick(exact, random, out uid))
            {
                return true;
            }

            return CMURoundSpawnPointSnapshot.TryPick(_planetFallback, random, out uid);
        }
    }

    private static List<TValue> GetOrAdd<TKey, TValue>(
        Dictionary<TKey, List<TValue>> dictionary,
        TKey key)
        where TKey : notnull
    {
        if (dictionary.TryGetValue(key, out var values))
            return values;

        values = [];
        dictionary.Add(key, values);
        return values;
    }
}
