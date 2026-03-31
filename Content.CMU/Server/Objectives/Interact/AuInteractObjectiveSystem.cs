using System.Linq;
using System.Runtime.CompilerServices;
using Content.Server.Popups;
using Content.Shared.AU14.Objectives;
using Content.Shared.AU14.Objectives.Fetch;
using Content.Shared.AU14.Objectives.Interact;
using Content.Shared.Popups;
using Robust.Shared.Log;

namespace Content.Server.AU14.Objectives.Interact;

/// <summary>
/// Server-side system for Interact objectives.
/// Handles spawning interactable entities, DoAfter completion, progress tracking, and objective completion.
/// </summary>
public sealed class AuInteractObjectiveSystem : EntitySystem
{
    [Robust.Shared.IoC.Dependency]private readonly IEntityManager _entManager = default!;
    [Robust.Shared.IoC.Dependency] private readonly AuObjectiveSystem _objectiveSystem = default!;
    [Robust.Shared.IoC.Dependency] private readonly PopupSystem _popup = default!;

    private static readonly ISawmill Sawmill = Logger.GetSawmill("au14-interactobj");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InteractObjectiveComponent, ComponentStartup>(OnObjectiveStartup);
        SubscribeLocalEvent<InteractObjectiveTrackerComponent, InteractObjectiveDoAfterEvent>(OnInteractDoAfter);
    }

    /// <summary>
    /// Called when the Interact objective is activated. Spawns or registers interactable entities.
    /// </summary>
    public void ActivateInteractObjectiveIfNeeded(EntityUid uid, AuObjectiveComponent comp)
    {
        if (!_entManager.TryGetComponent(uid, out InteractObjectiveComponent? interactComp))
            return;
        if (!comp.Active || interactComp.EntitiesSpawned)
            return;
        OnObjectiveStartup(uid, interactComp, ref Unsafe.NullRef<ComponentStartup>());
    }

    private void OnObjectiveStartup(EntityUid uid, InteractObjectiveComponent component, ref ComponentStartup args)
    {
        if (component.EntitiesSpawned)
            return;

        var objComp = EnsureComp<AuObjectiveComponent>(uid);
        if (!objComp.Active)
            return;

        // If Spawn is false, register existing preplaced entities by scanning nearby
        if (!component.Spawn)
        {
            var registered = RegisterPreplacedEntities(uid, component);
            if (registered > 0)
            {
                component.EntitiesSpawned = true;
                Sawmill.Info($"[INTERACT OBJ] Registered {registered} preplaced entities for objective {uid}");
            }
            return;
        }

        // Spawn entities on markers (same pattern as Fetch/Destroy objectives)
        var markerId = component.SpawnMarker;
        var amount = component.AmountToSpawn;

        if (component.Interactables.Count == 0)
        {
            Sawmill.Warning($"[INTERACT OBJ] Objective {uid} has no Interactables defined!");
            return;
        }

        var entityToSpawn = component.Interactables[0]; // Use first interactable as the entity to spawn

        var markers = new List<EntityUid>();
        var genericMarkers = new List<EntityUid>();
        var markerQuery = EntityManager.AllEntityQueryEnumerator<FetchObjectiveMarkerComponent, TransformComponent>();
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
        {
            Sawmill.Warning($"[INTERACT OBJ] No markers found for objective {uid}");
            return;
        }

        // Shuffle markers
        var rng = new Random();
        if (markers.Count > 1)
        {
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
                continue;

            var xform = EntityManager.GetComponent<TransformComponent>(markerUid);
            var ent = EntityManager.SpawnEntity(entityToSpawn, xform.Coordinates);
            var tracker = _entManager.EnsureComponent<InteractObjectiveTrackerComponent>(ent);
            tracker.ObjectiveUid = uid;
            markerComp.Used = true;
        }

        component.EntitiesSpawned = true;
        Sawmill.Info($"[INTERACT OBJ] Spawned {toSpawn} interactable entities for objective {uid}");
    }

    /// <summary>
    /// Scans ALL entities on the map and registers any whose prototype matches the Interactables list.
    /// When spawn is false, every matching entity on the map becomes interactable.
    /// </summary>
    private int RegisterPreplacedEntities(EntityUid objectiveUid, InteractObjectiveComponent component)
    {
        if (component.Interactables.Count == 0)
            return 0;

        var interactableSet = component.Interactables.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var registered = 0;

        var query = EntityManager.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var ent, out var meta))
        {
            if (ent == objectiveUid)
                continue;

            if (EntityManager.HasComponent<InteractObjectiveTrackerComponent>(ent))
                continue;

            var proto = meta.EntityPrototype?.ID;
            if (proto == null || !interactableSet.Contains(proto))
                continue;

            var tracker = _entManager.EnsureComponent<InteractObjectiveTrackerComponent>(ent);
            tracker.ObjectiveUid = objectiveUid;
            registered++;
        }

        return registered;
    }

    /// <summary>
    /// Handles DoAfter completion for interact objectives.
    /// </summary>
    private void OnInteractDoAfter(EntityUid uid, InteractObjectiveTrackerComponent tracker, InteractObjectiveDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_entManager.TryGetComponent<InteractObjectiveComponent>(tracker.ObjectiveUid, out var interactComp))
            return;

        if (!_entManager.TryGetComponent<AuObjectiveComponent>(tracker.ObjectiveUid, out var objComp))
            return;

        if (!objComp.Active)
            return;

        var faction = args.Faction.ToLowerInvariant();
        if (string.IsNullOrEmpty(faction))
            return;

        // Check faction eligibility
        if (!objComp.FactionNeutral && objComp.Faction.ToLowerInvariant() != faction)
        {
            // Non-neutral objective: only the assigned faction can complete it
            if (!objComp.Factions.Any(f => f.ToLowerInvariant() == faction))
                return;
        }

        // Check if already completed for this faction
        if (objComp.FactionStatuses.TryGetValue(faction, out var status) &&
            status == AuObjectiveComponent.ObjectiveStatus.Completed)
            return;

        // Check per-entity completion cap
        var entityCompletions = tracker.CompletionsPerFaction.GetValueOrDefault(faction, 0);
        if (entityCompletions >= interactComp.CompletionsPerEnt)
            return;

        // Increment interaction count for this entity+faction
        if (!tracker.InteractionsPerFaction.ContainsKey(faction))
            tracker.InteractionsPerFaction[faction] = 0;
        tracker.InteractionsPerFaction[faction]++;

        var currentInteractions = tracker.InteractionsPerFaction[faction];
        var popupUser = args.User != EntityUid.Invalid ? args.User : uid;

        _popup.PopupEntity(interactComp.DoAfterMessageComplete, uid, popupUser, PopupType.Medium);
        Sawmill.Info($"[INTERACT OBJ] Entity {uid} interacted by {args.User} for faction {faction}. Interaction {currentInteractions}/{interactComp.Interactionsneeded}");

        // Check if this entity has reached the required number of interactions for one completion
        if (currentInteractions >= interactComp.Interactionsneeded)
        {
            // Reset interaction counter for next completion cycle on this entity
            tracker.InteractionsPerFaction[faction] = 0;

            // Increment per-entity completion
            if (!tracker.CompletionsPerFaction.ContainsKey(faction))
                tracker.CompletionsPerFaction[faction] = 0;
            tracker.CompletionsPerFaction[faction]++;

            // Increment objective-level completion
            if (!interactComp.CompletionsPerFaction.ContainsKey(faction))
                interactComp.CompletionsPerFaction[faction] = 0;
            interactComp.CompletionsPerFaction[faction]++;

            var totalNeeded = interactComp.TotalCompletionsNeeded > 0
                ? interactComp.TotalCompletionsNeeded
                : interactComp.AmountToSpawn;

            Sawmill.Info($"[INTERACT OBJ] Entity {uid} completed for faction {faction}. Total completions: {interactComp.CompletionsPerFaction[faction]}/{totalNeeded}");

            // Destroy entity if configured
            if (interactComp.DestroyOnComplete && tracker.CompletionsPerFaction[faction] >= interactComp.CompletionsPerEnt)
            {
                if (_entManager.EntityExists(uid))
                {
                    Sawmill.Info($"[INTERACT OBJ] Destroying entity {uid} after completion");
                    _entManager.QueueDeleteEntity(uid);
                }
            }

            // Award points for each completion
            _objectiveSystem.AwardPointsToFaction(faction, objComp);

            // Check if the overall objective is complete
            if (interactComp.CompletionsPerFaction[faction] >= totalNeeded)
            {
                Sawmill.Info($"[INTERACT OBJ] Objective {tracker.ObjectiveUid} completed for faction {faction}!");
                _objectiveSystem.CompleteObjectiveForFaction(tracker.ObjectiveUid, objComp, faction);
            }
        }
    }

    /// <summary>
    /// Resets the interact objective for repeating. Clears completion tracking and re-registers entities.
    /// </summary>
    public void ResetInteractObjective(EntityUid uid, InteractObjectiveComponent component)
    {
        component.CompletionsPerFaction.Clear();
        component.EntitiesSpawned = false;

        // Reset all trackers linked to this objective
        var query = EntityManager.EntityQueryEnumerator<InteractObjectiveTrackerComponent>();
        while (query.MoveNext(out _, out var tracker))
        {
            if (tracker.ObjectiveUid != uid)
                continue;

            tracker.CurrentInteractions = 0;
            tracker.CompletionsPerFaction.Clear();
            tracker.InteractionsPerFaction.Clear();
        }

        // Re-register/re-spawn entities
        if (component.Spawn)
        {
            OnObjectiveStartup(uid, component, ref Unsafe.NullRef<ComponentStartup>());
        }
        else
        {
            var registered = RegisterPreplacedEntities(uid, component);
            if (registered > 0)
                component.EntitiesSpawned = true;
        }
    }
}

