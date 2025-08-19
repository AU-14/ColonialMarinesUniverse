using Content.Shared.AU14.Objectives;
using Content.Shared.AU14.Objectives.Fetch;
using Content.Shared.Interaction.Events;
using Content.Shared.DragDrop;
using Robust.Shared.Map;

namespace Content.Server.AU14.Objectives.Fetch;

public sealed class AuFetchObjectiveSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FetchObjectiveComponent, ComponentStartup>(OnObjectiveStartup);
        SubscribeLocalEvent<AuFetchItemComponent, DroppedEvent>(OnFetchItemDropped);
        SubscribeLocalEvent<AuFetchItemComponent, DragDropDraggedEvent>(OnFetchItemUndragged);
    }

    private void OnObjectiveStartup(EntityUid uid, FetchObjectiveComponent component, ref ComponentStartup args)
    {
        var objcomp = EnsureComp<AuObjectiveComponent>(uid);
        if (objcomp.Active)
            return;

        var entityToSpawn = component.EntityToSpawn;
        var markerFetchId = component.MarkerEntity; // Used as FetchId
        var amount = component.AmountToSpawn;

        var markers = new List<EntityUid>();
        var genericMarkers = new List<EntityUid>();
        var markerQuery = EntityManager.AllEntityQueryEnumerator<FetchObjectiveMarkerComponent, TransformComponent>();
        while (markerQuery.MoveNext(out var markerUid, out var markerComp, out _))
        {
            if (markerComp.FetchId == markerFetchId)
                markers.Add(markerUid);
            else if (markerComp.Generic)
                genericMarkers.Add(markerUid);
        }

        if (markers.Count == 0)
            markers = genericMarkers;

        if (markers.Count == 0 || string.IsNullOrEmpty(entityToSpawn))
            return;

        for (var i = 0; i < amount; i++)
        {
            var markerIndex = i % markers.Count;
            var markerUid = markers[markerIndex];
            var xform = EntityManager.GetComponent<TransformComponent>(markerUid);
            var ent = EntityManager.SpawnEntity(entityToSpawn, xform.Coordinates);
            var comp = _entManager.EnsureComponent<AuFetchItemComponent>(ent);
            comp.FetchObjective = component;
        }
    }

    private void OnFetchItemDropped(EntityUid uid, AuFetchItemComponent comp, ref DroppedEvent args)
    {
        TryHandleFetchItemDropOrUndrag(uid, comp);
    }

    private void OnFetchItemUndragged(EntityUid uid, AuFetchItemComponent comp, ref DragDropDraggedEvent args)
    {
        TryHandleFetchItemDropOrUndrag(uid, comp);
    }

    private void TryHandleFetchItemDropOrUndrag(EntityUid uid, AuFetchItemComponent comp)
    {
        if (comp.FetchObjective == null)
            return;
        var xform = EntityManager.GetComponent<TransformComponent>(uid);
        var tile = xform.Coordinates;
        // Find all entities on this tile with FetchObjectiveMarkerComponent
        var foundMarker = false;
        foreach (var ent in _lookup.GetEntitiesInRange(tile, 0.9f))
        {
            if (!EntityManager.TryGetComponent(ent, out FetchObjectiveMarkerComponent? marker))
                continue;
            var returnId = comp.FetchObjective.CustomReturnPointId;
            if (!string.IsNullOrEmpty(returnId))
            {
                if (marker.FetchId == returnId || (string.IsNullOrEmpty(marker.FetchId) && marker.Generic))
                {
                    foundMarker = true;
                    break;
                }
            }
            else if (marker.Generic)
            {
                foundMarker = true;
                break;
            }
        }
        if (foundMarker)
        {
            if (!comp.Fetched)
            {
                comp.Fetched = true;
                comp.FetchObjective.AmountFetched++;
                if (comp.FetchObjective.AmountFetched >= comp.FetchObjective.AmountToFetch)
                {
                    var objComp = EnsureComp<AuObjectiveComponent>(comp.FetchObjective.Owner);
                    objComp.Completed = true;
                }
            }
        }
    }
}
