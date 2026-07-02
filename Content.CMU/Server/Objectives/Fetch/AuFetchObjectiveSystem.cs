using System.Linq;
using Content.Shared.AU14;
using Content.Shared.AU14.Objectives;
using Content.Shared.AU14.Objectives.Fetch;
using Content.Shared.DragDrop;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Pulling.Events;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Objectives.Fetch;

public sealed partial class AuFetchObjectiveSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private AuObjectiveSystem _objectiveSystem = default!;
    [Dependency] private SharedTransformSystem _xformSys = default!;

    private ISawmill _logs = default!;

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("obj-fetch");
        SubscribeLocalEvent<FetchObjectiveComponent, ComponentStartup>(OnObjectiveStartup);
        SubscribeLocalEvent<AuFetchItemComponent, DroppedEvent>(OnFetchItemDropped);
        SubscribeLocalEvent<AuFetchItemComponent, PullStoppedMessage>(OnFetchItemUndragged);
        SubscribeLocalEvent<FetchObjectiveReturnPointComponent, DragDropTargetEvent>(OnReturnPointDragDropTarget);
        SubscribeLocalEvent<AuFetchItemComponent, EntityTerminatingEvent>(OnFetchItemDestroyed);
    }

    private void OnObjectiveStartup(EntityUid uid, FetchObjectiveComponent component, ref ComponentStartup _) => StartupFetchObjective(uid, component);
    private void OnFetchItemDropped(EntityUid uid, AuFetchItemComponent comp, ref DroppedEvent _) => TryHandleFetchItemDropOrUndrag(uid, comp);
    private void OnFetchItemUndragged(EntityUid uid, AuFetchItemComponent comp, ref PullStoppedMessage _) => TryHandleFetchItemDropOrUndrag(uid, comp);

    public void ActivateFetchObjectiveIfNeeded(EntityUid uid, AuObjectiveComponent comp)
    {
        if (!TryComp(uid, out FetchObjectiveComponent? fetchComp))
            return;
        if (!comp.Active || fetchComp.ItemsSpawned)
            return;

        StartupFetchObjective(uid, fetchComp);
    }

    /// <summary>
    /// Scans a local radius around the objective for preplaced entities whose prototype matches
    /// <paramref name="prototypeId"/> and attaches AuFetchItemComponent to them so they count for the objective.
    /// This replaces the previous global MetaData startup handler which was extremely expensive.
    /// Returns the number of entities registered.
    /// </summary>
    private int RegisterPreplacedFetchEntities(string prototypeId, EntityUid objectiveUid, FetchObjectiveComponent component, float radius = 48f)
    {
        if (string.IsNullOrEmpty(prototypeId))
            return 0;

        if (!TryComp(objectiveUid, out TransformComponent? objXform))
            return 0;

        var registered = 0;

        // Use a spatial query to limit the scan to nearby entities only
        var center = objXform.Coordinates;
        foreach (var ent in _lookup.GetEntitiesInRange(center, radius))
        {
            // Skip the objective entity itself
            if (ent == objectiveUid)
                continue;

            // Skip if already has the fetch-item component
            if (HasComp<AuFetchItemComponent>(ent))
                continue;

            if (!TryComp(ent, out MetaDataComponent? meta))
                continue;

            var proto = meta.EntityPrototype?.ID;
            if (proto == null)
                continue;

            if (proto != prototypeId)
                continue;

            // Attach the fetch item component and link it to this objective
            var itemComp = EnsureComp<AuFetchItemComponent>(ent);
            itemComp.FetchObjective = component;
            itemComp.ObjectiveUid = objectiveUid;
            registered++;
        }

        if (registered <= 0) return registered;

        component.ItemsSpawned = true;
        _logs.Info($"[FETCH OBJ] Registered '{registered}' preplaced fetch entities for objective ({objectiveUid})");

        return registered;
    }

    private void StartupFetchObjective(EntityUid uid, FetchObjectiveComponent component)
    {
        // Prevent duplicate spawns
        if (component.ItemsSpawned)
            return;
        var objcomp = EnsureComp<AuObjectiveComponent>(uid);
        if (!objcomp.Active)
            return;

        // New behavior: when UseMarkers is false, items are registered by the Analyzer scan verb instead of being spawned at markers.
        if (!component.UseMarkers)
            return;

        // If this objective accepts preplaced entities (UseAnyEntity), try a local registration first
        if (component.UseAnyEntity && !string.IsNullOrEmpty(component.EntityToSpawn))
        {
            var registered = RegisterPreplacedFetchEntities(component.EntityToSpawn, uid, component);
            if (registered > 0)
                return; // we've satisfied the objective with preplaced items; don't spawn markers
        }

        var entityToSpawn = component.EntityToSpawn;
        var markerFetchId = component.MarkerEntity;
        var amount = component.AmountToSpawn;

        var markers = new List<EntityUid>();
        var genericMarkers = new List<EntityUid>();
        var objMap = Transform(uid).MapID;
        var markerQuery = AllEntityQuery<FetchObjectiveMarkerComponent, TransformComponent>();
        while (markerQuery.MoveNext(out var markerUid, out var markerComp, out var markerXform))
        {
            if (markerComp.Used || markerXform.MapID != objMap)
                continue;

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
            var markerComp = Comp<FetchObjectiveMarkerComponent>(markerUid);
            if (markerComp.Used)
                continue; // Double check, should not happen
            var xform = Comp<TransformComponent>(markerUid);
            var ent = Spawn(entityToSpawn, xform.Coordinates);
            var comp = EnsureComp<AuFetchItemComponent>(ent);
            comp.FetchObjective = component;
            comp.ObjectiveUid = uid;
            // Mark this marker as used
            markerComp.Used = true;
            if (!string.IsNullOrEmpty(component.SpawnOther))
            {
                Spawn(component.SpawnOther, xform.Coordinates);
            }
        }
        component.ItemsSpawned = true;
    }


    public void TryActivateFetchObjective(EntityUid uid, FetchObjectiveComponent component)
    {
        var objComp = EnsureComp<AuObjectiveComponent>(uid);
        if (!objComp.Active || component.ItemsSpawned)
            return;

        // New behavior: when UseMarkers is false, items are registered by the Analyzer scan verb.
        if (!component.UseMarkers)
            return;

        // If objective accepts preplaced entities, register them now before spawning
        if (component.UseAnyEntity && !string.IsNullOrEmpty(component.EntityToSpawn))
        {
            var registered = RegisterPreplacedFetchEntities(component.EntityToSpawn, uid, component);
            if (registered > 0)
                return;
        }

        StartupFetchObjective(uid, component);
    }
    private void TryHandleFetchItemDropOrUndrag(EntityUid uid, AuFetchItemComponent comp)
    {
        _logs.Debug($"[FETCH START] TryHandleFetchItemDropOrUndrag called for ({uid})");
        var xform = Comp<TransformComponent>(uid);
        var tile = xform.Coordinates;
        var gridId = _xformSys.GetGrid(tile);
        var tilePos = _xformSys.GetWorldPosition(xform);
        _logs.Debug($"[FETCH START]     Item ({uid}) at grid {gridId}, pos {tilePos}");
        (FetchObjectiveReturnPointComponent rpComp, EntityUid rpUid)? usedReturnPoint = null;
        foreach (var ent in _lookup.GetEntitiesInRange(tile, 10f))
        {
            _logs.Debug($"[FETCH RANGE] Checking entity ({ent}) in range");
            if (!TryComp(ent, out FetchObjectiveReturnPointComponent? returnPoint))
                continue;
            var returnXform = Comp<TransformComponent>(ent);
            var returnCoords = returnXform.Coordinates;
            var returnGridId = _xformSys.GetGrid(returnCoords);
            var returnTilePos = _xformSys.GetWorldPosition(returnXform);
            _logs.Debug($"[FETCH RETURN] Return point ({ent}) at grid {returnGridId}, pos {returnTilePos}, generic={returnPoint.Generic}, fetchid={returnPoint.FetchId}, faction={returnPoint.ReturnPointFaction}");
            // Check if on same grid and tile (rounded to int)
            if (gridId != returnGridId)
            {
                _logs.Warning($"[FETCH MISMATCH] Grid mismatch: item {gridId}, return {returnGridId}");
                continue;
            }
            if ((int)tilePos.X != (int)returnTilePos.X || (int)tilePos.Y != (int)returnTilePos.Y)
            {
                _logs.Warning($"[FETCH MISMATCH] Tile mismatch: item ({(int)tilePos.X},{(int)tilePos.Y}), return ({(int)returnTilePos.X},{(int)returnTilePos.Y})");
                continue;
            }
            var returnId = comp.FetchObjective.CustomReturnPointId;
            if (!string.IsNullOrEmpty(returnId))
            {
                if (returnPoint.FetchId != returnId
                    && (!string.IsNullOrEmpty(returnPoint.FetchId) || !returnPoint.Generic))
                    continue;
                _logs.Info($"[FETCH MATCH] Matched specific returnId {returnId}");
                usedReturnPoint = (returnPoint, ent);
                break;
            }
            else if (returnPoint.Generic)
            {
                _logs.Info($"[FETCH MATCH] Matched generic return point");
                usedReturnPoint = (returnPoint, ent);
                break;
            }
        }
        if (usedReturnPoint == null)
        {
            _logs.Warning($"[FETCH RETURN] No valid return point found for fetch item ({uid}) at {tile} (grid {gridId}, pos {tilePos})");
            return;
        }
        _logs.Debug($"[FETCH RETURN] Found valid return point ({usedReturnPoint.Value.rpUid}) for fetch item ({uid}) at {tile} (grid {gridId}, pos {tilePos})");
        var returnPointFaction = usedReturnPoint.Value.rpComp.ReturnPointFaction.ToLowerInvariant();
        if (string.IsNullOrEmpty(returnPointFaction))
        {
            _logs.Warning($"[FETCH RETURN] Return point faction is empty");
            return;
        }
        var fetchObj = comp.FetchObjective;
        // Initialize dictionary if needed
        fetchObj.AmountFetchedPerFaction.TryAdd(returnPointFaction, 0);
        // Only mark this item as fetched for this faction
        if (!comp.Fetched)
        {
            fetchObj.AmountFetchedPerFaction[returnPointFaction]++;
            comp.Fetched = true;
            _logs.Info($"[FETCH SUCCESS] Fetch item ({uid}) counted for faction '{returnPointFaction}'. Total: {fetchObj.AmountFetchedPerFaction[returnPointFaction]}/{fetchObj.AmountToFetch}");
        }
        var objComp = EnsureComp<AuObjectiveComponent>(comp.ObjectiveUid);
        if (objComp.FactionNeutral)
        {
            if (fetchObj.AmountFetchedPerFaction[returnPointFaction] < fetchObj.AmountToFetch)
                return;

            _logs.Info($"[FETCH SUCCESS] Neutral Objective ({comp.ObjectiveUid}) completed for faction '{returnPointFaction}'!");
            _objectiveSystem.CompleteObjectiveForFaction(comp.ObjectiveUid, objComp, returnPointFaction);
        }
        else
        {
            if (returnPointFaction != objComp.Faction.ToLowerInvariant())
                return;
            if (fetchObj.AmountFetchedPerFaction[returnPointFaction] < fetchObj.AmountToFetch)
                return;

            _logs.Info($"[FETCH SUCCESS] Objective ({comp.ObjectiveUid}) completed for faction '{returnPointFaction}'!");
            _objectiveSystem.CompleteObjectiveForFaction(comp.ObjectiveUid, objComp, returnPointFaction);
        }
    }

    private void OnReturnPointDragDropTarget(EntityUid uid, FetchObjectiveReturnPointComponent comp, ref DragDropTargetEvent args)
    {
        if (!TryComp(args.Dragged, out AuFetchItemComponent? fetchItem))
            return;
        TryHandleFetchItemDropOrUndrag(args.Dragged, fetchItem);
    }

    /// <summary>
    /// Scans a 5-tile radius around the analyzer for entities matching any active non-marker fetch
    /// objective that belongs to the analyzer's faction. For every match found it directly credits
    /// that faction (incrementing AmountFetchedPerFaction and marking the item Fetched) and
    /// completes the objective when the threshold is reached — exactly as TryHandleFetchItemDropOrUndrag
    /// does for the legacy return-point flow. The analyzer machine is the return point.
    /// Returns the number of items newly fetched this scan.
    /// </summary>
    public int ScanForFetchItems(EntityUid analyzerUid)
    {
        if (!TryComp(analyzerUid, out TransformComponent? analyzerXform))
            return 0;

        // Read the analyzer's faction — this determines which objectives it can credit.
        var analyzerFaction = string.Empty;
        if (TryComp(analyzerUid, out AnalyzerComponent? analyzerComp))
            analyzerFaction = analyzerComp.Faction.ToLowerInvariant();

        var analyzerCoords = analyzerXform.Coordinates;
        var totalFetched = 0;

        var query = EntityQueryEnumerator<FetchObjectiveComponent, AuObjectiveComponent>();
        while (query.MoveNext(out var objUid, out var fetchComp, out var auComp))
        {
            if (!auComp.Active)
                continue;

            // New-behavior objectives only (UseMarkers == false).
            if (fetchComp.UseMarkers)
                continue;

            if (string.IsNullOrEmpty(fetchComp.EntityToSpawn))
                continue;

            // Faction gate: skip objectives that don't belong to this analyzer's faction.
            // Faction-neutral objectives are open to any analyzer.
            // An analyzer with no faction set is a dev fallback and sees everything.
            if (!string.IsNullOrEmpty(analyzerFaction) && !auComp.FactionNeutral)
            {
                if (auComp.Faction.ToLowerInvariant() != analyzerFaction)
                    continue;
            }

            // The faction we are crediting for this objective.
            var creditFaction = string.IsNullOrEmpty(analyzerFaction)
                ? auComp.Faction.ToLowerInvariant()
                : analyzerFaction;

            var fetchedThisObjective = 0;

            foreach (var ent in _lookup.GetEntitiesInRange(analyzerCoords, 5f))
            {
                if (ent == analyzerUid || ent == objUid)
                    continue;

                if (!TryComp(ent, out MetaDataComponent? meta))
                    continue;

                var proto = meta.EntityPrototype?.ID;
                if (proto == null || proto != fetchComp.EntityToSpawn)
                    continue;

                // Attach the fetch-item component if not already present, then check if already fetched.
                var itemComp = EnsureComp<AuFetchItemComponent>(ent);
                if (itemComp.Fetched)
                    continue;

                // Link the item to this objective (in case it was just created).
                itemComp.FetchObjective = fetchComp;
                itemComp.ObjectiveUid = objUid;

                // Credit the faction — mirrors the return-point logic in TryHandleFetchItemDropOrUndrag.
                fetchComp.AmountFetchedPerFaction.TryAdd(creditFaction, 0);

                fetchComp.AmountFetchedPerFaction[creditFaction]++;
                itemComp.Fetched = true;
                totalFetched++;
                fetchedThisObjective++;

                _logs.Info($"[FETCH SCAN] Item {ent} ({proto}) fetched for faction '{creditFaction}', objective {objUid}. " +
                    $"Total: {fetchComp.AmountFetchedPerFaction[creditFaction]}/{fetchComp.AmountToFetch}");
            }

            if (fetchedThisObjective == 0)
                continue;

            // Check completion — same logic as TryHandleFetchItemDropOrUndrag.
            fetchComp.AmountFetchedPerFaction.TryGetValue(creditFaction, out var totalForFaction);
            if (auComp.FactionNeutral)
            {
                if (totalForFaction < fetchComp.AmountToFetch)
                    continue;

                _logs.Debug($"[FETCH SCAN] Objective ({objUid}) completed for faction '{creditFaction}'!");
                _objectiveSystem.CompleteObjectiveForFaction(objUid, auComp, creditFaction);
            }
            else
            {
                if (creditFaction != auComp.Faction.ToLowerInvariant()
                    || totalForFaction < fetchComp.AmountToFetch)
                    continue;

                _logs.Debug($"[FETCH SCAN] Objective ({objUid}) completed for faction '{creditFaction}'!");
                _objectiveSystem.CompleteObjectiveForFaction(objUid, auComp, creditFaction);
            }
        }

        return totalFetched;
    }

    /// <summary>
    /// Completes a fetch objective for the given faction. Used by external systems (e.g. AnalyzerSystem)
    /// that need to complete an objective without going through the full item-drop flow.
    /// </summary>
    public void CompleteFetchObjective(EntityUid uid, AuObjectiveComponent auComp, string faction)
    {
        _objectiveSystem.CompleteObjectiveForFaction(uid, auComp, faction);
    }

    /// <summary>
    /// Resets and respawns a fetch objective for repeating objectives.
    /// </summary>
    public void ResetAndRespawnFetchObjective(EntityUid uid, FetchObjectiveComponent fetchComp)
    {
        fetchComp.AmountFetched = 0;
        fetchComp.AmountFetchedPerFaction.Clear();
        if (!fetchComp.RespawnOnRepeat)
            return;

        fetchComp.ItemsSpawned = false; // Reset so items can respawn
        StartupFetchObjective(uid, fetchComp);
    }


    private void OnFetchItemDestroyed(EntityUid uid, AuFetchItemComponent comp, ref EntityTerminatingEvent args)
    {
        var fetchObj = comp.FetchObjective;
        if (comp.Fetched ||
            comp.ObjectiveUid == EntityUid.Invalid ||
            TerminatingOrDeleted(comp.ObjectiveUid) ||
            !TryComp<AuObjectiveComponent>(comp.ObjectiveUid, out var objComp))
            return;

        int unfetched = 0;
        var query = EntityQueryEnumerator<AuFetchItemComponent>();
        while (query.MoveNext(out var ent, out var itemComp))
        {
            if (itemComp.FetchObjective == fetchObj && !itemComp.Fetched && ent != uid)
                unfetched++;
        }

        var factions = objComp.FactionNeutral ? objComp.Factions : [objComp.Faction];
        foreach (var faction in factions)
        {
            var factionKey = faction.ToLowerInvariant();
            fetchObj.AmountFetchedPerFaction.TryGetValue(factionKey, out int alreadyFetched);
            int possible = alreadyFetched + unfetched;
            if (possible >= fetchObj.AmountToFetch)
                continue;
            if (!objComp.FactionStatuses.TryGetValue(factionKey, out var status)
                || status != AuObjectiveComponent.ObjectiveStatus.Incomplete)
                continue;

            objComp.FactionStatuses[factionKey] = AuObjectiveComponent.ObjectiveStatus.Failed;
            _logs.Info($"[FETCH FAIL] Objective ({comp.ObjectiveUid}) failed for faction '{factionKey}' due to destroyed fetch items");
            // Optionally, refresh consoles or notify
            _objectiveSystem.AwardPointsToFaction(factionKey, objComp); // Optionally award 0 points to trigger UI update
        }
    }

    // Markers are being used up, so that we don't spawn multiple high value objs on the same spot
    public void SpawnMissingFetchObjectives(string presetId,
        MapId targetMap,
        ObjectiveMasterComponent master,
        List<(EntityUid Uid, AuObjectiveComponent Comp)> allObjectives,
        IPrototypeManager proto)
    {
        // Gather unused generic marker positions
        var markerPositions = new List<EntityCoordinates>();
        var markerQuery = EntityQueryEnumerator<FetchObjectiveMarkerComponent, TransformComponent>();
        while (markerQuery.MoveNext(out _, out var marker, out var xform))
        {
            if (marker is { Generic: true, Used: false } && xform.MapID == targetMap)
                markerPositions.Add(xform.Coordinates);
        }

        if (markerPositions.Count == 0)
        {
            _logs.Warning("[OBJ SPAWN] No generic fetch markers found, mappers must place them!");
            return;
        }

        // Shuffle markers
        var rng = new Random();
        for (int i = markerPositions.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (markerPositions[i], markerPositions[j]) = (markerPositions[j], markerPositions[i]);
        }
        int markerIdx = 0;

        // Per-faction, per-level spawning respecting limits
        foreach (var faction in new[] { "govfor", "opfor", "clf", "scientist" })
        {
            var factionData = master.GetOrCreateFactionData(faction);
            int maxMinor = factionData.MinorObjectives;
            int maxMajor = factionData.MajorObjectives;

            int currentMinor = allObjectives.Count(o =>
                !o.Comp.Active &&
                o.Comp.Factions.Any(f => f.ToLowerInvariant() == faction) &&
                o.Comp.ObjectiveLevel == 1 &&
                o.Comp.ApplicableModes.Any(m => m.Equals(presetId, StringComparison.OrdinalIgnoreCase)));

            int currentMajor = allObjectives.Count(o =>
                !o.Comp.Active &&
                o.Comp.Factions.Any(f => f.ToLowerInvariant() == faction) &&
                o.Comp.ObjectiveLevel == 2 &&
                o.Comp.ApplicableModes.Any(m => m.Equals(presetId, StringComparison.OrdinalIgnoreCase)));

            SpawnObjectivesOfType(faction, 1, Math.Max(0, maxMinor - currentMinor), presetId, markerPositions, ref markerIdx, allObjectives, proto);
            SpawnObjectivesOfType(faction, 2, Math.Max(0, maxMajor - currentMajor), presetId, markerPositions, ref markerIdx, allObjectives, proto);
        }

        // Neutral objectives
        int currentNeutral = allObjectives.Count(o =>
            o.Comp is { Active: false, FactionNeutral: true } &&
            o.Comp.ApplicableModes.Any(m => m.Equals(presetId, StringComparison.OrdinalIgnoreCase)));

        SpawnObjectivesOfType(null, 1, Math.Max(0, master.MaxNeutralObjectives - currentNeutral), presetId, markerPositions, ref markerIdx, allObjectives, proto);
    }

    private void SpawnObjectivesOfType(string? faction,
        int level,
        int count,
        string presetId,
        List<EntityCoordinates> markerPositions,
        ref int markerIdx,
        List<(EntityUid Uid, AuObjectiveComponent Comp)> allObjectives,
        IPrototypeManager proto)
    {
        if (count <= 0) return;

        var compFactory = EntityManager.ComponentFactory;
        var candidates = new List<EntityPrototype>();

        foreach (var p in proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (!p.TryComp<AuObjectiveComponent>(out var objComp, compFactory))
                continue;

            if (!objComp.ApplicableModes.Any(m => m.Equals(presetId, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (faction != null)
            {
                if (objComp.Factions.All(f => f.ToLowerInvariant() != faction))
                    continue;
            }
            else
            {
                if (!objComp.FactionNeutral)
                    continue;
            }

            if (objComp.ObjectiveLevel != level)
                continue;

            if (allObjectives.Any(o => o.Comp.ID == objComp.ID))
                continue;

            candidates.Add(p);
        }

        var rng = new Random();
        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int idx = rng.Next(candidates.Count);
            var chosenProto = candidates[idx];
            var coords = markerPositions[markerIdx % markerPositions.Count];
            markerIdx++;

            Spawn(chosenProto.ID, coords);
            _logs.Debug($"[OBJ SPAWN] Spawned missing objective '{chosenProto.ID}' at {coords} for {faction ?? "neutral"} L{level}.");

            candidates.RemoveAt(idx);
        }
    }
}
