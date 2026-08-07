using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Map;

namespace Content.Server._CMU14.Round.Objectives;

public sealed class ObjectiveWatchedEntityStartupEvent(EntityUid uid) : EntityEventArgs
{
    public readonly EntityUid Uid = uid;
}

public sealed class ObjectiveInterestSystem : EntitySystem
{
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;

    private readonly Dictionary<string, List<EntityUid>> _interestByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<EntityUid, MapId> _objectiveMap = new();
    private readonly List<EntityUid> _wildcardObjectives = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MetaDataComponent, ComponentStartup>(OnEntityStartup);
    }

    private void OnEntityStartup(EntityUid uid, MetaDataComponent meta, ref ComponentStartup args)
    {
        RaiseLocalEvent(new ObjectiveWatchedEntityStartupEvent(uid));
    }

    public override void Shutdown()
    {
        _interestByKey.Clear();
        _wildcardObjectives.Clear();
        _objectiveMap.Clear();
        base.Shutdown();
    }

    public void RegisterInterest(EntityUid objUid, MapId map, IEnumerable<string>? keys = null, bool wildcard = false)
    {
        UnregisterInterest(objUid);
        _objectiveMap[objUid] = map;

        if (wildcard)
            _wildcardObjectives.Add(objUid);

        if (keys == null)
            return;

        foreach (var key in keys)
        {
            if (string.IsNullOrEmpty(key))
                continue;

            if (!_interestByKey.TryGetValue(key, out var list))
                _interestByKey[key] = list = new List<EntityUid>();
            list.Add(objUid);
        }
    }

    public void UnregisterInterest(EntityUid objUid)
    {
        _wildcardObjectives.Remove(objUid);
        _objectiveMap.Remove(objUid);

        List<string>? emptyKeys = null;
        foreach (var (key, list) in _interestByKey)
        {
            if (!list.Remove(objUid) || list.Count != 0)
                continue;

            emptyKeys ??= new List<string>();
            emptyKeys.Add(key);
        }

        if (emptyKeys == null)
            return;

        foreach (var key in emptyKeys)
            _interestByKey.Remove(key);
    }

    public IEnumerable<EntityUid> GetInterestedObjectives(MapId entityMap, IEnumerable<string> keys)
    {
        var seen = new HashSet<EntityUid>();
        foreach (var key in keys)
        {
            if (string.IsNullOrEmpty(key) || !_interestByKey.TryGetValue(key, out var list))
                continue;

            foreach (var objUid in list)
                if (_objectiveMap.TryGetValue(objUid, out var map) && IsSameZNetwork(map, entityMap) && seen.Add(objUid))
                    yield return objUid;
        }

        foreach (var objUid in _wildcardObjectives)
        {
            if (_objectiveMap.TryGetValue(objUid, out var map) && IsSameZNetwork(map, entityMap) && seen.Add(objUid))
                yield return objUid;
        }
    }

    private bool IsSameZNetwork(MapId a, MapId b)
    {
        if (a == b)
            return true;

        if (!_mapSystem.TryGetMap(a, out var mapUidA) || !_mapSystem.TryGetMap(b, out var mapUidB))
            return false;

        if (!_zLevels.TryGetZNetwork(mapUidA.Value, out var network))
            return false;

        return _zLevels.IsMapInNetwork(network.Value, mapUidB.Value);
    }
}
