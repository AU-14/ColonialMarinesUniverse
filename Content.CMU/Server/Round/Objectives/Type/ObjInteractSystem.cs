using System.Linq;
using Content.Server.Popups;
using Content.Shared._CMU14.Round.Objectives;
using Content.Shared._CMU14.Round.Objectives.Type;
using Content.Shared._CMU14.Round.Objectives.Component;
using Content.Shared.Popups;

namespace Content.Server._CMU14.Round.Objectives.Type;

public sealed partial class ObjInteractSystem : ObjectiveSystem
{
    [Dependency] private PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("obj-interact");
        SubscribeLocalEvent<InteractObjectiveComponent, ObjectiveActivatedEvent>(OnActivated);
        SubscribeLocalEvent<InteractObjectiveComponent, ObjectiveResetEvent>(OnReset);
        SubscribeLocalEvent<InteractTrackerComponent, InteractObjectiveDoAfterEvent>(OnDoAfterComplete);
        SubscribeLocalEvent<ObjectiveWatchedEntityStartupEvent>(OnEntityMetaStartup);
    }

    private void OnActivated(EntityUid uid, InteractObjectiveComponent interactComp, ref ObjectiveActivatedEvent _)
    {
        if (!TryComp(uid, out CMUObjectiveComponent? comp)
                || !comp.Active
                || interactComp.HasSpawned)
            return;

        var objMap = Transform(uid).MapID;

        if (interactComp.Interactables.Count > 0)
        {
            ObjInt.RegisterInterest(uid, objMap,
                keys: interactComp.Interactables,
                wildcard: false);
        }

        if (!interactComp.Spawn)
        {
            var registered = RegisterPreplacedEntities(uid, interactComp);
            interactComp.HasSpawned = registered > 0;
        }
        else
        {
            var entityToSpawn = interactComp.Interactables.FirstOrDefault();
            if (string.IsNullOrEmpty(entityToSpawn))
            {
                _logs.Warning($"[INTERACT] Objective {uid} has no Interactables defined!");
                return;
            }

            var markers = ResolveMarkers(objMap, interactComp.SpawnMarkerId);
            var spawned = SpawnEntitiesAtMarkers(entityToSpawn, interactComp.SpawnCount, markers, shuffle: true);
            foreach (var ent in spawned)
                EnsureComp<InteractTrackerComponent>(ent).ObjectiveUid = uid;

            interactComp.HasSpawned = spawned.Count > 0;
        }
    }

    private void OnReset(EntityUid uid, InteractObjectiveComponent comp, ref ObjectiveResetEvent args)
    {
        comp.CompletionsPerFaction.Clear();
        comp.HasSpawned = false;

        var query = EntityQueryEnumerator<InteractTrackerComponent, TransformComponent>();
        while (query.MoveNext(out _, out var tracker, out var xform))
        {
            if (tracker.ObjectiveUid != uid || xform.MapID != Transform(uid).MapID)
                continue;

            tracker.CompletionsPerFaction.Clear();
            tracker.InteractionsPerFaction.Clear();
        }
    }

    private int RegisterPreplacedEntities(EntityUid objectiveUid, InteractObjectiveComponent comp)
    {
        var interactableSet = comp.Interactables.ToHashSet(StringComparer.OrdinalIgnoreCase);
        int registered = 0;
        var objMap = Transform(objectiveUid).MapID;

        var query = AllEntityQuery<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var meta, out var xform))
        {
            if (xform.MapID != objMap || ent == objectiveUid || HasComp<InteractTrackerComponent>(ent))
                continue;
            if (meta.EntityPrototype?.ID is not { } proto || !interactableSet.Contains(proto))
                continue;

            EnsureComp<InteractTrackerComponent>(ent).ObjectiveUid = objectiveUid;
            registered++;
        }
        return registered;
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
            if (!TryComp(objUid, out InteractObjectiveComponent? _)
                    || !TryComp(objUid, out CMUObjectiveComponent? auComp) || !auComp.Active)
                continue;

            if (HasComp<InteractTrackerComponent>(uid))
                continue;

            EnsureComp<InteractTrackerComponent>(uid).ObjectiveUid = objUid;
        }
    }

    private void OnDoAfterComplete(EntityUid uid, InteractTrackerComponent tracker, InteractObjectiveDoAfterEvent args)
    {
        if (args.Cancelled || !TryComp(tracker.ObjectiveUid, out InteractObjectiveComponent? interactComp)
                || !TryComp(tracker.ObjectiveUid, out CMUObjectiveComponent? auComp))
            return;

        var faction = args.Faction.ToLowerInvariant();
        if (string.IsNullOrEmpty(faction)) return;

        if (!auComp.FactionNeutral && auComp.Faction.ToLowerInvariant() != faction
                && auComp.Factions.All(f => f.ToLowerInvariant() != faction))
            return;

        if (auComp.StatusesPerFaction.TryGetValue(faction, out var s) && s == CMUObjectiveComponent.ObjectiveStatus.Completed)
            return;

        var entityCompletions = tracker.CompletionsPerFaction.GetValueOrDefault(faction, 0);
        if (entityCompletions >= interactComp.CompletionsPerEnt)
            return;

        tracker.InteractionsPerFaction.TryAdd(faction, 0);
        tracker.InteractionsPerFaction[faction]++;

        var current = tracker.InteractionsPerFaction[faction];
        _popup.PopupEntity(interactComp.DoAfterMessageComplete, uid, args.User != EntityUid.Invalid ? args.User : uid, PopupType.Medium);

        if (current < interactComp.InteractionsNeeded)
            return;

        tracker.InteractionsPerFaction[faction] = 0;
        tracker.CompletionsPerFaction.TryAdd(faction, 0);
        tracker.CompletionsPerFaction[faction]++;

        interactComp.CompletionsPerFaction.TryAdd(faction, 0);
        interactComp.CompletionsPerFaction[faction]++;

        var totalNeeded = interactComp.TotalCompletionsNeeded > 0
            ? interactComp.TotalCompletionsNeeded
            : interactComp.SpawnCount;

        ObjCtrl.AwardPointsToFaction(faction, auComp);

        if (interactComp.DestroyOnComplete
                && tracker.CompletionsPerFaction[faction] >= interactComp.CompletionsPerEnt
                && Exists(uid))
            QueueDel(uid);

        if (interactComp.CompletionsPerFaction[faction] >= totalNeeded)
        {
            ObjInt.UnregisterInterest(tracker.ObjectiveUid);
            ObjCtrl.CompleteObjectiveForFaction(tracker.ObjectiveUid, auComp, faction, awardPoints: false);
        }
    }
}
