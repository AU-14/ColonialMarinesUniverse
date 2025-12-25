using System.Linq;
using System.Runtime.CompilerServices;
using Content.Shared.AU14.Objectives;
using Content.Shared.AU14.Objectives.Fetch;
using Content.Shared.Interaction.Events;
using Content.Shared.DragDrop;
using Robust.Shared.Map;
using Content.Server.AU14.Objectives;
using Content.Shared.Movement.Pulling.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Log;

namespace Content.Server.AU14.Objectives.Fetch;

public sealed class AuFetchObjectiveSystem : EntitySystem
{
    [Robust.Shared.IoC.Dependency] private readonly IEntityManager _entManager = default!;
    [Robust.Shared.IoC.Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Robust.Shared.IoC.Dependency] private readonly AuObjectiveSystem _objectiveSystem = default!;
    [Robust.Shared.IoC.Dependency] private readonly SharedTransformSystem _xformSys = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FetchObjectiveComponent, ComponentStartup>(OnObjectiveStartup);
        SubscribeLocalEvent<FetchObjectiveComponent, ComponentHandleState>(OnFetchObjectiveHandleState);
        SubscribeLocalEvent<AuFetchItemComponent, DroppedEvent>(OnFetchItemDropped);
        SubscribeLocalEvent<AuFetchItemComponent, PullStoppedMessage>(OnFetchItemUndragged);
        SubscribeLocalEvent<FetchObjectiveReturnPointComponent, DragDropTargetEvent>(OnReturnPointDragDropTarget);
        SubscribeLocalEvent<MetaDataComponent, ComponentStartup>(OnMetaDataStartup);
        SubscribeLocalEvent<AuFetchItemComponent, EntityTerminatingEvent>(OnFetchItemDestroyed);
    }

    public void ActivateFetchObjectiveIfNeeded(EntityUid uid, AuObjectiveComponent comp)
    {
        if (!_entManager.TryGetComponent(uid, out FetchObjectiveComponent? fetchComp))
            return;
        if (!comp.Active || fetchComp.ItemsSpawned)
            return;
        OnObjectiveStartup(uid, fetchComp, ref Unsafe.NullRef<ComponentStartup>());
    }

    private void OnMetaDataStartup(EntityUid uid, MetaDataComponent meta, ref ComponentStartup args)
    {
        if (meta.EntityPrototype == null)
            return;
        var protoId = meta.EntityPrototype.ID;
        var query = EntityManager.EntityQueryEnumerator<FetchObjectiveComponent>();
        while (query.MoveNext(out var fetchUid, out var fetchComp))
        {
            var objComp = EnsureComp<AuObjectiveComponent>(fetchUid);
            if (!objComp.Active || !fetchComp.UseAnyEntity || string.IsNullOrEmpty(fetchComp.EntityToSpawn))
                continue;
            if (fetchComp.EntityToSpawn == protoId)
            {
                var comp = _entManager.EnsureComponent<AuFetchItemComponent>(uid);
                comp.FetchObjective = fetchComp;
                comp.ObjectiveUid = fetchUid;
            }
        }
    }

    private void OnFetchObjectiveHandleState(EntityUid uid, FetchObjectiveComponent component, ref ComponentHandleState args)
    {
    }

    private void OnObjectiveStartup(EntityUid uid, FetchObjectiveComponent component, ref ComponentStartup args)
    {
        // Prevent duplicate spawns
        if (component.ItemsSpawned)
            return;
        var objcomp = EnsureComp<AuObjectiveComponent>(uid);
        if (!objcomp.Active)
            return;

        var entityToSpawn = component.EntityToSpawn;
        var markerFetchId = component.MarkerEntity;
        var amount = component.AmountToSpawn;


        var markers = new List<EntityUid>();
        var genericMarkers = new List<EntityUid>();
        var markerQuery = EntityManager.AllEntityQueryEnumerator<FetchObjectiveMarkerComponent, TransformComponent>();
        while (markerQuery.MoveNext(out var markerUid, out var markerComp, out _))
        {
            if (markerComp.Used)
                continue; // Skip used markers
            if (markerComp.FetchId == markerFetchId)
                markers.Add(markerUid);
            else if (markerComp.Generic)
                genericMarkers.Add(markerUid);
        }

        if (markers.Count == 0)
            markers = genericMarkers;

        if (markers.Count == 0 || string.IsNullOrEmpty(entityToSpawn))
            return;

        // Shuffle markers for random selection
        var rng = new Random();
        if (markers.Count > 1)
        {
            // Fisher-Yates shuffle for robust randomness
            int n = markers.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (markers[n], markers[k]) = (markers[k], markers[n]);
            }
        }

        int toSpawn = Math.Min(amount, markers.Count);
        for (var i = 0; i < toSpawn; i++)
        {
            var markerUid = markers[i];
            var markerComp = EntityManager.GetComponent<FetchObjectiveMarkerComponent>(markerUid);
            if (markerComp.Used)
                continue; // Double check, should not happen
            var xform = EntityManager.GetComponent<TransformComponent>(markerUid);
            var ent = EntityManager.SpawnEntity(entityToSpawn, xform.Coordinates);
            var comp = _entManager.EnsureComponent<AuFetchItemComponent>(ent);
            comp.FetchObjective = component;
            comp.ObjectiveUid = uid;
            // Mark this marker as used
            markerComp.Used = true;
            if (!string.IsNullOrEmpty(component.SpawnOther))
            {
                EntityManager.SpawnEntity(component.SpawnOther, xform.Coordinates);
            }
        }
        component.ItemsSpawned = true;
    }


    public void TryActivateFetchObjective(EntityUid uid, FetchObjectiveComponent component)
    {
        var objComp = EnsureComp<AuObjectiveComponent>(uid);
        if (objComp.Active && !component.ItemsSpawned)
        {
            OnObjectiveStartup(uid, component, ref Unsafe.NullRef<ComponentStartup>());
        }
    }

    private void OnFetchItemDropped(EntityUid uid, AuFetchItemComponent comp, ref DroppedEvent args)
    {
        TryHandleFetchItemDropOrUndrag(uid, comp);
    }

    private void OnFetchItemUndragged(EntityUid uid, AuFetchItemComponent comp, ref PullStoppedMessage args)
    {
        TryHandleFetchItemDropOrUndrag(uid, comp);
    }

    private void TryHandleFetchItemDropOrUndrag(EntityUid uid, AuFetchItemComponent comp)
    {
        Logger.Info($"[FETCH DEBUG] TryHandleFetchItemDropOrUndrag called for {uid}");
        var xform = EntityManager.GetComponent<TransformComponent>(uid);
        var tile = xform.Coordinates;
        var gridId = _xformSys.GetGrid(tile);
        var tilePos = _xformSys.GetWorldPosition(xform);
        Logger.Info($"[FETCH DEBUG] Item {uid} at grid {gridId}, pos {tilePos}");
        FetchObjectiveReturnPointComponent? usedReturnPoint = null;
        foreach (var ent in _lookup.GetEntitiesInRange(tile, 10f))
        {
            Logger.Info($"[FETCH DEBUG] Checking entity {ent} in range");
            if (!EntityManager.TryGetComponent(ent, out FetchObjectiveReturnPointComponent? returnPoint))
                continue;
            var returnXform = EntityManager.GetComponent<TransformComponent>(ent);
            var returnCoords = returnXform.Coordinates;
            var returnGridId = _xformSys.GetGrid(returnCoords);
            var returnTilePos = _xformSys.GetWorldPosition(returnXform);
            Logger.Info($"[FETCH DEBUG] Return point {ent} at grid {returnGridId}, pos {returnTilePos}, generic={returnPoint.Generic}, fetchid={returnPoint.FetchId}, faction={returnPoint.ReturnPointFaction}");
            // Check if on same grid and tile (rounded to int)
            if (gridId != returnGridId)
            {
                Logger.Info($"[FETCH DEBUG] Grid mismatch: item {gridId}, return {returnGridId}");
                continue;
            }
            if ((int)tilePos.X != (int)returnTilePos.X || (int)tilePos.Y != (int)returnTilePos.Y)
            {
                Logger.Info($"[FETCH DEBUG] Tile mismatch: item ({(int)tilePos.X},{(int)tilePos.Y}), return ({(int)returnTilePos.X},{(int)returnTilePos.Y})");
                continue;
            }
            var returnId = comp.FetchObjective.CustomReturnPointId;
            if (!string.IsNullOrEmpty(returnId))
            {
                if (returnPoint.FetchId == returnId || (string.IsNullOrEmpty(returnPoint.FetchId) && returnPoint.Generic))
                {
                    Logger.Info($"[FETCH DEBUG] Matched specific returnId {returnId}");
                    usedReturnPoint = returnPoint;
                    break;
                }
            }
            else if (returnPoint.Generic)
            {
                Logger.Info($"[FETCH DEBUG] Matched generic return point");
                usedReturnPoint = returnPoint;
                break;
            }
        }
        if (usedReturnPoint == null)
        {
            Logger.Info($"[FETCH DEBUG] No valid return point found for fetch item {uid} at {tile} (grid {gridId}, pos {tilePos})");
            return;
        }
        Logger.Info($"[FETCH DEBUG] Found valid return point {usedReturnPoint.Owner} for fetch item {uid} at {tile} (grid {gridId}, pos {tilePos})");
        var returnPointFaction = usedReturnPoint.ReturnPointFaction.ToLowerInvariant();
        if (string.IsNullOrEmpty(returnPointFaction))
        {
            Logger.Info($"[FETCH DEBUG] Return point faction is empty");
            return;
        }
        var fetchObj = comp.FetchObjective;
        // Initialize dictionary if needed
        if (!fetchObj.AmountFetchedPerFaction.ContainsKey(returnPointFaction))
            fetchObj.AmountFetchedPerFaction[returnPointFaction] = 0;
        // Only mark this item as fetched for this faction
        if (!comp.Fetched)
        {
            fetchObj.AmountFetchedPerFaction[returnPointFaction]++;
            comp.Fetched = true;
            Logger.Info($"[FETCH DEBUG] Fetch item {uid} counted for faction {returnPointFaction}. Total: {fetchObj.AmountFetchedPerFaction[returnPointFaction]}/{fetchObj.AmountToFetch}");
        }
        var objComp = EnsureComp<AuObjectiveComponent>(comp.ObjectiveUid);
        if (objComp.FactionNeutral)
        {
            if (fetchObj.AmountFetchedPerFaction[returnPointFaction] >= fetchObj.AmountToFetch)
            {
                Logger.Info($"[FETCH DEBUG] Objective {comp.ObjectiveUid} completed for faction {returnPointFaction}!");
                _objectiveSystem.CompleteObjectiveForFaction(comp.ObjectiveUid, objComp, returnPointFaction);
            }
        }
        else
        {
            if (returnPointFaction == objComp.Faction.ToLowerInvariant())
            {
                if (fetchObj.AmountFetchedPerFaction[returnPointFaction] >= fetchObj.AmountToFetch)
                {
                    Logger.Info($"[FETCH DEBUG] Objective {comp.ObjectiveUid} completed for faction {returnPointFaction}!");
                    _objectiveSystem.CompleteObjectiveForFaction(comp.ObjectiveUid, objComp, returnPointFaction);
                }
            }
        }
    }

    private void OnReturnPointDragDropTarget(EntityUid uid, FetchObjectiveReturnPointComponent comp, ref DragDropTargetEvent args)
    {
        if (!EntityManager.TryGetComponent(args.Dragged, out AuFetchItemComponent? fetchItem))
            return;
        TryHandleFetchItemDropOrUndrag(args.Dragged, fetchItem);
    }

    /// <summary>
    /// Resets and respawns a fetch objective for repeating objectives.
    /// </summary>
    public void ResetAndRespawnFetchObjective(EntityUid uid, FetchObjectiveComponent fetchComp)
    {
        fetchComp.AmountFetched = 0;
        fetchComp.AmountFetchedPerFaction.Clear();
        if (fetchComp.RespawnOnRepeat)
        {
            fetchComp.ItemsSpawned = false; // Reset so items can respawn
            OnObjectiveStartup(uid, fetchComp, ref Unsafe.NullRef<ComponentStartup>());
        }
    }


    private void OnFetchItemDestroyed(EntityUid uid, AuFetchItemComponent comp, ref EntityTerminatingEvent args)
    {
        var fetchObj = comp.FetchObjective;
        if (comp.Fetched)
            return;
        int unfetched = 0;
        var query = EntityManager.EntityQueryEnumerator<AuFetchItemComponent>();
        while (query.MoveNext(out var ent, out var itemComp))
        {
            if (itemComp.FetchObjective == fetchObj && !itemComp.Fetched && ent != uid)
                unfetched++;
        }
        var objComp = EnsureComp<AuObjectiveComponent>(comp.ObjectiveUid);
        var factions = objComp.FactionNeutral ? objComp.Factions : new List<string> { objComp.Faction };
        foreach (var faction in factions)
        {
            var factionKey = faction.ToLowerInvariant();
            int alreadyFetched = 0;
            fetchObj.AmountFetchedPerFaction.TryGetValue(factionKey, out alreadyFetched);
            int possible = alreadyFetched + unfetched;
            if (possible < fetchObj.AmountToFetch)
            {
                if (objComp.FactionStatuses.TryGetValue(factionKey, out var status) && status == AuObjectiveComponent.ObjectiveStatus.Incomplete)
                {
                    objComp.FactionStatuses[factionKey] = AuObjectiveComponent.ObjectiveStatus.Failed;
                    Logger.Info($"[FETCH FAIL] Objective {comp.ObjectiveUid} failed for faction {factionKey} due to destroyed fetch items");
                    // Optionally, refresh consoles or notify
                    _objectiveSystem?.AwardPointsToFaction(factionKey, objComp); // Optionally award 0 points to trigger UI update
                }
            }
        }
    }
}
