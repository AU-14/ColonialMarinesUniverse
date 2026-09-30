using Content.Shared._RMC14.Intel.Detector;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.CMU14.Round.Objectives.Type;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Round.Objectives.Type;

public enum FetchAnalyzeResult : byte
{
    NotObjective,
    WrongFaction,
    AlreadyFetched,
    Fetched,
}

public sealed partial class ObjFetchSystem : ObjectiveSystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _xformSys = default!;

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("obj-fetch");
        SubscribeLocalEvent<FetchObjectiveComponent, ObjectiveActivatedEvent>(OnActivated);
        SubscribeLocalEvent<FetchObjectiveComponent, ObjectiveResetEvent>(OnReset);
        SubscribeLocalEvent<ObjectiveWatchedEntityStartupEvent>(OnEntityMetaStartup);
        SubscribeLocalEvent<FetchItemComponent, EntityTerminatingEvent>(OnFetchItemDestroyed);
        SubscribeLocalEvent<FetchItemComponent, ComponentStartup>(OnFetchItemStartup);
    }

    /// <summary>Objective items show up on intel detectors, the same as RMC intel.</summary>
    private void OnFetchItemStartup(EntityUid uid, FetchItemComponent comp, ref ComponentStartup args)
        => EnsureComp<IntelDetectorTrackedComponent>(uid);

    private void OnActivated(EntityUid uid, FetchObjectiveComponent fetchComp, ref ObjectiveActivatedEvent args)
    {
        if (!TryComp(uid, out CMUObjectiveComponent? comp) || !comp.Active)
            return;

        if (args.LateActivation)
            fetchComp.LateActivation = true;

        var objMap = Transform(uid).MapID;
        ObjInt.RegisterInterest(uid, objMap,
            keys: string.IsNullOrEmpty(fetchComp.TargetPrototype) ? null : new[] { fetchComp.TargetPrototype },
            wildcard: fetchComp.UseAnyEntity);

        if (fetchComp.HasSpawned)
            return;

        var claimed = string.IsNullOrEmpty(fetchComp.TargetPrototype)
            ? 0 : fetchComp.Catalog
                ? ClaimRandomFetchSources(uid, fetchComp, objMap)
                : RegisterNearbyFetchEntities(uid, fetchComp);

        if (claimed == 0)
        {
            if (fetchComp.LateActivation)
            {
                _logs.Info($"[OBJ-FETCH] Late activated fetch objective '{ToPrettyString(uid)}' ('{comp.Id}') on map {objMap}" +
                           $" found no free spawn source or pre-placed '{fetchComp.TargetPrototype}'!");
                return;
            }

            _logs.Error($"[OBJ-FETCH] Fetch objective refusing to spawn! '{ToPrettyString(uid)}' ('{comp.Id}', '{comp.ObjectiveDescription}') on map={objMap}" +
                        $" has no spawn sources: no {(string.IsNullOrEmpty(fetchComp.SpawnMarkerId) ? "generic marker" : $"marker '{fetchComp.SpawnMarkerId}'")}" +
                        $" and no pre-placed '{fetchComp.TargetPrototype}' entities. Mappers must place CMUObjectiveMarker (or item ents) on the planet map.");
            ObjCtrl.MarkObjectiveFailed(uid, comp);
            return;
        }

        if (claimed < fetchComp.FetchCount)
            _logs.Warning($"[OBJ-FETCH] '{comp.ObjectiveDescription}' ('{comp.Id}') claimed only {claimed} of" +
                          $" {fetchComp.SpawnCount} sources; needs {fetchComp.FetchCount} fetched to complete - not enough targets exist on the map!");

        fetchComp.HasSpawned = true;
    }

    private void OnReset(EntityUid uid, FetchObjectiveComponent comp, ref ObjectiveResetEvent args)
    {
        comp.AmountFetchedPerFaction.Clear();

        if (comp.UseAnyEntity)
        {
            ObjInt.RegisterInterest(uid, Transform(uid).MapID, keys: string.IsNullOrEmpty(comp.TargetPrototype)
                ? null : new[] { comp.TargetPrototype }, wildcard: true);
        }

        if (!comp.RespawnOnRepeat)
            return;

        var query = EntityQueryEnumerator<FetchItemComponent>();
        while (query.MoveNext(out var ent, out var item))
        {
            if (item.ObjectiveUid == uid && !item.Fetched && Exists(ent))
                QueueDel(ent);
        }

        var objMap = Transform(uid).MapID;
        var searchMaps = _zLevels.GetAllNetworkMapIds(objMap);
        var markerQuery = AllEntityQuery<CMUObjectiveMarkerComponent, TransformComponent>();
        while (markerQuery.MoveNext(out _, out var markerComp, out var markerXform))
        {
            if (!searchMaps.Contains(markerXform.MapID))
                continue;

            if (!string.IsNullOrEmpty(comp.SpawnMarkerId))
            {
                if (markerComp.FetchId == comp.SpawnMarkerId)
                    markerComp.Used = false;
            }
            else if (markerComp.Generic)
                markerComp.Used = false;
        }

        comp.HasSpawned = false;
    }

    /// <summary>Checks the same unclaimed sources that activation will consume.</summary>
    public bool HasAvailableSources(EntityUid objectiveUid, FetchObjectiveComponent comp)
    {
        if (comp.HasSpawned || comp.LateActivation)
            return true;

        if (comp.Catalog)
            return HasAvailableCatalogSources(Transform(objectiveUid).MapID, comp);

        return FindNearbyFetchEntities(objectiveUid, comp).Count >= comp.FetchCount;
    }

    public bool HasAvailableCatalogSources(MapId map, FetchObjectiveComponent comp)
    {
        if (string.IsNullOrEmpty(comp.TargetPrototype) || comp.SpawnCount < comp.FetchCount)
            return false;

        return FindPreplacedFetchEntities(map, comp.TargetPrototype).Count
            + ResolveMarkers(map, comp.SpawnMarkerId).Count >= comp.FetchCount;
    }

    private int RegisterNearbyFetchEntities(EntityUid objectiveUid, FetchObjectiveComponent comp)
    {
        var entities = FindNearbyFetchEntities(objectiveUid, comp);
        foreach (var ent in entities)
            EnsureComp<FetchItemComponent>(ent).ObjectiveUid = objectiveUid;
        return entities.Count;
    }

    private List<EntityUid> FindNearbyFetchEntities(EntityUid objectiveUid, FetchObjectiveComponent comp, float radius = 48f)
    {
        var found = new List<EntityUid>();
        if (!TryComp(objectiveUid, out TransformComponent? xform))
            return found;

        foreach (var ent in _lookup.GetEntitiesInRange(xform.Coordinates, radius))
        {
            if (ent == objectiveUid || HasComp<FetchItemComponent>(ent))
                continue;

            if (!TryComp(ent, out MetaDataComponent? meta) || meta.EntityPrototype?.ID != comp.TargetPrototype)
                continue;

            found.Add(ent);
        }
        return found;
    }

    private int ClaimRandomFetchSources(EntityUid objectiveUid, FetchObjectiveComponent comp, MapId objMap)
    {
        var preplaced = FindPreplacedFetchEntities(objMap, comp.TargetPrototype);
        var markers = ResolveMarkers(objMap, comp.SpawnMarkerId);

        var pool = new List<(bool Preplaced, EntityUid Uid)>(preplaced.Count + markers.Count);
        foreach (var ent in preplaced)
            pool.Add((true, ent));
        foreach (var marker in markers)
            pool.Add((false, marker));

        if (pool.Count == 0)
            return 0;

        var rng = new Random();
        for (var n = pool.Count - 1; n > 0; n--)
        {
            var k = rng.Next(n + 1);
            (pool[n], pool[k]) = (pool[k], pool[n]);
        }

        var claimed = Math.Min(comp.SpawnCount, pool.Count);
        for (var i = 0; i < pool.Count; i++)
        {
            var (isPreplaced, srcUid) = pool[i];

            if (i >= claimed)
            {
                if (isPreplaced)
                    QueueDel(srcUid);
                continue;
            }

            if (isPreplaced)
            {
                EnsureComp<FetchItemComponent>(srcUid).ObjectiveUid = objectiveUid;
                continue;
            }

            var markerXform = Comp<TransformComponent>(srcUid);
            var ent = Spawn(comp.TargetPrototype, markerXform.Coordinates);
            EnsureComp<FetchItemComponent>(ent).ObjectiveUid = objectiveUid;
            if (!string.IsNullOrEmpty(comp.SpawnOther))
                Spawn(comp.SpawnOther, markerXform.Coordinates);
            MarkMarkerUsed(srcUid);
        }

        return claimed;
    }

    private List<EntityUid> FindPreplacedFetchEntities(MapId objMap, string targetPrototype)
    {
        var found = new List<EntityUid>();
        var searchMaps = _zLevels.GetAllNetworkMapIds(objMap);
        var query = EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var meta, out var entXform))
        {
            if (HasComp<FetchItemComponent>(ent))
                continue;

            if (!searchMaps.Contains(entXform.MapID) || meta.EntityPrototype?.ID != targetPrototype)
                continue;

            found.Add(ent);
        }
        return found;
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
        var claimed = HasComp<FetchItemComponent>(uid);
        foreach (var objUid in interested)
        {
            if (!TryComp(objUid, out CMUObjectiveComponent? auComp) || !auComp.Active)
                continue;

            if (TryComp(objUid, out FetchObjectiveComponent? fetchComp)
                    && !string.IsNullOrEmpty(fetchComp.TargetPrototype)
                    && !string.Equals(fetchComp.TargetPrototype, proto, StringComparison.OrdinalIgnoreCase))
                continue;

            if (HasComp<FetchItemComponent>(uid))
                continue;

            EnsureComp<FetchItemComponent>(uid).ObjectiveUid = objUid;
            claimed = true;
        }

        if (!claimed && ObjCtrl.TryGetFetchObjectiveForItem(proto, out var objectiveProto) && ObjCtrl.SelectionComplete)
            ObjCtrl.LateSpawnFetchObjectiveForItem(uid, objectiveProto);
    }

    /// <summary>
    /// Hands an objective item in at an Analyzer Machine. Credits the Analyzer's faction, or the objective's own
    /// faction for an Analyzer with none set.
    /// </summary>
    public FetchAnalyzeResult TryFetchAtAnalyzer(EntityUid analyzerUid, EntityUid itemUid)
    {
        var proto = MetaData(itemUid).EntityPrototype?.ID;
        if (string.IsNullOrEmpty(proto))
            return FetchAnalyzeResult.NotObjective;

        if (TryComp(itemUid, out FetchItemComponent? existing) && existing.Fetched)
            return FetchAnalyzeResult.AlreadyFetched;

        var analyzerFaction = TryComp(analyzerUid, out FetchAnalyzerComponent? a) ? a.Faction.ToLowerInvariant() : string.Empty;
        var wrongFaction = false;

        var query = EntityQueryEnumerator<FetchObjectiveComponent, CMUObjectiveComponent>();
        while (query.MoveNext(out var objUid, out var fetchComp, out var auComp))
        {
            if (!auComp.Active || !string.Equals(fetchComp.TargetPrototype, proto, StringComparison.OrdinalIgnoreCase))
                continue;

            // An item already claimed by a different objective only counts for that one.
            if (existing != null && existing.ObjectiveUid.IsValid() && existing.ObjectiveUid != objUid)
                continue;

            if (!string.IsNullOrEmpty(analyzerFaction) && !auComp.FactionNeutral && auComp.Faction.ToLowerInvariant() != analyzerFaction)
            {
                wrongFaction = true;
                continue;
            }

            var creditFaction = string.IsNullOrEmpty(analyzerFaction) ? auComp.Faction.ToLowerInvariant() : analyzerFaction;
            if (string.IsNullOrEmpty(creditFaction))
                continue;

            var item = EnsureComp<FetchItemComponent>(itemUid);
            item.ObjectiveUid = objUid;
            item.Fetched = true;
            fetchComp.AmountFetchedPerFaction.TryAdd(creditFaction, 0);
            fetchComp.AmountFetchedPerFaction[creditFaction]++;

            if (ShouldCompleteForFaction(auComp, creditFaction, fetchComp.AmountFetchedPerFaction[creditFaction], fetchComp.FetchCount))
            {
                ObjInt.UnregisterInterest(objUid);
                ObjCtrl.CompleteObjectiveForFaction(objUid, auComp, creditFaction, sawmill: _logs);
            }

            return FetchAnalyzeResult.Fetched;
        }

        return wrongFaction ? FetchAnalyzeResult.WrongFaction : FetchAnalyzeResult.NotObjective;
    }

    public int ScanForFetchItems(EntityUid analyzerUid, List<EntityUid>? fetched = null)
    {
        if (!TryComp(analyzerUid, out TransformComponent? analyzerXform))
            return 0;

        var analyzerFaction = TryComp(analyzerUid, out FetchAnalyzerComponent? a) ? a.Faction.ToLowerInvariant() : string.Empty;
        int totalFetched = 0;

        var query = EntityQueryEnumerator<FetchObjectiveComponent, CMUObjectiveComponent>();
        while (query.MoveNext(out var objUid, out var fetchComp, out var auComp))
        {
            if (!auComp.Active || string.IsNullOrEmpty(fetchComp.TargetPrototype))
                continue;

            if (!string.IsNullOrEmpty(analyzerFaction) && !auComp.FactionNeutral && auComp.Faction.ToLowerInvariant() != analyzerFaction)
                continue;

            var creditFaction = string.IsNullOrEmpty(analyzerFaction) ? auComp.Faction.ToLowerInvariant() : analyzerFaction;
            if (string.IsNullOrEmpty(creditFaction))
                continue;

            foreach (var ent in _lookup.GetEntitiesInRange(analyzerXform.Coordinates, 5f))
            {
                if (ent == analyzerUid || ent == objUid) continue;
                if (!TryComp(ent, out MetaDataComponent? meta) || meta.EntityPrototype?.ID != fetchComp.TargetPrototype) continue;

                var item = EnsureComp<FetchItemComponent>(ent);
                if (item.Fetched) continue;

                item.ObjectiveUid = objUid;
                fetchComp.AmountFetchedPerFaction.TryAdd(creditFaction, 0);
                fetchComp.AmountFetchedPerFaction[creditFaction]++;
                item.Fetched = true;
                totalFetched++;
                fetched?.Add(ent);
            }

            if (ShouldCompleteForFaction(auComp, creditFaction, fetchComp.AmountFetchedPerFaction.GetValueOrDefault(creditFaction), fetchComp.FetchCount))
            {
                ObjInt.UnregisterInterest(objUid);
                ObjCtrl.CompleteObjectiveForFaction(objUid, auComp, creditFaction, sawmill: _logs);
            }
        }
        return totalFetched;
    }

    private void OnFetchItemDestroyed(EntityUid uid, FetchItemComponent comp, ref EntityTerminatingEvent args)
    {
        if (comp.Fetched || comp.ObjectiveUid == EntityUid.Invalid || TerminatingOrDeleted(comp.ObjectiveUid))
            return;
        if (!TryComp(comp.ObjectiveUid, out FetchObjectiveComponent? fetchComp)
                || !TryComp(comp.ObjectiveUid, out CMUObjectiveComponent? auComp))
            return;

        int unfetched = 0;
        var q = EntityQueryEnumerator<FetchItemComponent>();
        while (q.MoveNext(out var ent, out var other))
        {
            if (ent != uid && other.ObjectiveUid == comp.ObjectiveUid && !other.Fetched)
                unfetched++;
        }

        var factions = auComp.FactionNeutral ? auComp.Factions : [auComp.Faction];
        foreach (var faction in factions)
        {
            var key = faction.ToLowerInvariant();
            var possible = fetchComp.AmountFetchedPerFaction.GetValueOrDefault(key) + unfetched;
            if (possible >= fetchComp.FetchCount) continue;
            if (auComp.StatusesPerFaction.TryGetValue(key, out var s) && s != CMUObjectiveComponent.ObjectiveStatus.Incomplete)
                continue;

            ObjCtrl.MarkObjectiveFailedForFaction(comp.ObjectiveUid, auComp, faction);
        }
    }
}
