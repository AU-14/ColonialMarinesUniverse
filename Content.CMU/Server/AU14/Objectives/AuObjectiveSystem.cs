using System.Linq;
using System.Numerics;
using Content.Server._CMU14.RoundStatistics;
using Content.Server.AU14.Objectives.Arrest;
using Content.Server.AU14.Objectives.Destroy;
using Content.Server.AU14.Objectives.Fetch;
using Content.Server.AU14.Objectives.Interact;
using Content.Server.AU14.Objectives.Kill;
using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Vendors;
using Content.Shared.AU14.Objectives;
using Content.Shared.AU14.Objectives.Arrest;
using Content.Shared.AU14.Objectives.Destroy;
using Content.Shared.AU14.Objectives.Fetch;
using Content.Shared.AU14.Objectives.Interact;
using Content.Shared.AU14.Objectives.Kill;
using Content.Shared._CMU14.Threats;
using Robust.Shared.Prototypes;
using Robust.Server.Player;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.AU14.Objectives;

public sealed partial class AuObjectiveSystem : AuSharedObjectiveSystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private ObjectivesConsoleSystem _objectivesConsoleSystem = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private CMURoundStatisticsSystem _roundStats = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoonSpawnRuleSystem = default!;
    [Dependency] private AuFetchObjectiveSystem _fetchObjectiveSystem = default!;
    [Dependency] private AuKillObjectiveSystem _killObjectiveSystem = default!;
    [Dependency] private AuArrestObjectiveSystem _arrestObjectiveSystem = default!;
    [Dependency] private AuDestroyObjectiveSystem _destroyObjectiveSystem = default!;
    [Dependency] private AuInteractObjectiveSystem _interactObjectiveSystem = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private SharedCMAutomatedVendorSystem _vendorSystem = default!;
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    private readonly List<(EntityUid Uid, AuObjectiveComponent Comp)> _allObjectives = new();
    private EntityUid _objectiveMasterUid = EntityUid.Invalid;
    private MapId _planetMapId = MapId.Nullspace;
    private ISawmill _logs = default!;
    public bool IsWinActive { get; set; }

    // Preface: the redundancies are because I want to keep backwards compatibility (not require mastercomps on maps)
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AuObjectiveComponent, ObjectiveActivatedEvent>(OnObjectiveActivated);
        SubscribeLocalEvent<AuObjectiveComponent, ComponentStartup>(OnObjectiveStartup);
        SubscribeLocalEvent<AuObjectiveComponent, ComponentShutdown>(OnObjectiveShutdown);
        SubscribeLocalEvent<SpendWinPointsEvent>(OnSpendWinPoints);
        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad);
        _logs = Logger.GetSawmill("objectives");
    }

    private void OnObjectiveShutdown(EntityUid uid, AuObjectiveComponent component, ref ComponentShutdown args) => _allObjectives.RemoveAll(o => o.Uid == uid);
    public override void Shutdown()
    {
        base.Shutdown();
        _planetMapId = MapId.Nullspace;
        _allObjectives.Clear();
    }

    private void OnObjectiveStartup(EntityUid uid, AuObjectiveComponent component, ref ComponentStartup args)
    {
        _logs.Debug($"[OBJ START] AuObjectiveComponent started: [{ToPrettyString(uid)}]");
        _allObjectives.Add((uid, component));
        InitializeObjectiveStatuses(component);
    }

    private void OnPostGameMapLoad(PostGameMapLoad ev)
    {
        IsWinActive = false;
        var gameMap = ev.GameMap;
        var map = ev.Map;
        var grids = ev.Grids.ToArray();
        Timer.Spawn(0, () => SetupPostGameMapLoad(gameMap, map, grids));
    }

    private void SetupPostGameMapLoad(GameMapPrototype gameMap, MapId mapId, IReadOnlyList<EntityUid> grids)
    {
        var presetId = _gameTicker.Preset?.ID;
        if (string.IsNullOrWhiteSpace(presetId))
            return;

        var selectedPlanet = _auRoundSystem.GetSelectedPlanet();
        if (selectedPlanet == null
            || !gameMap.ID.Equals(selectedPlanet.MapId, StringComparison.OrdinalIgnoreCase))
        {
            _logs.Debug($"[OBJ MASTER] OnPostGameMapLoad: map '{gameMap.ID}' is not the voted planet '{selectedPlanet?.MapId}', skipping.");
            return;
        }

        // first grid index could be a dropship/faulty mapped grid (need to grab largest)
        EntityUid? bestPlanetGrid = null;
        float bestArea = -1f;
        foreach (var grid in grids)
        {
            if (!TryComp<MapGridComponent>(grid, out var gridComp)) continue;
            var area = gridComp.LocalAABB.Width * gridComp.LocalAABB.Height;
            if (!(area > bestArea))
                continue;

            bestArea = area;
            bestPlanetGrid = grid;
        }

        if (bestPlanetGrid == null)
        {
            _logs.Warning($"[OBJ MASTER] OnPostGameMapLoad: planet map has no valid grids!");
            return;
        }
        _logs.Debug($"[OBJ MASTER] OnPostGameMapLoad: planet map '{selectedPlanet.MapId}' loaded as main map, with valid grid ({bestPlanetGrid.Value})");

        _planetMapId = mapId;
        EnsureComp<RMCPlanetComponent>(_mapSystem.GetMap(mapId));
        var hasPlanetMaster = false;
        var masterScan = EntityQueryEnumerator<ObjectiveMasterComponent, TransformComponent>();
        while (masterScan.MoveNext(out var mUid, out _, out var mXform))
        {
            if (mXform.MapID != mapId)
                continue;

            hasPlanetMaster = true;
            _objectiveMasterUid = mUid;
            break;
        }

        if (hasPlanetMaster)
        {
            _logs.Debug($"[OBJ MASTER] OnPostGameMapLoad: ObjectiveMaster loaded from the planet, running Main()");
            Timer.Spawn(0, Main);
            return;
        }

        bool spawnedIn = false;
        var compFactory = EntityManager.ComponentFactory;
        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (!proto.TryComp<ObjectiveMasterComponent>(out var masterComp, compFactory)
                    || !string.Equals(masterComp.GamePreset, presetId, StringComparison.OrdinalIgnoreCase))
                continue;

            _objectiveMasterUid = Spawn(proto.ID, new EntityCoordinates(bestPlanetGrid.Value, Vector2.Zero));
            spawnedIn = true;
            _logs.Warning($"[OBJ MASTER] OnPostGameMapLoad: auto-spawned MISSING ObjectiveMaster '{proto.ID}' for preset '{presetId}' on planet '{gameMap.MapName}'");
            break;
        }

        // Fallback if the map didn't even have a master prototype at all
        if (!spawnedIn)
        {
            _objectiveMasterUid = Spawn("ObjectiveMasterBaseDistress", new EntityCoordinates(bestPlanetGrid.Value, Vector2.Zero));
            _logs.Warning($"[OBJ MASTER] OnPostGameMapLoad: no ObjectiveMaster found for preset '{presetId}', spawned fallback 'ObjectiveMasterBaseDistress'");
        }

        if (TryComp<ObjectiveMasterComponent>(_objectiveMasterUid, out var master))
            _fetchObjectiveSystem.SpawnMissingFetchObjectives(presetId, mapId, master, _allObjectives, _proto);
        Timer.Spawn(0, Main);
    }

    private void OnObjectiveActivated(EntityUid uid, AuObjectiveComponent component, ref ObjectiveActivatedEvent args)
    {
        if (HasComp<FetchObjectiveComponent>(uid))
            _fetchObjectiveSystem.ActivateFetchObjectiveIfNeeded(uid, component);

        if (HasComp<KillObjectiveComponent>(uid))
            _killObjectiveSystem.ActivateKillObjectiveIfNeeded(uid, component);

        if (HasComp<ArrestObjectiveComponent>(uid))
            _arrestObjectiveSystem.ActivateArrestObjectiveIfNeeded(uid, component);

        if (HasComp<DestroyObjectiveComponent>(uid))
            _destroyObjectiveSystem.ActivateDestroyObjectiveIfNeeded(uid, component);

        if (HasComp<InteractObjectiveComponent>(uid))
            _interactObjectiveSystem.ActivateInteractObjectiveIfNeeded(uid, component);
    }

    private void OnSpendWinPoints(SpendWinPointsEvent ev)
    {
        if (string.IsNullOrEmpty(ev.Team) || ev.Team == Team.None)
            return;

        // Ensure we have a reference to the authoritative ObjectiveMaster
        if (GetOrReselectObjMaster() is not { } master)
        {
            _logs.Error("[OBJ COMPLETE] OnSpendWinPoints called with null ObjectiveMaster!");
            return;
        }

        var key = ev.Team.ToLowerInvariant();
        var data = master.GetOrCreateFactionData(key);

        // No need to call Dirty on the component reference directly; find the entity to mark dirty for replication
        data.CurrentWinPoints = Math.Max(0, data.CurrentWinPoints - ev.Amount);
        DirtyObjectiveMaster();
        // Update all vendor caches so their BUIs reflect the new balance
        _vendorSystem.UpdateVendorFactionPointsCache(key, data.CurrentWinPoints);
    }

    /// <summary>
    /// Awards a raw number of win points directly to a faction without requiring an objective.
    /// Used by systems like the CLF Analyzer cash insertion that earn points outside the objective flow.
    /// </summary>
    public void AwardRawPointsToFaction(string faction, int points) => ApplyWinPoints(faction, points);

    public void AwardPointsToFaction(string faction, AuObjectiveComponent objective) =>
        ApplyWinPoints(faction, objective.CustomPoints == 0
            ? (objective.ObjectiveLevel == 1 ? 5 : 20)
            : objective.CustomPoints);

    public void CompleteObjectiveForFaction(EntityUid uid, AuObjectiveComponent objective, string completingFaction)
    {
        if (_planetMapId == MapId.Nullspace || Transform(uid).MapID != _planetMapId)
            return;

        // NOTE: repeating neutral objs?
        if (objective.FactionStatuses.ContainsValue(AuObjectiveComponent.ObjectiveStatus.Completed))
            return;

        var factionKey = completingFaction.ToLowerInvariant();
        MarkFactionCompleted(objective, factionKey);
        AwardAndRefresh(objective, completingFaction);

        if (objective.ObjectiveLevel == 3)
        {
            // Only end the round automatically for final objectives if their FinalType is InstantWin.
            if (objective.FinalType == AuObjectiveComponent.FinalObjectiveType.InstantWin)
                DeclareObjectiveVictory(completingFaction, objective.RoundEndMessage);
            else
                _logs.Info($"[OBJ FINAL] Final objective '{objective.objectiveDescription}' completed for faction '{completingFaction}' as Boon; not ending the round.");
        }

        TryUnlockOrSpawnNextTier(uid, objective, completingFaction);

        if (!objective.Repeating)
        {
            Dirty(uid, objective);
            return;
        }

        if (objective.MaxRepeatable is { } maxRepeat && objective.TimesCompleted + 1 >= maxRepeat)
        {
            objective.TimesCompleted = maxRepeat;
            objective.Active = false;
            MarkAllFactionsCompleted(objective, factionKey);
            Dirty(uid, objective);
            _logs.Debug($"[OBJ REPEAT] Objective '{objective.objectiveDescription}' reached max repeats ({maxRepeat}), marking as completed.");
            _objectivesConsoleSystem.RefreshConsolesForFaction(completingFaction);
            return;
        }

        objective.TimesCompleted++;
        ResetObjectiveStatuses(objective);
        ResetObjectiveComponents(uid);
        objective.Active = true;
        Dirty(uid, objective);
        RaiseLocalEvent(uid, new ObjectiveActivatedEvent());
        _logs.Debug($"[OBJ REPEAT] Restarted repeating objective '{objective.objectiveDescription}'...");

        // Refresh consoles for all relevant factions
        if (objective.FactionNeutral)
            foreach (var faction in objective.Factions)
                _objectivesConsoleSystem.RefreshConsolesForFaction(faction);
        else
            _objectivesConsoleSystem.RefreshConsolesForFaction(objective.Faction);
    }

    /// <summary>
    /// Ends the round with an objective victory for the supplied faction and records the result.
    /// </summary>
    public void DeclareObjectiveVictory(string faction, string? roundEndMessage)
    {
        var message = roundEndMessage ?? string.Empty;
        var roundEndText = Loc.GetString("objectives-system-round-end",
            ("faction", faction.ToUpperInvariant()),
            ("message", message));

        _roundStats.RecordObjectiveVictory(faction);
        _gameTicker.EndRound(roundEndText);
    }

    /// <summary>Reset faction statuses to Incomplete before a repeat.</summary>
    private void ResetObjectiveStatuses(AuObjectiveComponent objective)
    {
        foreach (var key in objective.FactionStatuses.Keys.ToList())
            objective.FactionStatuses[key] = AuObjectiveComponent.ObjectiveStatus.Incomplete;
    }

    // Returns all inactive entities that have the preset in applicableModes
    // If presetId is null, all inactive objectives are returned
    private List<(EntityUid Uid, AuObjectiveComponent Comp)> GetInactiveObjectives(string? presetId = null, MapId? mapId = null)
    {
        var objectives = new List<(EntityUid Uid, AuObjectiveComponent Comp)>();
        int count = 0;
        foreach (var (uid, comp) in _allObjectives)
        {
            if (!Exists(uid)) continue;
            count++;

            if (comp.Active) continue;
            _logs.Debug($"[OBJ GET] {count}: Found objective entity {uid} ({comp.objectiveDescription}), Active={comp.Active}"); // FIXME: remove debug spam

            if (mapId != null && Transform(uid).MapID != mapId)
                continue;

            bool modeMatch = true;
            if (presetId != null)
            {
                // neutral objectives to appear if applicableModes is empty or contains the preset
                if (comp.FactionNeutral)
                    modeMatch = comp.ApplicableModes.Count == 0 || comp.ApplicableModes.Any(m => m.Equals(presetId, StringComparison.OrdinalIgnoreCase));
                else
                    modeMatch = comp.ApplicableModes.Any(m => m.Equals(presetId, StringComparison.OrdinalIgnoreCase));
            }

            if (!modeMatch)
                continue;

            objectives.Add((uid, comp));
        }

        _logs.Debug($"[OBJ GET]     Found {count} objectives, {objectives.Count} eligible.");
        return objectives;
    }

    // TODO: this method re-runs a lot (performance concern)
    private List<(EntityUid Uid, AuObjectiveComponent Comp)> SelectObjectives(string faction,
    List<(EntityUid Uid, AuObjectiveComponent Comp)> allObjectives,
    int? objectiveLevel = null,
    int maxCount = int.MaxValue)
    {
        var playercount = _playerManager.PlayerCount;
        var factionLower = faction.ToLowerInvariant();
        var selected = new List<(EntityUid Uid, AuObjectiveComponent Comp)>();
        string? selectedPlatoonId = null;

        // Get the current threat prototype if available
        ThreatPrototype? currentThreat = _auRoundSystem.SelectedThreat;
        switch (factionLower)
        {
            case "govfor":
                selectedPlatoonId = _platoonSpawnRuleSystem.SelectedGovforPlatoon?.ID;
                break;
            case "opfor":
                selectedPlatoonId = _platoonSpawnRuleSystem.SelectedOpforPlatoon?.ID;
                break;
                // NOTE: Add more cases if other factions can have platoons
        }

        foreach (var (objUid, objective) in allObjectives)
        {
            if (objective.FactionNeutral) continue;
            // Exclude win/final objectives (ObjectiveLevel == 3) from roundstart unless RollAnyway is true
            if (objective is { ObjectiveLevel: 3, RollAnyway: false }) continue;

            bool factionMatch = objective.Factions.Any(f => f.ToLowerInvariant() == factionLower);
            bool maxPlayersMatch = objective.Maxplayers == 0 || objective.Maxplayers >= playercount;
            bool minPlayersMatch = objective.MinPlayers == 0 || playercount >= objective.MinPlayers;
            bool levelMatch = objectiveLevel == null
                ? (objective.ObjectiveLevel == 1 || objective.ObjectiveLevel == 2)
                : (objective.ObjectiveLevel == objectiveLevel);

            // Threat objective whitelist check
            bool threatWhitelistMatch = true;
            if (currentThreat is { ObjectiveWhitelist.Count: > 0 })
            {
                // Only allow objectives whose id is in the threat's whitelist
                if (!currentThreat.ObjectiveWhitelist.Contains(objective.ID))
                    threatWhitelistMatch = false;
            }

            if (!factionMatch) continue;
            if (!maxPlayersMatch) continue;
            if (!minPlayersMatch) continue;
            if (!levelMatch) continue;
            if (!threatWhitelistMatch) continue;
            if (selectedPlatoonId != null && objective.BlacklistedPlatoons.Contains(selectedPlatoonId)) continue;

            // --- WhitelistedPlatoons logic ---
            if (objective.WhitelistedPlatoons.Count > 0 && (selectedPlatoonId == null || !objective.WhitelistedPlatoons.Contains(selectedPlatoonId)))
                continue;

            selected.Add((objUid, objective));
        }
        // Randomly select up to maxCount objectives if more are available
        if (selected.Count > maxCount)
            selected = WeightedRandomPick(selected, maxCount);

        return selected;
    }

    /// <summary>Reset objective‑specific components (fetch, kill, interact) for a repeat.</summary>
    private void ResetObjectiveComponents(EntityUid uid)
    {
        if (TryComp(uid, out FetchObjectiveComponent? fetchComp))
            _fetchObjectiveSystem.ResetAndRespawnFetchObjective(uid, fetchComp);

        if (TryComp(uid, out KillObjectiveComponent? killComp))
        {
            if (killComp.RespawnOnRepeat)
                killComp.MobsSpawned = false;
            killComp.AmountKilledPerFaction.Clear();
        }

        if (TryComp(uid, out InteractObjectiveComponent? interactComp))
            _interactObjectiveSystem.ResetInteractObjective(uid, interactComp);
    }

    // Checks if a Kill objective is completable: at least one entity is marked for this objective
    private bool IsKillObjectiveCompletable(EntityUid uid, AuObjectiveComponent _)
    {
        // Only care about objectives with a KillObjectiveComponent
        if (!TryComp(uid, out KillObjectiveComponent? killObj))
            return false;
        // If the objective will spawn a mob and hasn't yet, it will be completable after activation
        if (killObj is { SpawnMob: true, MobsSpawned: false })
            return true;
        var query = EntityQueryEnumerator<MarkedForKillComponent>();
        while (query.MoveNext(out var _, out var markComp))
        {
            if (markComp.AssociatedObjectives.ContainsKey(uid))
                return true;
        }
        return false;
    }

    /// <summary>Mark the completing faction as Completed. For one‑shot neutrals, fail all other factions.</summary>
    private void MarkFactionCompleted(AuObjectiveComponent objective, string factionKey)
    {
        if (objective.FactionNeutral)
        {
            if (!objective.FactionStatuses.TryGetValue(factionKey, out var status)
                    || status != AuObjectiveComponent.ObjectiveStatus.Incomplete)
                return;

            objective.FactionStatuses[factionKey] = AuObjectiveComponent.ObjectiveStatus.Completed;
            _logs.Debug($"[OBJ COMPLETE] Set FactionStatuses['{factionKey}'] = Completed");

            // Only mark other factions as Failed if NOT repeating
            if (objective.Repeating)
                return;

            foreach (var key in objective.FactionStatuses.Keys.ToList())
            {
                if (key == factionKey
                    || objective.FactionStatuses[key] != AuObjectiveComponent.ObjectiveStatus.Incomplete)
                    continue;

                objective.FactionStatuses[key] = AuObjectiveComponent.ObjectiveStatus.Failed;
                _logs.Debug($"[OBJ COMPLETE] Set FactionStatuses['{key}'] = Failed");
            }
        }
        else
        {
            objective.FactionStatuses[factionKey] = AuObjectiveComponent.ObjectiveStatus.Completed;
            _logs.Debug($"[OBJ COMPLETE] Set FactionStatuses['{factionKey}'] = Completed");
        }
    }

    /// <summary>Set all relevant faction statuses to Completed (used when max repeats reached).</summary>
    private void MarkAllFactionsCompleted(AuObjectiveComponent objective, string factionKey)
    {
        if (objective.FactionNeutral)
        {
            foreach (var key in objective.FactionStatuses.Keys.ToList())
                objective.FactionStatuses[key] = AuObjectiveComponent.ObjectiveStatus.Completed;
        }
        else
        {
            objective.FactionStatuses[factionKey] = AuObjectiveComponent.ObjectiveStatus.Completed;
        }
    }

    /// <summary>Award points and refresh consoles for the completing faction(s).</summary>
    private void AwardAndRefresh(AuObjectiveComponent objective, string completingFaction)
    {
        AwardPointsToFaction(completingFaction, objective);
        if (objective.FactionNeutral)
            foreach (var f in objective.Factions)
                _objectivesConsoleSystem.RefreshConsolesForFaction(f);
        else
            _objectivesConsoleSystem.RefreshConsolesForFaction(completingFaction);
    }

    private void TryUnlockOrSpawnNextTier(EntityUid completedUid, AuObjectiveComponent completedObjective, string completingFaction)
    {
        _logs.Info($"[OBJ TIER] Attempting to spawn next-tier for prototype='{completedObjective.NextTier}' for faction {completingFaction}");

        // Nothing to do if NextTier is empty
        var nextTier = completedObjective.NextTier;
        if (!nextTier.HasValue)
            return;

        var protoIdStr = nextTier.Value.Id;
        if (string.IsNullOrEmpty(protoIdStr))
            return;

        // Ensure we have the completed objective's transform to spawn at the same location
        if (!TryComp(completedUid, out TransformComponent? completedXform))
            return;

        // Ensure the referenced prototype actually contains an AuObjectiveComponent
        if (!nextTier.Value.TryGet(out AuObjectiveComponent? _, _proto, EntityManager.ComponentFactory))
        {
            _logs.Warning($"[OBJ TIER] Next tier prototype '{protoIdStr}' does not contain an AuObjectiveComponent or is missing!");
            return;
        }

        // Always spawn a new entity from the prototype (do not try to find and reuse an existing inactive objective)
        var newEnt = Spawn(protoIdStr, completedXform.Coordinates);
        if (TryComp(newEnt, out AuObjectiveComponent? newObjComp))
        {
            newObjComp.FactionStatuses.Clear(); // clear stale data from startups
            newObjComp.Faction = newObjComp.FactionNeutral ? string.Empty : completingFaction.ToLowerInvariant();
            newObjComp.Active = true;
            InitializeObjectiveStatuses(newObjComp);
            Dirty(newEnt, newObjComp);
            RaiseLocalEvent(newEnt, new ObjectiveActivatedEvent());

            if (newObjComp.FactionNeutral)
                foreach (var f in newObjComp.Factions)
                    _objectivesConsoleSystem.RefreshConsolesForFaction(f);
            else
                _objectivesConsoleSystem.RefreshConsolesForFaction(newObjComp.Faction);

            _logs.Info($"[OBJ TIER] Activated next-tier objective '{newObjComp.objectiveDescription}' for '{(newObjComp.FactionNeutral ? "all listed factions" : completingFaction)}'");
        }
        else
            _logs.Warning($"[OBJ TIER] Spawned prototype {protoIdStr} but it does not contain an AuObjectiveComponent!");
    }

    private void ApplyWinPoints(string faction, int points)
    {
        if (GetOrReselectObjMaster() is not { } master)
            return;

        var key = faction.ToLowerInvariant();
        var data = master.GetOrCreateFactionData(key);

        // Sync the authoritative master to the actual entity for replication
        data.CurrentWinPoints += points;
        DirtyObjectiveMaster();
        // Push new balance to all objective-point vendors so their BUIs reflect it
        // regardless of whether the ObjectiveMasterComponent entity is in the client's PVS.
        _vendorSystem.UpdateVendorFactionPointsCache(key, data.CurrentWinPoints);

        if (!master.FinalObjectiveGivenFactions.Contains(key) && data.CurrentWinPoints >= data.RequiredWinPoints)
            TryActivateFinalObjective(key);
    }

    private void TryActivateFinalObjective(string factionKey)
    {
        // Only activate a final objective if it is completable
        var finalObjectives = new List<(EntityUid Uid, AuObjectiveComponent Comp)>();
        var finalObjQuery = AllEntityQuery<AuObjectiveComponent>();
        while (finalObjQuery.MoveNext(out var uid, out var comp))
        {
            if (_planetMapId == MapId.Nullspace || Transform(uid).MapID != _planetMapId)
                continue;

            if (comp is { Active: false, ObjectiveLevel: 3 }
                && comp.Factions.Any(f => f.ToLowerInvariant() == factionKey))
            {
                finalObjectives.Add((uid, comp));
            }
        }

        // Try to find a completable final objective
        foreach (var (uid, comp) in finalObjectives.OrderBy(_ => Random.Shared.Next()))
        {
            if (TryComp(uid, out KillObjectiveComponent? _) && !IsKillObjectiveCompletable(uid, comp))
                continue;

            comp.Faction = factionKey;
            InitializeObjectiveStatuses(comp);
            comp.Active = true;
            Dirty(uid, comp);
            RaiseLocalEvent(uid, new ObjectiveActivatedEvent());

            if (GetOrReselectObjMaster() is not { } master) return;
            master.FinalObjectiveGivenFactions.Add(factionKey);
            DirtyObjectiveMaster();

            IsWinActive = true;
            _logs.Info($"[OBJ FINAL] Activated '{comp.objectiveDescription}' for '{factionKey}', IsWinActive=true");
            return;
        }

        _logs.Warning($"[OBJ FINAL] No completable final objective found for faction '{factionKey}'. None activated!");
    }

    private void InitializeObjectiveStatuses(AuObjectiveComponent obj)
    {
        if (obj.FactionNeutral)
            foreach (var faction in obj.Factions)
                obj.FactionStatuses.TryAdd(faction.ToLowerInvariant(), AuObjectiveComponent.ObjectiveStatus.Incomplete);
        else if (!string.IsNullOrEmpty(obj.Faction))
            obj.FactionStatuses.TryAdd(obj.Faction.ToLowerInvariant(), AuObjectiveComponent.ObjectiveStatus.Incomplete);
    }

    private ObjectiveMasterComponent? GetOrReselectObjMaster()
    {
        if (_objectiveMasterUid.IsValid() && TryComp(_objectiveMasterUid, out ObjectiveMasterComponent? master))
            return master;

        if (_planetMapId == MapId.Nullspace)
        {
            _logs.Warning("[OBJ MASTER] GetOrReselectObjMaster called before planet map loaded.");
            return null;
        }

        var query = EntityQueryEnumerator<ObjectiveMasterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.MapID != _planetMapId)
                continue;

            _objectiveMasterUid = uid;
            return comp;
        }

        return null;
    }

    private void DirtyObjectiveMaster()
    {
        if (_objectiveMasterUid.IsValid() && TryComp(_objectiveMasterUid, out ObjectiveMasterComponent? master))
            Dirty(_objectiveMasterUid, master);
    }

    public void Main()
    {
        if (GetOrReselectObjMaster() is not { } master) return;
        var presetId = _gameTicker.Preset?.ID.ToLowerInvariant() ?? string.Empty;
        var modeObjectives = GetInactiveObjectives(presetId, Transform(_objectiveMasterUid).MapID);
        _logs.Info($"[OBJ MAIN] Preset='{presetId}', Eligible objectives={modeObjectives.Count}");

        if (modeObjectives.Count == 0)
        {
            _logs.Warning($"[OBJ MAIN]   No objectives passed filtering for preset '{presetId}':");
            foreach (var (_, comp) in _allObjectives.Take(30))
                _logs.Warning($"   {comp.objectiveDescription} - active={comp.Active} - neutral={comp.FactionNeutral} - modes=[{string.Join(", ", comp.ApplicableModes)}]");
        }

        string[] factions = presetId switch
        {
            "insurgency" => ["govfor", "clf", "scientist"],
            "forceonforce" => ["govfor", "opfor", "scientist"],
            "distresssignal" => ["govfor"],
            _ => ["scientist"] // corporate fallback (e.g. colonyfall)
        };

        foreach (var faction in factions)
        {
            try
            {
                var factionData = master.GetOrCreateFactionData(faction);
                ActivateFactionObjectives(faction, 1,
                    SelectObjectives(faction, modeObjectives, 1, GetRandomObjectiveCount(factionData.MinorObjectives, factionData.MinMinorObjectives)));
                ActivateFactionObjectives(faction, 2,
                    SelectObjectives(faction, modeObjectives, 2, GetRandomObjectiveCount(factionData.MajorObjectives, factionData.MinMajorObjectives)));
            }
            catch (Exception ex) { _logs.Error($"[OBJ FAIL] Failed to active {faction} objectives! {ex}"); }
        }

        try
        {
            // Gather all inactive neutral objectives that are applicable to this game mode
            var neutralCandidates = modeObjectives.Where(x => x.Comp is { FactionNeutral: true } && (x.Comp.ObjectiveLevel != 3 || x.Comp.RollAnyway)).ToList();
            int neutralCap = GetRandomObjectiveCount(master.MaxNeutralObjectives, master.MinNeutralObjectives);
            _logs.Info($"[OBJ NEUTRAL] Found {neutralCandidates.Count} neutral candidates, max allowed = {neutralCap}");

            // If we have more candidates than allowed, perform weighted random selection
            if (neutralCandidates.Count > neutralCap)
                neutralCandidates = WeightedRandomPick(neutralCandidates, neutralCap);

            foreach (var (uid, obj) in neutralCandidates)
            {
                obj.Active = true;
                Dirty(uid, obj);
                RaiseLocalEvent(uid, new ObjectiveActivatedEvent());
                _logs.Debug($"[OBJ NEUTRAL] Activated neutral objective '{obj.objectiveDescription}'");
            }
        }
        catch (Exception ex) { _logs.Error($"[OBJ NEUTRAL] Failed to activate neutral objectives: {ex.Message}!"); }
    }

    private static List<(EntityUid Uid, AuObjectiveComponent Comp)> WeightedRandomPick(
    List<(EntityUid Uid, AuObjectiveComponent Comp)> candidates, int count)
    {
        if (count <= 0 || candidates.Count == 0)
            return new List<(EntityUid Uid, AuObjectiveComponent Comp)>();

        var weighted = candidates
            .Select(obj => (obj.Uid, obj.Comp, Weight: Math.Max(1, obj.Comp.ObjectiveWeight)))
            .ToList();

        var chosen = new List<(EntityUid Uid, AuObjectiveComponent Comp)>();
        for (int i = 0; i < count && weighted.Count > 0; i++)
        {
            int totalWeight = weighted.Sum(x => x.Weight);
            int pick = Random.Shared.Next(totalWeight);
            int cumulative = 0;
            for (int j = 0; j < weighted.Count; j++)
            {
                cumulative += weighted[j].Weight;
                if (pick >= cumulative)
                    continue;

                chosen.Add((weighted[j].Uid, weighted[j].Comp));
                weighted.RemoveAt(j);
                break;
            }
        }
        return chosen;
    }

    private int GetRandomObjectiveCount(int max, int? min)
    {
        if (min is not { } minValue)
            return max;

        if (minValue < max)
            return Random.Shared.Next(minValue, max + 1);

        if (minValue > max)
            _logs.Warning($"[OBJ RANDOM] MinObjectives ({minValue}) > MaxObjectives ({max}), using maximums");

        return max;
    }

    private void ActivateFactionObjectives(string faction, int level,
    List<(EntityUid Uid, AuObjectiveComponent Comp)> objectives)
    {
        var levelName = level == 1 ? "minor" : "major";
        _logs.Debug($"[OBJ {faction.ToUpper()}] Activating {objectives.Count} {faction} {levelName} objectives");

        foreach (var (objUid, obj) in objectives)
        {
            obj.Faction = faction;
            InitializeObjectiveStatuses(obj);
            obj.Active = true;
            Dirty(objUid, obj);
            RaiseLocalEvent(objUid, new ObjectiveActivatedEvent());
            _logs.Debug($"[OBJ {faction.ToUpper()}] Activated {faction} {levelName}: {obj.objectiveDescription}"); // FIXME: remove debug spam
        }
    }

    public string GetOppositeFaction(string faction, string? mode) => (mode?.ToLowerInvariant(), faction) switch
    {
        ("forceonforce", "govfor") => "opfor",
        ("forceonforce", "opfor") => "govfor",
        ("distresssignal", "clf") => "govfor",
        ("distresssignal", "govfor") => "clf",
        ("insurgency", "clf") => "govfor",
        ("insurgency", "govfor") => "clf",
        _ => string.Empty,
    };

    public MapId? GetPlanetMapId() => _planetMapId != MapId.Nullspace ? _planetMapId : null;

    public (int current, int required) GetWinPoints(string faction)
    {
        if (GetOrReselectObjMaster() is not { } master)
            return (0, 0);
        var key = faction.ToLowerInvariant();
        var data = master.GetOrCreateFactionData(key);
        return (data.CurrentWinPoints, data.RequiredWinPoints);
    }
}
