using Content.Shared._CMU14.Round.Objectives.Component;
using Content.Shared._CMU14.Round.Objectives.Type;
using Content.Shared.DragDrop;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Pulling.Events;
using Robust.Shared.Map;

namespace Content.Server._CMU14.Round.Objectives.Type;

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
        SubscribeLocalEvent<FetchItemComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<FetchItemComponent, PullStoppedMessage>(OnUndragged);
        SubscribeLocalEvent<FetchReturnPointComponent, DragDropTargetEvent>(OnReturnPointDragDrop);
        SubscribeLocalEvent<FetchItemComponent, EntityTerminatingEvent>(OnFetchItemDestroyed);
    }

    private void OnActivated(EntityUid uid, FetchObjectiveComponent fetchComp, ref ObjectiveActivatedEvent args)
    {
        if (!TryComp(uid, out CMUObjectiveComponent? comp) || !comp.Active || fetchComp.HasSpawned)
            return;

        if (args.LateActivation)
            fetchComp.LateActivation = true;

        var objMap = Transform(uid).MapID;
        ObjInt.RegisterInterest(uid, objMap,
            keys: string.IsNullOrEmpty(fetchComp.TargetPrototype) ? null : new[] { fetchComp.TargetPrototype },
            wildcard: fetchComp.UseAnyEntity);

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
        var searchMaps = GetZNetworkMapIds(objMap);
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

    private int RegisterNearbyFetchEntities(EntityUid objectiveUid, FetchObjectiveComponent comp, float radius = 48f)
    {
        if (!TryComp(objectiveUid, out TransformComponent? xform))
            return 0;

        var registered = 0;
        foreach (var ent in _lookup.GetEntitiesInRange(xform.Coordinates, radius))
        {
            if (ent == objectiveUid || HasComp<FetchItemComponent>(ent))
                continue;

            if (!TryComp(ent, out MetaDataComponent? meta) || meta.EntityPrototype?.ID != comp.TargetPrototype)
                continue;

            EnsureComp<FetchItemComponent>(ent).ObjectiveUid = objectiveUid;
            registered++;
        }
        return registered;
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

        var toClaim = Math.Min(comp.SpawnCount, pool.Count);
        for (var i = 0; i < toClaim; i++)
        {
            var (isPreplaced, srcUid) = pool[i];
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

        return toClaim;
    }

    private List<EntityUid> FindPreplacedFetchEntities(MapId objMap, string targetPrototype)
    {
        var found = new List<EntityUid>();
        var searchMaps = GetZNetworkMapIds(objMap);
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
        }
    }

    private void OnDropped(EntityUid uid, FetchItemComponent item, ref DroppedEvent _) => TryCompleteFetch(uid, item);
    private void OnUndragged(EntityUid uid, FetchItemComponent item, ref PullStoppedMessage _) => TryCompleteFetch(uid, item);

    private void OnReturnPointDragDrop(EntityUid uid, FetchReturnPointComponent comp, ref DragDropTargetEvent args)
    {
        if (TryComp(args.Dragged, out FetchItemComponent? item))
            TryCompleteFetch(args.Dragged, item);
    }

    private void TryCompleteFetch(EntityUid itemUid, FetchItemComponent item)
    {
        if (item.Fetched
                || !TryComp(item.ObjectiveUid, out FetchObjectiveComponent? fetchComp)
                || !TryComp(item.ObjectiveUid, out CMUObjectiveComponent? auComp))
            return;

        var xform = Transform(itemUid);
        var coords = xform.Coordinates;
        var gridId = _xformSys.GetGrid(coords);
        var pos = _xformSys.GetWorldPosition(xform);

        FetchReturnPointComponent? matched = null;
        foreach (var ent in _lookup.GetEntitiesInRange(coords, 10f))
        {
            if (!TryComp(ent, out FetchReturnPointComponent? rp))
                continue;

            var rpXform = Transform(ent);
            if (_xformSys.GetGrid(rpXform.Coordinates) != gridId)
                continue;

            var rpPos = _xformSys.GetWorldPosition(rpXform);
            if ((int)pos.X != (int)rpPos.X || (int)pos.Y != (int)rpPos.Y)
                continue;

            var returnId = fetchComp.CustomReturnPointId;
            if (!string.IsNullOrEmpty(returnId))
            {
                if (rp.FetchId == returnId || (string.IsNullOrEmpty(rp.FetchId) && rp.Generic))
                { matched = rp; break; }
            }
            else if (rp.Generic)
            { matched = rp; break; }
        }

        if (matched is not { } rpComp || string.IsNullOrEmpty(rpComp.ReturnPointFaction))
            return;

        var faction = rpComp.ReturnPointFaction.ToLowerInvariant();
        if (!auComp.FactionNeutral && faction != auComp.Faction.ToLowerInvariant())
            return;

        fetchComp.AmountFetchedPerFaction.TryAdd(faction, 0);
        fetchComp.AmountFetchedPerFaction[faction]++;
        item.Fetched = true;

        if (ShouldCompleteForFaction(auComp, faction, fetchComp.AmountFetchedPerFaction[faction], fetchComp.FetchCount))
        {
            ObjInt.UnregisterInterest(item.ObjectiveUid);
            ObjCtrl.CompleteObjectiveForFaction(item.ObjectiveUid, auComp, faction, sawmill: _logs);
        }
    }

    public int ScanForFetchItems(EntityUid analyzerUid)
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
        while (q.MoveNext(out var _, out var other))
        {
            if (other.ObjectiveUid == comp.ObjectiveUid && !other.Fetched)
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
