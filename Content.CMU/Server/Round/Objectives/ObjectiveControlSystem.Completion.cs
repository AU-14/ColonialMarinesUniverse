using System.Linq;
using Content.Server._CMU14.RoundStatistics;
using Content.Shared._CMU14.Round.Objectives.Component;
using Content.Shared._CMU14.Round.Objectives.Type;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Vendors;
using Robust.Shared.Map;

namespace Content.Server._CMU14.Round.Objectives;

public sealed partial class ObjectiveControlSystem
{
    [Dependency] private SharedCMAutomatedVendorSystem _vendorSystem = default!;
    [Dependency] private CMURoundStatisticsSystem _roundStats = default!;
    [Dependency] private ObjectiveConsoleSystem _objConsole = default!;
    [Dependency] private IntelSystem _intel = default!;

    public void CompleteObjectiveForFaction(EntityUid uid, CMUObjectiveComponent objective, string completingFaction, bool awardPoints = true, ISawmill? sawmill = null)
    {
        if (_planetMapId == MapId.Nullspace || Transform(uid).MapID != _planetMapId)
            return;

        if (objective.StatusesPerFaction.ContainsValue(CMUObjectiveComponent.ObjectiveStatus.Completed))
            return;

        var factionKey = completingFaction.ToLowerInvariant();
        if (!MarkFactionCompleted(objective, factionKey))
            return;
        AwardAndRefresh(objective, completingFaction, awardPoints);
        (sawmill ?? _logs).Debug($"[OBJ-COMPLETE] '{objective.ObjectiveDescription}' completed for '{completingFaction}'.");

        if (objective.ObjectiveLevel == 3)
        {
            if (objective.FinalType == CMUObjectiveComponent.FinalObjectiveType.InstantWin)
                DeclareObjectiveVictory(completingFaction, objective.RoundEndMessage);
            else
                _logs.Info($"[OBJ-FINAL] Final objective '{objective.ObjectiveDescription}' completed for faction '{completingFaction}' as Boon (not ending the round).");
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
            _logs.Debug($"[OBJ-REPEAT] Objective '{objective.ObjectiveDescription}' reached max repeats ({maxRepeat}), marking as completed.");
            _objConsole.RefreshConsolesForFaction(completingFaction);
            return;
        }

        objective.TimesCompleted++;
        ResetObjectiveStatuses(objective);

        RaiseLocalEvent(uid, new ObjectiveResetEvent());

        objective.Active = true;
        Dirty(uid, objective);
        RaiseLocalEvent(uid, new ObjectiveActivatedEvent());
        _logs.Debug($"[OBJ-REPEAT] Restarted repeating objective '{objective.ObjectiveDescription}'...");

        if (objective.FactionNeutral)
            foreach (var faction in objective.Factions)
                _objConsole.RefreshConsolesForFaction(faction);
        else
            _objConsole.RefreshConsolesForFaction(objective.Faction);
    }

    private bool MarkFactionCompleted(CMUObjectiveComponent objective, string factionKey)
    {
        if (objective.FactionNeutral)
        {
            if (!objective.StatusesPerFaction.TryGetValue(factionKey, out var status)
                    || status != CMUObjectiveComponent.ObjectiveStatus.Incomplete)
                return false;

            objective.StatusesPerFaction[factionKey] = CMUObjectiveComponent.ObjectiveStatus.Completed;

            if (objective.Repeating)
                return true;

            foreach (var key in objective.StatusesPerFaction.Keys.ToList())
            {
                if (key == factionKey || objective.StatusesPerFaction[key] != CMUObjectiveComponent.ObjectiveStatus.Incomplete)
                    continue;
                objective.StatusesPerFaction[key] = CMUObjectiveComponent.ObjectiveStatus.Failed;
            }

            return true;
        }

        objective.StatusesPerFaction[factionKey] = CMUObjectiveComponent.ObjectiveStatus.Completed;
        return true;
    }

    private void MarkAllFactionsCompleted(CMUObjectiveComponent objective, string factionKey)
    {
        if (objective.FactionNeutral)
            foreach (var key in objective.StatusesPerFaction.Keys.ToList())
                objective.StatusesPerFaction[key] = CMUObjectiveComponent.ObjectiveStatus.Completed;
        else
            objective.StatusesPerFaction[factionKey] = CMUObjectiveComponent.ObjectiveStatus.Completed;
    }

    private void AwardAndRefresh(CMUObjectiveComponent objective, string completingFaction, bool awardPoints = true)
    {
        if (awardPoints)
            AwardPointsToFaction(completingFaction, objective);
        if (objective.FactionNeutral)
            foreach (var f in objective.Factions)
                _objConsole.RefreshConsolesForFaction(f);
        else
            _objConsole.RefreshConsolesForFaction(completingFaction);
    }

    public void MarkObjectiveFailed(EntityUid uid, CMUObjectiveComponent objective)
    {
        if (objective.FactionNeutral)
        {
            foreach (var faction in objective.Factions)
            {
                if (string.IsNullOrEmpty(faction))
                    continue;
                objective.StatusesPerFaction[faction.ToLowerInvariant()] = CMUObjectiveComponent.ObjectiveStatus.Failed;
                _objConsole.RefreshConsolesForFaction(faction);
            }
        }
        else if (!string.IsNullOrEmpty(objective.Faction))
        {
            objective.StatusesPerFaction[objective.Faction.ToLowerInvariant()] = CMUObjectiveComponent.ObjectiveStatus.Failed;
            _objConsole.RefreshConsolesForFaction(objective.Faction);
        }
        Dirty(uid, objective);
    }

    public void MarkObjectiveFailedForFaction(EntityUid uid, CMUObjectiveComponent objective, string faction)
    {
        if (string.IsNullOrEmpty(faction))
            return;

        objective.StatusesPerFaction[faction.ToLowerInvariant()] = CMUObjectiveComponent.ObjectiveStatus.Failed;
        Dirty(uid, objective);
        _objConsole.RefreshConsolesForFaction(faction);
    }

    public void AwardPointsToFaction(string faction, CMUObjectiveComponent objective)
        => ApplyWinPoints(faction, objective.CustomPoints == 0
            ? (objective.ObjectiveLevel == 1 ? 5 : 20)
            : objective.CustomPoints);

    public void AwardRawPointsToFaction(string faction, int points) => ApplyWinPoints(faction, points);

    private void ApplyWinPoints(string faction, int points)
    {
        if (GetOrReselectObjMaster() is not { } master)
            return;

        var key = faction.ToLowerInvariant();
        var data = master.GetOrCreateFactionData(key);
        data.CurrentWinPoints += points;
        DirtyObjectiveMaster();
        _vendorSystem.UpdateVendorFactionPointsCache(key, data.CurrentWinPoints);
        _intel.UpdateTree(_intel.EnsureTechTree(key));

        if (!master.FactionsGivenFinalObjective.Contains(key) && data.CurrentWinPoints >= data.RequiredWinPoints)
            TryActivateFinalObjective(key);
    }

    private void TryActivateFinalObjective(string factionKey)
    {
        var finalObjectives = new List<(EntityUid Uid, CMUObjectiveComponent Comp)>();
        foreach (var (uid, comp) in _allObjectives)
        {
            if (_planetMapId == MapId.Nullspace || Transform(uid).MapID != _planetMapId)
                continue;

            if (comp is { Active: false, ObjectiveLevel: 3 }
                && comp.Factions.Any(f => f.ToLowerInvariant() == factionKey))
            {
                finalObjectives.Add((uid, comp));
            }
        }

        foreach (var (uid, comp) in finalObjectives.OrderBy(_ => Random.Shared.Next()))
        {
            if (TryComp(uid, out KillObjectiveComponent? _) && !IsKillObjectiveCompletable(uid))
                continue;

            ActivateObjective(uid, comp, factionKey);

            if (GetOrReselectObjMaster() is not { } master)
                return;
            master.FactionsGivenFinalObjective.Add(factionKey);
            DirtyObjectiveMaster();

            IsWinActive = true;
            _logs.Info($"[OBJ-FINAL] Activated '{comp.ObjectiveDescription}' for '{factionKey}', IsWinActive=true");
            return;
        }

        _logs.Warning($"[OBJ-FINAL] No completable final objective found for faction '{factionKey}'. None activated!");
    }

    private void TryUnlockOrSpawnNextTier(EntityUid completedUid, CMUObjectiveComponent completedObjective, string completingFaction)
    {
        var nextTier = completedObjective.NextTierObjective;
        if (!nextTier.HasValue)
            return;

        var protoIdStr = nextTier.Value.Id;
        if (string.IsNullOrEmpty(protoIdStr))
            return;

        if (!TryComp(completedUid, out TransformComponent? completedXform))
            return;

        if (!nextTier.Value.TryGet(out CMUObjectiveComponent? _, _proto, EntityManager.ComponentFactory))
        {
            _logs.Warning($"[OBJ-TIER] Next tier prototype '{protoIdStr}' does not contain a CMUObjectiveComponent or is missing!");
            return;
        }

        var newEnt = Spawn(protoIdStr, completedXform.Coordinates);
        if (TryComp(newEnt, out CMUObjectiveComponent? newObjComp))
        {
            newObjComp.StatusesPerFaction.Clear();
            ActivateObjective(newEnt, newObjComp, completingFaction.ToLowerInvariant());
        }
        else
            _logs.Warning($"[OBJ-TIER] Spawned prototype {protoIdStr} but it does not contain a CMUObjectiveComponent!");
    }

    public void DeclareObjectiveVictory(string faction, string? roundEndMessage)
    {
        var message = roundEndMessage ?? string.Empty;
        var roundEndText = Loc.GetString("objectives-system-round-end",
            ("faction", faction.ToUpperInvariant()),
            ("message", message));

        _roundStats.RecordObjectiveVictory(faction);
        _gameTicker.EndRound(roundEndText);
    }

    public void InitializeObjectiveStatuses(CMUObjectiveComponent obj)
    {
        if (obj.FactionNeutral)
            foreach (var faction in obj.Factions)
                obj.StatusesPerFaction.TryAdd(faction.ToLowerInvariant(), CMUObjectiveComponent.ObjectiveStatus.Incomplete);
        else if (!string.IsNullOrEmpty(obj.Faction))
            obj.StatusesPerFaction.TryAdd(obj.Faction.ToLowerInvariant(), CMUObjectiveComponent.ObjectiveStatus.Incomplete);
    }

    private void ResetObjectiveStatuses(CMUObjectiveComponent objective)
    {
        foreach (var key in objective.StatusesPerFaction.Keys.ToList())
            objective.StatusesPerFaction[key] = CMUObjectiveComponent.ObjectiveStatus.Incomplete;
    }
}
