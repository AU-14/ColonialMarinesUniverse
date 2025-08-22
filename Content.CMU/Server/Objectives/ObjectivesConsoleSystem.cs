using System.Linq;
using Content.Shared.AU14.Objectives;
using Content.Shared.AU14.Objectives.Capture;
using Content.Shared.AU14.Objectives.Fetch;
using Content.Shared.AU14.Objectives.Kill;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Server.AU14.Objectives;

public sealed class ObjectivesConsoleSystem : SharedObjectivesConsoleSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<ObjectivesConsoleComponent>(
            ObjectivesConsoleKey.Key,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnUiOpened);
                subs.Event<ObjectivesConsoleRequestObjectivesMessage>(OnRequestObjectives);
            }
        );
    }

    private void OnUiOpened(EntityUid uid, ObjectivesConsoleComponent comp, BoundUIOpenedEvent args)
    {
        SendObjectives(uid, comp);
    }

    private void OnRequestObjectives(EntityUid uid, ObjectivesConsoleComponent comp, ObjectivesConsoleRequestObjectivesMessage msg)
    {
        SendObjectives(uid, comp);
    }

    private void SendObjectives(EntityUid uid, ObjectivesConsoleComponent comp)
    {
        var objectives = new List<ObjectiveEntry>();
        var query = EntityManager.EntityQueryEnumerator<AuObjectiveComponent>();
        int currentWinPoints = 0;
        int requiredWinPoints = 0;
        // Find the ObjectiveMaster for this faction
        foreach (var master in EntityManager.EntityQuery<ObjectiveMasterComponent>())
        {
            switch (comp.Faction.ToLowerInvariant())
            {
                case "govfor":
                    currentWinPoints = master.CurrentWinPointsGovfor;
                    requiredWinPoints = master.RequiredWinPointsGovfor;
                    break;
                case "opfor":
                    currentWinPoints = master.CurrentWinPointsOpfor;
                    requiredWinPoints = master.RequiredWinPointsOpfor;
                    break;
                case "clf":
                    currentWinPoints = master.CurrentWinPointsClf;
                    requiredWinPoints = master.RequiredWinPointsClf;
                    break;
                case "scientist":
                    currentWinPoints = master.CurrentWinPointsScientist;
                    requiredWinPoints = master.RequiredWinPointsScientist;
                    break;
            }
            break; // Only use the first master found
        }
        while (query.MoveNext(out var objUid, out var objComp))
        {
            if (!objComp.Active)
                continue;
            var consoleFaction = comp.Faction.ToLowerInvariant();
            if (objComp.FactionNeutral)
            {
                if (objComp.Factions.Count == 0)
                    continue;
                if (objComp.Factions.All(f => f.ToLowerInvariant() != consoleFaction))
                    continue;
            }
            else
            {
                if (string.IsNullOrEmpty(objComp.Faction) || objComp.Faction.ToLowerInvariant() != consoleFaction)
                    continue;
            }
            ObjectiveStatusDisplay statusDisplay;
            // Special handling for capture objectives
            if (EntityManager.TryGetComponent(objUid, out CaptureObjectiveComponent? captureComp))
            {
                var capStatus = captureComp.GetObjectiveStatus(consoleFaction, objComp);
                switch (capStatus)
                {
                    case CaptureObjectiveComponent.CaptureObjectiveStatus.Completed:
                        statusDisplay = ObjectiveStatusDisplay.Completed;
                        break;
                    case CaptureObjectiveComponent.CaptureObjectiveStatus.Failed:
                        statusDisplay = ObjectiveStatusDisplay.Failed;
                        break;
                    case CaptureObjectiveComponent.CaptureObjectiveStatus.Captured:
                        statusDisplay = ObjectiveStatusDisplay.Captured;
                        break;
                    case CaptureObjectiveComponent.CaptureObjectiveStatus.Uncaptured:
                        statusDisplay = ObjectiveStatusDisplay.Uncaptured;
                        break;
                    default:
                        statusDisplay = ObjectiveStatusDisplay.Uncompleted;
                        break;
                }
                // --- Progress for capture objectives ---
                int factionProgress = 0;
                var factionKey = consoleFaction.ToLowerInvariant();
                if (captureComp.TimesIncrementedPerFaction.TryGetValue(factionKey, out var val))
                    factionProgress = val;
                string capProgress = captureComp.MaxHoldTimes > 0
                    ? $"{factionProgress}/{captureComp.MaxHoldTimes}"
                    : $"{factionProgress}";
                objectives.Add(new ObjectiveEntry(
                    objComp.objectiveDescription,
                    statusDisplay,
                    objComp.ObjectiveLevel == 3 ? ObjectiveTypeDisplay.Win : objComp.ObjectiveLevel == 2 ? ObjectiveTypeDisplay.Major : ObjectiveTypeDisplay.Minor,
                    capProgress,
                    objComp.Repeating,
                    objComp.Repeating ? objComp.TimesCompleted : (int?)null,
                    objComp.MaxRepeatable,
                    objComp.CustomPoints != 0 ? objComp.CustomPoints : (objComp.ObjectiveLevel == 1 ? 5 : 20)));
                continue;
            }
            else if (objComp.FactionStatuses.TryGetValue(consoleFaction, out var status))
            {
                switch (status)
                {
                    case AuObjectiveComponent.ObjectiveStatus.Completed:
                        statusDisplay = ObjectiveStatusDisplay.Completed;
                        break;
                    case AuObjectiveComponent.ObjectiveStatus.Failed:
                        statusDisplay = ObjectiveStatusDisplay.Failed;
                        break;
                    default:
                        statusDisplay = ObjectiveStatusDisplay.Uncompleted;
                        break;
                }
            }
            else
            {
                statusDisplay = ObjectiveStatusDisplay.Uncompleted;
            }
            ObjectiveTypeDisplay typeDisplay;
            if (objComp.ObjectiveLevel == 3)
                typeDisplay = ObjectiveTypeDisplay.Win;
            else if (objComp.ObjectiveLevel == 2)
                typeDisplay = ObjectiveTypeDisplay.Major;
            else
                typeDisplay = ObjectiveTypeDisplay.Minor;

            // Fetch progress logic
            string? fetchProgress = null;
            if (EntityManager.TryGetComponent(objUid, out FetchObjectiveComponent? fetchComp))
            {
                int fetched = 0;
                int toFetch = fetchComp.AmountToFetch;
                if (objComp.FactionNeutral)
                {
                    fetchComp.AmountFetchedPerFaction.TryGetValue(consoleFaction, out fetched);
                }
                else
                {
                    fetchComp.AmountFetchedPerFaction.TryGetValue(objComp.Faction.ToLowerInvariant(), out fetched);
                }
                fetchProgress = $"{fetched}/{toFetch}";
            }
            // Add logic to display kill progress for KillObjectiveComponent
            if (EntityManager.TryGetComponent(objUid, out KillObjectiveComponent? killComp))
            {
                int killed = 0;
                int toKill = killComp.AmountToKill;
                killComp.AmountKilledPerFaction.TryGetValue(consoleFaction.ToLowerInvariant(), out killed);
                fetchProgress = $"{killed}/{toKill} kills";
            }

            int? repeatsCompleted = objComp.Repeating ? objComp.TimesCompleted : (int?)null;
            int? maxRepeatable = objComp.MaxRepeatable;
            int points = objComp.CustomPoints != 0 ? objComp.CustomPoints : (objComp.ObjectiveLevel == 1 ? 5 : 20);
            objectives.Add(new ObjectiveEntry(objComp.objectiveDescription, statusDisplay, typeDisplay, fetchProgress, objComp.Repeating, repeatsCompleted, maxRepeatable, points));
        }
        var state = new ObjectivesConsoleBoundUserInterfaceState(objectives, currentWinPoints, requiredWinPoints);
        _ui.SetUiState(uid, ObjectivesConsoleKey.Key, state);
    }

    public void RefreshConsolesForFaction(string faction)
    {
        var query = EntityManager.EntityQueryEnumerator<ObjectivesConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (string.Equals(comp.Faction, faction, StringComparison.OrdinalIgnoreCase))
            {
                SendObjectives(uid, comp);
            }
        }
    }
}
