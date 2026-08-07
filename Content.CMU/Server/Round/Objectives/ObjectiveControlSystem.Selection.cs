using System.Linq;
using Content.Shared._CMU14.Round.Objectives.Components;
using Content.Shared._CMU14.Round.Objectives.Type;
using Content.Shared._CMU14.Threats;
using Robust.Shared.Map;

namespace Content.Server._CMU14.Round.Objectives;

public sealed partial class ObjectiveControlSystem
{
    private List<(EntityUid Uid, CMUObjectiveComponent Comp)> GetInactiveObjectives(
        string? presetId = null, MapId? mapId = null)
    {
        var objectives = new List<(EntityUid Uid, CMUObjectiveComponent Comp)>();
        int count = 0;
        foreach (var (uid, comp) in _allObjectives)
        {
            if (!Exists(uid))
                continue;

            count++;

            if (comp.Active)
                continue;

            _logs.Debug($"[OBJ-SEL] GetInactiveObjectives: Found entity {uid} ({comp.ObjectiveDescription}), Active={comp.Active}");

            if (mapId != null && Transform(uid).MapID != mapId)
                continue;

            bool modeMatch = true;
            if (presetId != null)
            {
                if (comp.FactionNeutral)
                    modeMatch = comp.AllowedPresets.Count == 0
                        || comp.AllowedPresets.Any(m => m.Equals(presetId, StringComparison.OrdinalIgnoreCase));
                else
                    modeMatch = comp.AllowedPresets.Any(m => m.Equals(presetId, StringComparison.OrdinalIgnoreCase));
            }

            if (!modeMatch)
                continue;

            objectives.Add((uid, comp));
        }

        _logs.Debug($"[OBJ-SEL] GetInactiveObjectives: Found {count} objectives, {objectives.Count} eligible.");
        return objectives;
    }

    private List<(EntityUid Uid, CMUObjectiveComponent Comp)> SelectObjectives(
        string faction,
        List<(EntityUid Uid, CMUObjectiveComponent Comp)> allObjectives,
        int? objectiveLevel = null,
        int maxCount = int.MaxValue)
    {
        var playerCount = _playerManager.PlayerCount;
        var factionLower = faction.ToLowerInvariant();
        var selected = new List<(EntityUid Uid, CMUObjectiveComponent Comp)>();
        string? selectedPlatoonId = null;
        ThreatPrototype? currentThreat = _auRoundSystem.SelectedThreat;

        switch (factionLower)
        {
            case "govfor":
                selectedPlatoonId = _platoonSystem.SelectedGovforPlatoon?.ID;
                break;
            case "opfor":
                selectedPlatoonId = _platoonSystem.SelectedOpforPlatoon?.ID;
                break;
        }

        foreach (var (objUid, objective) in allObjectives)
        {
            if (objective.FactionNeutral)
                continue;

            if (objective is { ObjectiveLevel: 3, RollAnyway: false })
                continue;

            bool factionMatch = objective.Factions.Any(f => f.ToLowerInvariant() == factionLower);
            bool maxPlayersMatch = objective.MaxPlayers == 0 || objective.MaxPlayers >= playerCount;
            bool minPlayersMatch = objective.MinPlayers == 0 || playerCount >= objective.MinPlayers;
            bool levelMatch = objectiveLevel == null
                ? (objective.ObjectiveLevel == 1 || objective.ObjectiveLevel == 2)
                : (objective.ObjectiveLevel == objectiveLevel);

            bool threatWhitelistMatch = true;
            if (currentThreat is { ObjectiveWhitelist.Count: > 0 })
            {
                if (!currentThreat.ObjectiveWhitelist.Contains(objective.Id))
                    threatWhitelistMatch = false;
            }

            if (!factionMatch) continue;
            if (!maxPlayersMatch) continue;
            if (!minPlayersMatch) continue;
            if (!levelMatch) continue;
            if (!threatWhitelistMatch) continue;
            if (selectedPlatoonId != null && objective.BlacklistedPlatoons.Contains(selectedPlatoonId))
                continue;
            if (objective.WhitelistedPlatoons.Count > 0
                && (selectedPlatoonId == null || !objective.WhitelistedPlatoons.Contains(selectedPlatoonId)))
                continue;

            selected.Add((objUid, objective));
        }

        if (selected.Count > maxCount)
            selected = WeightedRandomPick(selected, maxCount);

        return selected;
    }

    private static List<(EntityUid Uid, CMUObjectiveComponent Comp)> WeightedRandomPick(
        List<(EntityUid Uid, CMUObjectiveComponent Comp)> candidates, int count)
    {
        if (count <= 0 || candidates.Count == 0)
            return new List<(EntityUid Uid, CMUObjectiveComponent Comp)>();

        var weighted = candidates
            .Select(obj => (obj.Uid, obj.Comp, Weight: Math.Max(1, obj.Comp.ObjectiveWeight)))
            .ToList();

        var chosen = new List<(EntityUid Uid, CMUObjectiveComponent Comp)>();
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

    private static int GetRandomObjectiveCount(int max, int? min)
    {
        if (min is not { } minValue)
            return max;

        if (minValue < max)
            return Random.Shared.Next(minValue, max + 1);

        if (minValue > max)
        {
            Logger.GetSawmill("objectives")
                .Warning($"[OBJ-SEL] GetRandomObjectiveCount: Min ({minValue}) > Max ({max}), using max.");
        }

        return max;
    }

    private void ActivateFactionObjectives(
        string faction,
        int level,
        List<(EntityUid Uid, CMUObjectiveComponent Comp)> objectives)
    {
        var levelName = level == 1 ? "minor" : "major";
        _logs.Debug($"[OBJ-SEL] Activating {objectives.Count} {faction} {levelName} objectives");

        foreach (var (objUid, obj) in objectives)
        {
            ActivateObjective(objUid, obj, faction);
            _logs.Debug($"[OBJ-SEL] Activated {faction} {levelName}: {obj.ObjectiveDescription}");
        }
    }

    private void ActivateObjective(EntityUid uid, CMUObjectiveComponent comp, string? faction = null, bool lateActivation = false)
    {
        if (!comp.FactionNeutral && !string.IsNullOrEmpty(faction))
            comp.Faction = faction;

        InitializeObjectiveStatuses(comp);
        comp.Active = true;
        Dirty(uid, comp);
        RaiseLocalEvent(uid, new ObjectiveActivatedEvent(lateActivation));

        if (comp.FactionNeutral)
        {
            foreach (var f in comp.Factions)
                _objConsole.RefreshConsolesForFaction(f);
        }
        else if (!string.IsNullOrEmpty(comp.Faction))
        {
            _objConsole.RefreshConsolesForFaction(comp.Faction);
        }
    }

    private void TryLateActivateObjective(EntityUid uid)
    {
        if (!Exists(uid) || TerminatingOrDeleted(uid))
            return;

        if (!TryComp(uid, out CMUObjectiveComponent? comp) || comp.Active)
            return;

        if (_planetMapId == MapId.Nullspace || Transform(uid).MapID != _planetMapId)
        {
            _logs.Debug($"[OBJ-LATE] Not activating '{comp.ObjectiveDescription}': entity's map" +
                        $" ({Transform(uid).MapID}) isn't the voted planet ({_planetMapId})!");
            return;
        }

        var presetId = _gameTicker.Preset?.ID;
        if (string.IsNullOrWhiteSpace(presetId))
            return;

        var modeMatch = comp.FactionNeutral
            ? comp.AllowedPresets.Count == 0 || comp.AllowedPresets.Any(m => m.Equals(presetId, StringComparison.OrdinalIgnoreCase))
            : comp.AllowedPresets.Any(m => m.Equals(presetId, StringComparison.OrdinalIgnoreCase));
        if (!modeMatch)
        {
            _logs.Debug($"[OBJ-LATE] Not activating '{comp.ObjectiveDescription}': allowedPresets" +
                        $" [{string.Join(", ", comp.AllowedPresets)}] doesn't include the current preset '{presetId}'!");
            return;
        }

        if (!comp.FactionNeutral && comp.Factions.Count == 0)
        {
            _logs.Debug($"[OBJ-LATE] Not activating '{comp.ObjectiveDescription}': not faction neutral and has no factions assigned!");
            return;
        }

        foreach (var (otherUid, other) in _allObjectives)
        {
            if (otherUid == uid || other.Id != comp.Id)
                continue;

            var stillInProgress = other.Active
                && other.StatusesPerFaction.Values.Any(s => s == CMUObjectiveComponent.ObjectiveStatus.Incomplete);
            if (!stillInProgress)
                continue;

            if (Exists(otherUid) && Transform(otherUid).MapID == _planetMapId)
            {
                _logs.Info($"[OBJ-LATE] Skipping late activation of '{comp.ObjectiveDescription}':" +
                           $" an in-progress copy ('{comp.Id}') already exists on this map.");
                return;
            }
        }

        var faction = comp.FactionNeutral ? null : comp.Factions[0];
        ActivateObjective(uid, comp, faction, lateActivation: true);
        _logs.Info($"[OBJ-LATE] Auto activated a late spawned objective '{comp.ObjectiveDescription}'" +
                   (faction != null ? $" for faction '{faction}'." : "."));
    }

    public string GetOppositeFaction(string faction, string? mode) => (mode?.ToLowerInvariant(), faction.ToLowerInvariant()) switch
    {
        ("forceonforce", "govfor") => "opfor",
        ("forceonforce", "opfor") => "govfor",
        ("distresssignal", "clf") => "govfor",
        ("distresssignal", "govfor") => "clf",
        ("insurgency", "clf") => "govfor",
        ("insurgency", "govfor") => "clf",
        _ => string.Empty,
    };

    private bool IsKillObjectiveCompletable(EntityUid uid)
    {
        if (!TryComp(uid, out KillObjectiveComponent? killObj))
            return false;

        if (killObj is { SpawnMob: true, HasSpawned: false })
            return true;

        var query = EntityQueryEnumerator<KillMarkedForComponent>();
        while (query.MoveNext(out var _, out var markComp))
        {
            if (markComp.AssociatedObjectives.ContainsKey(uid))
                return true;
        }

        return false;
    }
}
