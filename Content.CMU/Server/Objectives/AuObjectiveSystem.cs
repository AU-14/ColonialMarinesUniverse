using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.Rules;
using Robust.Shared.GameStates;
using Robust.Shared.GameObjects;
using Content.Shared.AU14.Objectives;
using Robust.Server.Player;
using Content.Server.GameTicking.Events;
using Content.Shared.AU14.Objectives.Fetch;
using Content.Shared.Clothing.Components;

namespace Content.Server.AU14.Objectives;

public sealed class AuObjectiveSystem : AuSharedObjectiveSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;


    [Dependency] private readonly IEntityManager _entityManager = default!;

    [Dependency] private readonly ObjectivesConsoleSystem _objectivesConsoleSystem = default!;

    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly RoundEnd.RoundEndSystem _roundEnd = default!;

    private readonly HashSet<string> _finalObjectiveGivenFactions = new();

    public (int govforMinor, int govforMajor, int opforMinor, int opforMajor, int clfMinor, int clfMajor, int
        scientistMinor, int scientistMajor) ObjectivesAmount()
    {
        foreach (var comp in EntityManager.EntityQuery<ObjectiveMasterComponent>())
        {
            return (
                comp.GovforMinorObjectives,
                comp.GovforMajorObjectives,
                comp.OpforMinorObjectives,
                comp.OpforMajorObjectives,
                comp.CLFMinorObjectives,
                comp.CLFMajorObjectives,
                comp.ScientistMinorObjectives,
                comp.ScientistMajorObjectives
            );
        }

        var def = new ObjectiveMasterComponent();
        return (
            def.GovforMinorObjectives,
            def.GovforMajorObjectives,
            def.OpforMinorObjectives,
            def.OpforMajorObjectives,
            def.CLFMinorObjectives,
            def.CLFMajorObjectives,
            def.ScientistMinorObjectives,
            def.ScientistMajorObjectives
        );
    }

    private ObjectiveMasterComponent? _objectiveMaster = null;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AuObjectiveComponent, ComponentHandleState>(OnObjectiveHandleState);
        SubscribeLocalEvent<AuObjectiveComponent, ComponentStartup>(OnObjectiveStartup);
        SubscribeLocalEvent<ObjectiveMasterComponent, ComponentStartup>(OnObjectiveMasterStartup);
    }

    private void OnObjectiveMasterStartup(EntityUid uid, ObjectiveMasterComponent component, ref ComponentStartup args)
    {
        Logger.Info($"[OBJ SYSTEM DEBUG] ObjectiveMasterComponent startup on entity {uid}, calling Main()");
        Main();
    }

    private void OnObjectiveStartup(EntityUid uid, AuObjectiveComponent component, ref ComponentStartup args)
    {
        Logger.Info(
            $"[OBJ STARTUP DEBUG] AuObjectiveComponent started on entity {uid} ({component.objectiveDescription})");
        InitializeObjectiveStatuses(component);
    }

    private void OnObjectiveHandleState(EntityUid uid, AuObjectiveComponent component, ref ComponentHandleState args)
    {
        // If the objective is not completed for any faction, do nothing
        if (component.FactionNeutral)
        {
            // If any faction has completed, mark as completed for that faction and failed for others
            foreach (var (faction, status) in component.FactionStatuses)
            {
                if (status == AuObjectiveComponent.ObjectiveStatus.Completed)
                {
                    CompleteObjectiveForFaction(uid, component, faction);
                    break;
                }
            }
        }
        else
        {
            var factionKey = component.Faction.ToLowerInvariant();
            if (!component.FactionStatuses.ContainsKey(factionKey) || component.FactionStatuses[factionKey] !=
                AuObjectiveComponent.ObjectiveStatus.Completed)
            {
                // Use the assigned faction for non-neutral objectives
                CompleteObjectiveForFaction(uid, component, component.Faction);
            }
        }
    }

    private List<AuObjectiveComponent> GetObjectives()
    {
        var objectives = new List<AuObjectiveComponent>();
        var query = EntityQueryEnumerator<AuObjectiveComponent>();
        int count = 0;
        while (query.MoveNext(out var uid, out var comp))
        {
            Logger.Info(
                $"[OBJ GET DEBUG] Found objective entity {uid} ({comp.objectiveDescription}), Active={comp.Active}");
            if (!comp.Active)
                objectives.Add(comp);
            count++;
        }

        Logger.Info($"[OBJ GET DEBUG] Total objectives found: {count}, eligible (inactive): {objectives.Count}");
        return objectives;
    }



    public void Main()
    {
        var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
        var presetId = ticker.Preset?.ID?.ToLowerInvariant();
        var govforMinor = new List<AuObjectiveComponent>();
        var govforMajor = new List<AuObjectiveComponent>();
        var opforMinor = new List<AuObjectiveComponent>();
        var opforMajor = new List<AuObjectiveComponent>();
        var clfMinor = new List<AuObjectiveComponent>();
        var clfMajor = new List<AuObjectiveComponent>();
        var scientistMinor = new List<AuObjectiveComponent>();
        var scientistMajor = new List<AuObjectiveComponent>();

        var allMasters = new List<ObjectiveMasterComponent>();
        foreach (var comp in EntityManager.EntityQuery<ObjectiveMasterComponent>())
        {
            allMasters.Add(comp);
        }

        if (allMasters.Count == 0)
        {
            _objectiveMaster = new ObjectiveMasterComponent();
        }
        else if (allMasters.Count == 1)
        {
            _objectiveMaster = allMasters[0];
        }
        else
        {
            _objectiveMaster = allMasters.FirstOrDefault(m => m.Mode.ToLowerInvariant() == presetId) ??
                               allMasters[0];
        }

        if (presetId == "insurgency")
        {
            govforMinor = SelectObjectives("govfor", 1, _objectiveMaster);
            govforMajor = SelectObjectives("govfor", 2, _objectiveMaster);
            clfMinor = SelectObjectives("clf", 1, _objectiveMaster);
            clfMajor = SelectObjectives("clf", 2, _objectiveMaster);
        }
        else if (presetId == "forceonforce")
        {
            govforMinor = SelectObjectives("govfor", 1, _objectiveMaster);
            govforMajor = SelectObjectives("govfor", 2, _objectiveMaster);
            opforMinor = SelectObjectives("opfor", 1, _objectiveMaster);
            opforMajor = SelectObjectives("opfor", 2, _objectiveMaster);
        }
        else if (presetId == "distresssignal")
        {
            govforMinor = SelectObjectives("govfor", 1, _objectiveMaster);
            govforMajor = SelectObjectives("govfor", 2, _objectiveMaster);
        }

        scientistMinor = SelectObjectives("scientist", 1, _objectiveMaster);
        scientistMajor = SelectObjectives("scientist", 2, _objectiveMaster);

        foreach (var obj in govforMinor)
        {
            obj.Active = true;
            obj.Faction = "govfor";
            Logger.Info($"[OBJ DEBUG] Set govforMinor objective '{obj.objectiveDescription}' active");
        }

        foreach (var obj in govforMajor)
        {
            obj.Active = true;
            obj.Faction = "govfor";
            Logger.Info($"[OBJ DEBUG] Set govforMajor objective '{obj.objectiveDescription}' active");
        }

        foreach (var obj in opforMinor)
        {
            obj.Active = true;
            obj.Faction = "opfor";
            Logger.Info($"[OBJ DEBUG] Set opforMinor objective '{obj.objectiveDescription}' active");
        }

        foreach (var obj in opforMajor)
        {
            obj.Active = true;
            obj.Faction = "opfor";
            Logger.Info($"[OBJ DEBUG] Set opforMajor objective '{obj.objectiveDescription}' active");
        }

        foreach (var obj in clfMinor)
        {
            obj.Active = true;
            obj.Faction = "clf";
            Logger.Info($"[OBJ DEBUG] Set clfMinor objective '{obj.objectiveDescription}' active");
        }

        foreach (var obj in clfMajor)
        {
            obj.Active = true;
            obj.Faction = "clf";
            Logger.Info($"[OBJ DEBUG] Set clfMajor objective '{obj.objectiveDescription}' active");
        }

        foreach (var obj in scientistMinor)
        {
            obj.Active = true;
            obj.Faction = "scientist";
            Logger.Info($"[OBJ DEBUG] Set scientistMinor objective '{obj.objectiveDescription}' active");
        }

        foreach (var obj in scientistMajor)
        {
            obj.Active = true;
            obj.Faction = "scientist";
            Logger.Info($"[OBJ DEBUG] Set scientistMajor objective '{obj.objectiveDescription}' active");
        }


        foreach (var obj in GetObjectives())
        {
            obj.FactionStatuses.Clear();
            InitializeObjectiveStatuses(obj);
            if (obj.FactionNeutral)
            {
                obj.Faction = string.Empty; // Not assigned to a single faction
            }
        }

        var allObjectives = GetObjectives();
        foreach (var obj in allObjectives)
        {
            if (obj.FactionNeutral && !obj.Active)
            {
                if (obj.ApplicableModes.Contains(presetId ?? string.Empty))
                {
                    if (obj.Factions.Count > 0)
                    {
                        obj.Active = true;
                        Logger.Info($"[OBJ DEBUG] Set neutral objective '{obj.objectiveDescription}' active");
                    }
                }
            }
        }
    }

    private List<AuObjectiveComponent> SelectObjectives(string faction,
        int? objectiveLevel = null,
        ObjectiveMasterComponent? objectiveMaster = null)
    {

        var playercount = _playerManager.PlayerCount;
        var ticker = _entityManager.EntitySysManager.GetEntitySystem<GameTicker>();
        var presetId = ticker.Preset?.ID ?? string.Empty;
        var presetIdLower = presetId.ToLowerInvariant();
        var factionLower = faction.ToLowerInvariant();
        var allObjectives = GetObjectives();
        var selected = new List<AuObjectiveComponent>();
        foreach (var objective in allObjectives)
        {
            bool modeMatch = objective.ApplicableModes.Any(m => m.ToLowerInvariant() == presetIdLower);
            bool factionMatch = objective.Factions.Any(f => f.ToLowerInvariant() == factionLower);
            bool maxPlayersMatch = (objective.Maxplayers == 0 || objective.Maxplayers >= playercount);
            bool levelMatch = (objectiveLevel == null
                ? (objective.ObjectiveLevel == 1 || objective.ObjectiveLevel == 2)
                : (objective.ObjectiveLevel == objectiveLevel));
            if (!modeMatch)
            {
                Logger.Info(
                    $"[OBJ SELECT DEBUG] Skipping '{objective.objectiveDescription}' - mode mismatch: presetId={presetId}, modes=[{string.Join(",", objective.ApplicableModes)}]");
                continue;
            }

            if (!factionMatch)
            {
                Logger.Info(
                    $"[OBJ SELECT DEBUG] Skipping '{objective.objectiveDescription}' - faction mismatch: faction={faction}, possible=[{string.Join(",", objective.Factions)}]");
                continue;
            }

            if (!maxPlayersMatch)
            {
                Logger.Info(
                    $"[OBJ SELECT DEBUG] Skipping '{objective.objectiveDescription}' - player count too high: {playercount} > {objective.Maxplayers}");
                continue;
            }

            if (!levelMatch)
            {
                Logger.Info(
                    $"[OBJ SELECT DEBUG] Skipping '{objective.objectiveDescription}' - level mismatch: {objective.ObjectiveLevel} != {objectiveLevel}");
                continue;
            }

            Logger.Info($"[OBJ SELECT DEBUG] Picking '{objective.objectiveDescription}'");
            selected.Add(objective);
        }

        return selected;
    }







    public void CompleteObjectiveForFaction(EntityUid uid, AuObjectiveComponent objective, string completingFaction)
    {
        if (_objectiveMaster == null)
            return;

        if (objective.FactionStatuses.ContainsValue(AuObjectiveComponent.ObjectiveStatus.Completed))
        {

            return;
        }


        var factionKey = completingFaction.ToLowerInvariant();

        if (objective.FactionNeutral)
        {
            if (!objective.FactionStatuses.ContainsKey(factionKey) ||
                objective.FactionStatuses[factionKey] != AuObjectiveComponent.ObjectiveStatus.Incomplete)
                return;

            objective.FactionStatuses[factionKey] = AuObjectiveComponent.ObjectiveStatus.Completed;
            Logger.Info($"[OBJ COMPLETE DEBUG] Set FactionStatuses['{factionKey}'] = Completed");

            // Only mark other factions as Failed if NOT repeating
            if (!objective.Repeating)
            {
                foreach (var key in objective.FactionStatuses.Keys.ToList())
                {
                    if (key != factionKey &&
                        objective.FactionStatuses[key] == AuObjectiveComponent.ObjectiveStatus.Incomplete)
                    {
                        objective.FactionStatuses[key] = AuObjectiveComponent.ObjectiveStatus.Failed;
                        Logger.Info($"[OBJ COMPLETE DEBUG] Set FactionStatuses['{key}'] = Failed");
                    }
                }
            }

            AwardPointsToFaction(completingFaction, objective);
            foreach (var faction in objective.Factions)
            {
                _objectivesConsoleSystem.RefreshConsolesForFaction(faction);
            }
        }
        else
        {
            if (!objective.FactionStatuses.TryAdd(factionKey, AuObjectiveComponent.ObjectiveStatus.Completed))
                objective.FactionStatuses[factionKey] = AuObjectiveComponent.ObjectiveStatus.Completed;
            Logger.Info($"[OBJ COMPLETE DEBUG] Set FactionStatuses['{factionKey}'] = Completed");
            AwardPointsToFaction(completingFaction, objective);
            _objectivesConsoleSystem.RefreshConsolesForFaction(completingFaction);
        }

        if (objective.ObjectiveLevel == 3)
        {
            EndRound(completingFaction, objective.RoundEndMessage);
        }

        if (objective.Repeating)
        {
            if (objective.MaxRepeatable is { } maxRepeat && objective.TimesCompleted + 1 >= maxRepeat)
            {
                objective.TimesCompleted = maxRepeat;
                objective.Active = false;
                if (objective.FactionNeutral)
                {
                    foreach (var key in objective.FactionStatuses.Keys.ToList())
                    {
                        objective.FactionStatuses[key] = AuObjectiveComponent.ObjectiveStatus.Completed;
                    }
                }
                else
                {
                    objective.FactionStatuses[factionKey] = AuObjectiveComponent.ObjectiveStatus.Completed;
                }
                Logger.Info($"[OBJ REPEAT DEBUG] Objective '{objective.objectiveDescription}' reached max repeats ({maxRepeat}), marking as completed.");
                _objectivesConsoleSystem.RefreshConsolesForFaction(completingFaction);
                return;
            }
            objective.TimesCompleted++;
            foreach (var key in objective.FactionStatuses.Keys.ToList())
            {
                objective.FactionStatuses[key] = AuObjectiveComponent.ObjectiveStatus.Incomplete;
            }

            // Move fetch-specific logic to AuFetchObjectiveSystem
            if (_entityManager.TryGetComponent(uid, out FetchObjectiveComponent? fetchComp))
            {
                var fetchSystem = _entityManager.EntitySysManager
                    .GetEntitySystem<Content.Server.AU14.Objectives.Fetch.AuFetchObjectiveSystem>();
                fetchSystem.ResetAndRespawnFetchObjective(uid, fetchComp);
            }

            // Reactivate the objective
            objective.Active = true;
            Logger.Info($"[OBJ REPEAT DEBUG] Restarted repeating objective '{objective.objectiveDescription}'");
            // Refresh consoles for all relevant factions
            if (objective.FactionNeutral)
            {
                foreach (var faction in objective.Factions)
                    _objectivesConsoleSystem.RefreshConsolesForFaction(faction);
            }
            else
            {
                _objectivesConsoleSystem.RefreshConsolesForFaction(objective.Faction);
            }
        }

        Logger.Info(
            $"[OBJ REPEAT DEBUG] Objective '{objective.objectiveDescription}' Repeating property: {objective.Repeating}");
        if (objective.Repeating)
        {
            // Reset status for all factions
            foreach (var key in objective.FactionStatuses.Keys.ToList())
            {
                objective.FactionStatuses[key] = AuObjectiveComponent.ObjectiveStatus.Incomplete;
            }

            if (_entityManager.TryGetComponent(uid, out FetchObjectiveComponent? fetchComp))
            {
                var fetchSystem = _entityManager.EntitySysManager
                    .GetEntitySystem<Content.Server.AU14.Objectives.Fetch.AuFetchObjectiveSystem>();
                fetchSystem.ResetAndRespawnFetchObjective(uid, fetchComp);
            }

            // Reactivate the objective
            objective.Active = true;
            Logger.Info($"[OBJ REPEAT DEBUG] Restarted repeating objective '{objective.objectiveDescription}'");
            // Refresh consoles for all relevant factions
            if (objective.FactionNeutral)
            {
                foreach (var faction in objective.Factions)
                {
                    _objectivesConsoleSystem.RefreshConsolesForFaction(faction);
                }
            }
            else
            {
                _objectivesConsoleSystem.RefreshConsolesForFaction(objective.Faction);
            }
        }
    }

    private void EndRound(string faction, string? roundendmessage)
    {
        // Compose a round end message if not provided
        var message = roundendmessage;
        if (string.IsNullOrEmpty(message))
            message = $"{faction.ToUpperInvariant()} has won the round!";
        _gameTicker.EndRound(faction.ToUpperInvariant() + " Won the round by: " + message);
    }

    private void AwardPointsToFaction(string faction, AuObjectiveComponent objective)
    {
        if (_objectiveMaster == null)
            return;
        var points = objective.CustomPoints == 0
            ? (objective.ObjectiveLevel == 1 ? 5 : 20)
            : objective.CustomPoints;
        var factionKey = faction.ToLowerInvariant();
        int newPoints = 0;
        int requiredPoints = 0;
        switch (factionKey)
        {
            case "govfor":
                _objectiveMaster.CurrentWinPointsGovfor += points;
                newPoints = _objectiveMaster.CurrentWinPointsGovfor;
                requiredPoints = _objectiveMaster.RequiredWinPointsGovfor;
                break;
            case "opfor":
                _objectiveMaster.CurrentWinPointsOpfor += points;
                newPoints = _objectiveMaster.CurrentWinPointsOpfor;
                requiredPoints = _objectiveMaster.RequiredWinPointsOpfor;
                break;
            case "clf":
                _objectiveMaster.CurrentWinPointsClf += points;
                newPoints = _objectiveMaster.CurrentWinPointsClf;
                requiredPoints = _objectiveMaster.RequiredWinPointsClf;
                break;
            case "scientist":
                _objectiveMaster.CurrentWinPointsScientist += points;
                newPoints = _objectiveMaster.CurrentWinPointsScientist;
                requiredPoints = _objectiveMaster.RequiredWinPointsScientist;
                break;
        }

        if (!_finalObjectiveGivenFactions.Contains(factionKey) && newPoints >= requiredPoints)
        {
            var finalObjectives = EntityManager.EntityQuery<AuObjectiveComponent>()
                .Where(obj =>
                    !obj.Active && obj.Factions.Any(f => f.ToLowerInvariant() == factionKey) &&
                    obj.ObjectiveLevel == 3)
                .ToList();
            if (finalObjectives.Count > 0)
            {
                var random = new Random();
                var selected = finalObjectives[random.Next(finalObjectives.Count)];
                selected.Active = true;
                selected.Faction = factionKey;
                Logger.Info(
                    $"[OBJ FINAL DEBUG] Activated final objective '{selected.objectiveDescription}' for faction '{factionKey}'");
                _finalObjectiveGivenFactions.Add(factionKey);

                if (selected.Owner != EntityUid.Invalid && EntityManager.HasComponent<Content.Shared.AU14.Objectives.Fetch.FetchObjectiveComponent>(selected.Owner))
                {
                    var fetchSystem = EntityManager.EntitySysManager.GetEntitySystem<Content.Server.AU14.Objectives.Fetch.AuFetchObjectiveSystem>();
                    var fetchComp = EntityManager.GetComponent<Content.Shared.AU14.Objectives.Fetch.FetchObjectiveComponent>(selected.Owner);
                    fetchSystem.TryActivateFetchObjective(selected.Owner, fetchComp);
                }
            }
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
}

