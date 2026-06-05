using System.Linq;
using System.Numerics;
using Content.Server.AU14.Objectives.Arrest;
using Content.Server.AU14.Objectives.Destroy;
using Content.Server.AU14.Objectives.Fetch;
using Content.Server.AU14.Objectives.Interact;
using Content.Server.AU14.Objectives.Kill;
using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Vendors;
using Content.Shared.AU14.Objectives;
using Content.Shared.AU14.Objectives.Arrest;
using Content.Shared.AU14.Objectives.Destroy;
using Content.Shared.AU14.Objectives.Fetch;
using Content.Shared.AU14.Objectives.Interact;
using Content.Shared.AU14.Objectives.Kill;
using Content.Shared.AU14.Threats;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.AU14.Objectives;

public sealed partial class AuObjectiveSystem : AuSharedObjectiveSystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private ObjectivesConsoleSystem _objectivesConsoleSystem = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoonSpawnRuleSystem = default!;
    [Dependency] private AuFetchObjectiveSystem _fetchObjectiveSystem = default!;
    [Dependency] private AuKillObjectiveSystem _killObjectiveSystem = default!;
    [Dependency] private AuArrestObjectiveSystem _arrestObjectiveSystem = default!;
    [Dependency] private AuDestroyObjectiveSystem _destroyObjectiveSystem = default!;
    [Dependency] private AuInteractObjectiveSystem _interactObjectiveSystem = default!;
    [Dependency] private SharedCMAutomatedVendorSystem _vendorSystem = default!;
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private RMCPlanetSystem _rmcPlanetSystem = default!;
    [Dependency] private IMapManager _mapManager = default!;
    private EntityUid _objectiveMasterUid = EntityUid.Invalid;
    private ISawmill _logs = default!;
    public bool IsWinActive { get; set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AuObjectiveComponent, ObjectiveActivatedEvent>(OnObjectiveActivated);
        SubscribeLocalEvent<ObjectiveMasterComponent, ComponentStartup>(OnObjectiveMasterStartup);
        SubscribeLocalEvent<AuObjectiveComponent, ComponentStartup>(OnObjectiveStartup);
        SubscribeLocalEvent<MapComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SpendWinPointsEvent>(OnSpendWinPoints);
        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad);
        _logs = Logger.GetSawmill("objectives");
    }

    private void OnObjectiveMasterStartup(EntityUid uid, ObjectiveMasterComponent _comp, ref ComponentStartup _args)
    {
        Timer.Spawn(TimeSpan.FromSeconds(0.1), () =>
        {
            if (!Exists(uid)) return;

            var x = Transform(uid);
            var isOnPlanet = _rmcPlanetSystem.IsOnPlanet(x); // FIXME: race-condition waiting on OnPostGameMapLoad to add IsOnPlanet's required RMCPlanetComponent
            var proto = MetaData(uid).EntityPrototype?.ID ?? "null";
            if (isOnPlanet)
            {
                _logs.Debug($"[OBJ START]     Planet ObjectiveMaster ({uid}, proto={proto}) spawned at {x.Coordinates}, map={x.MapUid}, grid={x.GridUid}.");
                return;
            }

            _logs.Debug($"[OBJ START]     Ship ObjectiveMaster ({uid}, proto={proto}) spawned at {x.Coordinates}, map={x.MapUid}, grid={x.GridUid}.");
            // Main(); // NOTE: can handle re-runs gracefully, but I don't see the need for a ship mastercomp (yet)
        });
    }

    private void OnObjectiveStartup(EntityUid uid, AuObjectiveComponent component, ref ComponentStartup args)
    {
        _logs.Debug($"[OBJ START] AuObjectiveComponent started on {ToPrettyString(uid)}: \"{component.objectiveDescription}\"");
        InitializeObjectiveStatuses(component);
    }

    // TODO: Spaghet doesn't belong here - TESTING
    private void OnMapInit(Entity<MapComponent> ent, ref MapInitEvent args)
    {
        var selected = _auRoundSystem.GetSelectedPlanet();
        if (selected == null)
            return;

        var mapId = MetaData(ent).EntityPrototype?.ID;
        if (mapId == null || !mapId.Equals(selected.MapId, StringComparison.OrdinalIgnoreCase))
            return;

        EnsureComp<RMCPlanetComponent>(ent);
        // EnsureComp<TacticalMapComponent>(ent); // NOTE: is this one even used?
        _logs.Debug($"[OBJ MASTER] Added RMCPlanetComponent to planet map entity during AuObjectiveSystem's MapInit.");
    }

    private void OnPostGameMapLoad(PostGameMapLoad ev)
    {
        IsWinActive = false;
        var presetId = _gameTicker.Preset?.ID;
        if (string.IsNullOrWhiteSpace(presetId))
            return;

        var selectedPlanet = _auRoundSystem.GetSelectedPlanet();
        if (selectedPlanet == null ||
        !ev.GameMap.ID.Equals(selectedPlanet.MapId, StringComparison.OrdinalIgnoreCase))
        {
            _logs.Debug($"[OBJ MASTER] OnPostGameMapLoad: map '{ev.GameMap.ID}' is not the voted planet '{selectedPlanet?.MapId}', skipping.");
            return;
        }

        // first grid index could be a dropship/faulty mapped grid (need to grab largest)
        EntityUid? bestGrid = null;
        float bestArea = -1f;
        foreach (var grid in ev.Grids)
        {
            if (TryComp<MapGridComponent>(grid, out var gridComp))
            {
                var area = gridComp.LocalAABB.Width * gridComp.LocalAABB.Height;
                if (area > bestArea)
                {
                    bestArea = area;
                    bestGrid = grid;
                }
            }
        }

        if (bestGrid == null)
        {
            _logs.Warning($"[OBJ MASTER] Planet map has no valid grids!");
            return;
        }

        var planetGrid = bestGrid.Value;
        _logs.Debug($"[OBJ MASTER] Planet map '{selectedPlanet.MapId}' loaded as main map, using grid: '{planetGrid}'");

        if (!EntityQuery<ObjectiveMasterComponent>().Any())
        {
            var compFactory = EntityManager.ComponentFactory;
            foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.TryGetComponent<ObjectiveMasterComponent>(out var masterComp, compFactory)
                    && string.Equals(masterComp.Mode, presetId, StringComparison.OrdinalIgnoreCase))
                {
                    Spawn(proto.ID, new EntityCoordinates(planetGrid, Vector2.Zero));
                    _logs.Warning($"[OBJ MASTER] Auto-spawned MISSING ObjectiveMaster '{proto.ID}' for preset '{presetId}' on planet '{ev.GameMap.MapName}'.");
                    break;
                }
            }
        }

        if (TrySelectMasterComponent(presetId))
        {
            _logs.Debug($"[OBJ MASTER] ObjectiveMaster loaded from the planet, running AuObjectiveSystem.Main()...");
            Main();
        }
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
            _logs.Error("[OBJ COMPLETE] OnSpendWinPoints called with null ObjectiveMasterComponent!");
            return;
        }

        int newBalance;
        var key = ev.Team.ToLowerInvariant();

        switch (key)
        {
            case "govfor":
                master.CurrentWinPointsGovfor = Math.Max(0, master.CurrentWinPointsGovfor - ev.Amount);
                newBalance = master.CurrentWinPointsGovfor;
                break;
            case "opfor":
                master.CurrentWinPointsOpfor = Math.Max(0, master.CurrentWinPointsOpfor - ev.Amount);
                newBalance = master.CurrentWinPointsOpfor;
                break;
            case "clf":
                master.CurrentWinPointsClf = Math.Max(0, master.CurrentWinPointsClf - ev.Amount);
                newBalance = master.CurrentWinPointsClf;
                break;
            case "scientist":
                master.CurrentWinPointsScientist = Math.Max(0, master.CurrentWinPointsScientist - ev.Amount);
                newBalance = master.CurrentWinPointsScientist;
                break;
            default:
                _logs.Error($"[OBJ WIN] Couldn't coalesce faction: {key} to an existing team.");
                return;
        }

        // No need to call Dirty on the component reference directly; find the entity to mark dirty for replication
        DirtyObjectiveMaster();

        // Update all vendor caches so their BUIs reflect the new balance
        _vendorSystem.UpdateVendorFactionPointsCache(key, newBalance);
    }

    public (int govforMinor, int govforMajor, int opforMinor, int opforMajor, int clfMinor, int clfMajor,
        int scientistMinor, int scientistMajor) ObjectivesAmount()
    {
        if (GetOrReselectObjMaster() is not { } master)
        {
            _logs.Warning("[OBJ AMT] ObjectivesAmount called with null ObjectiveMasterComponent!");
            return (0, 0, 0, 0, 0, 0, 0, 0);
        }

        return (
            master.GovforMinorObjectives,
            master.GovforMajorObjectives,
            master.OpforMinorObjectives,
            master.OpforMajorObjectives,
            master.CLFMinorObjectives,
            master.CLFMajorObjectives,
            master.ScientistMinorObjectives,
            master.ScientistMajorObjectives
        );
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
        if (GetOrReselectObjMaster() == null)
        {
            _logs.Error("[OBJ COMPLETE] CompleteObjectiveForFaction called with null ObjectiveMasterComponent!");
            return;
        }

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
                EndRound(completingFaction, objective.RoundEndMessage);
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

    private void EndRound(string faction, string? roundEndMessage)
    {
        var message = roundEndMessage ?? string.Empty;
        var roundEndText = Loc.GetString("objectives-system-round-end",
            ("faction", faction.ToUpperInvariant()),
            ("message", message));

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
    private List<(EntityUid Uid, AuObjectiveComponent Comp)> GetInactiveObjectives(string? presetId = null)
    {
        var objectives = new List<(EntityUid Uid, AuObjectiveComponent Comp)>();
        var query = EntityQueryEnumerator<AuObjectiveComponent>();
        int count = 0;
        while (query.MoveNext(out var uid, out var comp))
        {
            count++;
            if (comp.Active) continue;

            _logs.Debug($"[OBJ GET] {count}: Found objective entity {uid} ({comp.objectiveDescription}), Active={comp.Active}.");
            if (presetId != null
                && !comp.ApplicableModes.Contains(presetId, StringComparer.OrdinalIgnoreCase))
                continue;

            objectives.Add((uid, comp));
        }

        _logs.Debug($"[OBJ GET]     Found {count} objectives, {objectives.Count} eligible.");
        return objectives;
    }

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
        ThreatPrototype? currentThreat = _auRoundSystem._selectedthreat;

        switch (factionLower)
        {
            case "govfor":
                selectedPlatoonId = _platoonSpawnRuleSystem.SelectedGovforPlatoon?.ID;
                break;
            case "opfor":
                selectedPlatoonId = _platoonSpawnRuleSystem.SelectedOpforPlatoon?.ID;
                break;
                // Add more cases if other factions can have platoons
        }

        foreach (var (objUid, objective) in allObjectives)
        {
            // Exclude win/final objectives (ObjectiveLevel == 3) from roundstart unless RollAnyway is true
            if (objective.ObjectiveLevel == 3 && !objective.RollAnyway) continue;

            bool factionMatch = objective.Factions.Any(f => f.ToLowerInvariant() == factionLower);
            bool maxPlayersMatch = objective.Maxplayers == 0 || objective.Maxplayers >= playercount;
            bool minPlayersMatch = objective.MinPlayers == 0 || playercount >= objective.MinPlayers;
            bool levelMatch = objectiveLevel == null
                ? (objective.ObjectiveLevel == 1 || objective.ObjectiveLevel == 2)
                : (objective.ObjectiveLevel == objectiveLevel);

            // Threat objective whitelist check
            bool threatWhitelistMatch = true;
            if (currentThreat != null && currentThreat.ObjectiveWhitelist.Count > 0)
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
        if (killObj.SpawnMob && !killObj.MobsSpawned)
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
            if (!objective.Repeating)
            {
                foreach (var key in objective.FactionStatuses.Keys.ToList())
                {
                    if (key != factionKey && objective.FactionStatuses[key] == AuObjectiveComponent.ObjectiveStatus.Incomplete)
                    {
                        objective.FactionStatuses[key] = AuObjectiveComponent.ObjectiveStatus.Failed;
                        _logs.Debug($"[OBJ COMPLETE] Set FactionStatuses['{key}'] = Failed");
                    }
                }
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
        {
            _logs.Error("[OBJ WIN] ApplyWinPoints called with null ObjectiveMasterComponent!");
            return;
        }

        var factionKey = faction.ToLowerInvariant();
        int newPoints;
        int requiredPoints;
        switch (factionKey)
        {
            case "govfor":
                master.CurrentWinPointsGovfor += points;
                newPoints = master.CurrentWinPointsGovfor;
                requiredPoints = master.RequiredWinPointsGovfor;
                break;
            case "opfor":
                master.CurrentWinPointsOpfor += points;
                newPoints = master.CurrentWinPointsOpfor;
                requiredPoints = master.RequiredWinPointsOpfor;
                break;
            case "clf":
                master.CurrentWinPointsClf += points;
                newPoints = master.CurrentWinPointsClf;
                requiredPoints = master.RequiredWinPointsClf;
                break;
            case "scientist":
                master.CurrentWinPointsScientist += points;
                newPoints = master.CurrentWinPointsScientist;
                requiredPoints = master.RequiredWinPointsScientist;
                break;
            default:
                _logs.Warning($"[OBJ WIN] ApplyWinPoints called with unknown faction: '{factionKey}'!");
                return;
        }

        // Sync the authoritative master to the actual entity for replication
        DirtyObjectiveMaster();

        // Push new balance to all objective-point vendors so their BUIs reflect it
        // regardless of whether the ObjectiveMasterComponent entity is in the client's PVS.
        _vendorSystem.UpdateVendorFactionPointsCache(factionKey, newPoints);

        if (!master.FinalObjectiveGivenFactions.Contains(factionKey) && newPoints >= requiredPoints)
            TryActivateFinalObjective(factionKey);
    }

    private void TryActivateFinalObjective(string factionKey)
    {
        // Only activate a final objective if it is completable
        var finalObjectives = new List<(EntityUid Uid, AuObjectiveComponent Comp)>();
        var finalObjQuery = AllEntityQuery<AuObjectiveComponent>();
        while (finalObjQuery.MoveNext(out var uid, out var comp))
        {
            if (!comp.Active
                && comp.ObjectiveLevel == 3
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
                if (pick < cumulative)
                {
                    chosen.Add((weighted[j].Uid, weighted[j].Comp));
                    weighted.RemoveAt(j);
                    break;
                }
            }
        }
        return chosen;
    }

    private int GetRandomObjectiveCount(int max, int? min)
    {
        if (min.HasValue)
        {
            if (min.Value > max)
            {
                _logs.Warning($"[OBJ RANDOM] MinObjectives ({min}) > MaxObjectives ({max}), using maximums.");
                return max;
            }
            if (min.Value < max)
                return Random.Shared.Next(min.Value, max + 1);
        }
        return max;
    }

    private void ActivateFactionObjectives(string faction, int level,
    List<(EntityUid Uid, AuObjectiveComponent Comp)> objectives)
    {
        var levelName = level == 1 ? "minor" : "major";
        _logs.Debug(
            $"[OBJ {faction.ToUpper()}] Activating {objectives.Count} {faction} {levelName} objectives...");

        foreach (var (objUid, obj) in objectives)
        {
            obj.Faction = faction;
            InitializeObjectiveStatuses(obj);
            obj.Active = true;
            Dirty(objUid, obj);
            RaiseLocalEvent(objUid, new ObjectiveActivatedEvent());
            _logs.Debug(
                $"[OBJ {faction.ToUpper()}] Activated {faction} {levelName}: {obj.objectiveDescription}");
        }
    }

    private void InitializeObjectiveStatuses(AuObjectiveComponent obj)
    {
        if (obj.FactionNeutral)
        {
            foreach (var faction in obj.Factions)
            {
                var key = faction.ToLowerInvariant();
                obj.FactionStatuses.TryAdd(key, AuObjectiveComponent.ObjectiveStatus.Incomplete);
            }
        }
        else if (!string.IsNullOrEmpty(obj.Faction))
        {
            var key = obj.Faction.ToLowerInvariant();
            obj.FactionStatuses.TryAdd(key, AuObjectiveComponent.ObjectiveStatus.Incomplete);
        }
    }

    private bool TrySelectMasterComponent(string? presetId)
    {
        (EntityUid Uid, ObjectiveMasterComponent Comp)? selected = null;
        var masterQuery = EntityQueryEnumerator<ObjectiveMasterComponent>();
        presetId = presetId?.ToLowerInvariant();

        while (masterQuery.MoveNext(out var uid, out var comp))
        {
            if (selected == null)
            {
                selected = (uid, comp);
                continue;
            }

            var selOnPlanet = _rmcPlanetSystem.IsOnPlanet(Transform(selected.Value.Uid));
            var curOnPlanet = _rmcPlanetSystem.IsOnPlanet(Transform(uid));
            if (curOnPlanet && !selOnPlanet)
            {
                selected = (uid, comp);
                continue;
            }

            if (curOnPlanet == selOnPlanet
                && comp.Mode.ToLowerInvariant() == presetId
                && selected.Value.Comp.Mode.ToLowerInvariant() != presetId)
                selected = (uid, comp);
        }

        if (selected == null)
        {
            _logs.Error("[OBJ MASTER] Cannot find an ObjectiveMasterComponent!");
            return false;
        }

        _objectiveMasterUid = selected.Value.Uid;
        return true;
    }

    private ObjectiveMasterComponent? GetOrReselectObjMaster()
    {
        if (_objectiveMasterUid.IsValid() && TryComp(_objectiveMasterUid, out ObjectiveMasterComponent? master))
            return master;

        var presetId = _gameTicker.Preset?.ID;
        if (TrySelectMasterComponent(presetId))
        {
            TryComp(_objectiveMasterUid, out master);
            return master;
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
        var presetId = _gameTicker.Preset?.ID?.ToLowerInvariant();
        if (!TrySelectMasterComponent(presetId))
        {
            _logs.Error("[OBJ MASTER] Main() ran with NO ObjMaster!");
            return;
        }

        if (GetOrReselectObjMaster() is not { } master) return;
        var govforMinor = new List<(EntityUid Uid, AuObjectiveComponent Comp)>();
        var govforMajor = new List<(EntityUid Uid, AuObjectiveComponent Comp)>();
        var opforMinor = new List<(EntityUid Uid, AuObjectiveComponent Comp)>();
        var opforMajor = new List<(EntityUid Uid, AuObjectiveComponent Comp)>();
        var clfMinor = new List<(EntityUid Uid, AuObjectiveComponent Comp)>();
        var clfMajor = new List<(EntityUid Uid, AuObjectiveComponent Comp)>();
        var scientistMinor = new List<(EntityUid Uid, AuObjectiveComponent Comp)>();
        var scientistMajor = new List<(EntityUid Uid, AuObjectiveComponent Comp)>();
        var modeObjectives = GetInactiveObjectives(presetId);

        if (presetId == "insurgency")
        {
            govforMinor = SelectObjectives("govfor", modeObjectives, 1, GetRandomObjectiveCount(master.GovforMinorObjectives, master.MinGovforMinorObjectives));
            govforMajor = SelectObjectives("govfor", modeObjectives, 2, GetRandomObjectiveCount(master.GovforMajorObjectives, master.MinGovforMajorObjectives));
            clfMinor = SelectObjectives("clf", modeObjectives, 1, GetRandomObjectiveCount(master.CLFMinorObjectives, master.MinCLFMinorObjectives));
            clfMajor = SelectObjectives("clf", modeObjectives, 2, GetRandomObjectiveCount(master.CLFMajorObjectives, master.MinCLFMajorObjectives));
        }
        else if (presetId == "forceonforce")
        {
            govforMinor = SelectObjectives("govfor", modeObjectives, 1, GetRandomObjectiveCount(master.GovforMinorObjectives, master.MinGovforMinorObjectives));
            govforMajor = SelectObjectives("govfor", modeObjectives, 2, GetRandomObjectiveCount(master.GovforMajorObjectives, master.MinGovforMajorObjectives));
            opforMinor = SelectObjectives("opfor", modeObjectives, 1, GetRandomObjectiveCount(master.OpforMinorObjectives, master.MinOpforMinorObjectives));
            opforMajor = SelectObjectives("opfor", modeObjectives, 2, GetRandomObjectiveCount(master.OpforMajorObjectives, master.MinOpforMajorObjectives));
        }
        else if (presetId == "distresssignal")
        {
            govforMinor = SelectObjectives("govfor", modeObjectives, 1, GetRandomObjectiveCount(master.GovforMinorObjectives, master.MinGovforMinorObjectives));
            govforMajor = SelectObjectives("govfor", modeObjectives, 2, GetRandomObjectiveCount(master.GovforMajorObjectives, master.MinGovforMajorObjectives));
        }

        // Scientist (Corporate) objectives are present in every game mode
        scientistMinor = SelectObjectives("scientist", modeObjectives, 1, GetRandomObjectiveCount(master.ScientistMinorObjectives, master.MinScientistMinorObjectives));
        scientistMajor = SelectObjectives("scientist", modeObjectives, 2, GetRandomObjectiveCount(master.ScientistMajorObjectives, master.MinScientistMajorObjectives));

        try { ActivateFactionObjectives("govfor", 1, govforMinor); }
        catch (Exception ex) { _logs.Error($"[OBJ FAIL] Failed to activate govfor minor objectives: {ex}"); }
        try { ActivateFactionObjectives("govfor", 2, govforMajor); }
        catch (Exception ex) { _logs.Error($"[OBJ FAIL] Failed to activate govfor major objectives: {ex}"); }

        try { ActivateFactionObjectives("opfor", 1, opforMinor); }
        catch (Exception ex) { _logs.Error($"[OBJ FAIL] Failed to activate opfor minor objectives: {ex}"); }
        try { ActivateFactionObjectives("opfor", 2, opforMajor); }
        catch (Exception ex) { _logs.Error($"[OBJ FAIL] Failed to activate opfor major objectives: {ex}"); }

        try { ActivateFactionObjectives("clf", 1, clfMinor); }
        catch (Exception ex) { _logs.Error($"[OBJ FAIL] Failed to activate clf minor objectives: {ex}"); }
        try { ActivateFactionObjectives("clf", 2, clfMajor); }
        catch (Exception ex) { _logs.Error($"[OBJ FAIL] Failed to activate clf major objectives: {ex}"); }

        try { ActivateFactionObjectives("scientist", 1, scientistMinor); }
        catch (Exception ex) { _logs.Error($"[OBJ FAIL] Failed to activate scientist minor objectives: {ex}"); }
        try { ActivateFactionObjectives("scientist", 2, scientistMajor); }
        catch (Exception ex) { _logs.Error($"[OBJ FAIL] Failed to activate scientist major objectives: {ex}"); }

        try
        {
            _logs.Debug("[OBJ NEUTRAL] Clearing statuses and initializing neutral objectives.");
            foreach (var (uid, obj) in modeObjectives.Where(x => !x.Comp.Active))
            {
                obj.FactionStatuses.Clear();
                InitializeObjectiveStatuses(obj);
                if (obj.FactionNeutral)
                    obj.Faction = string.Empty;
                Dirty(uid, obj);
            }

            // Gather all inactive neutral objectives that are applicable to this game mode
            var neutralCandidates = modeObjectives
                    .Where(obj => obj.Comp.FactionNeutral
                    && !obj.Comp.Active
                    && obj.Comp.Factions.Count > 0)
                .ToList();

            int neutralCap = GetRandomObjectiveCount(master.NeutralObjectives, master.MinNeutralObjectives);
            _logs.Info($"[OBJ NEUTRAL] Found {neutralCandidates.Count} neutral candidates, max allowed = {neutralCap}");

            // If we have more candidates than allowed, perform weighted random selection
            if (neutralCandidates.Count > neutralCap)
                neutralCandidates = WeightedRandomPick(neutralCandidates, neutralCap);

            // Activate the selected neutral objectives
            foreach (var (objUid, obj) in neutralCandidates)
            {
                obj.Active = true;
                Dirty(objUid, obj);
                RaiseLocalEvent(objUid, new ObjectiveActivatedEvent());
                _logs.Debug($"[OBJ NEUTRAL] Activated neutral objective '{obj.objectiveDescription}'");
            }
        }
        catch (Exception ex) { _logs.Error($"[OBJ NEUTRAL] Failed to activate neutral objectives: {ex.Message}"); }
    }
}

