using System.Linq;
using Content.Shared.AU14.Objectives;
using Content.Shared.AU14.Objectives.Fetch;
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
        var factionKey = comp.Faction.ToLowerInvariant();
        int currentWinPoints = 0;
        int requiredWinPoints = 0;
        // Find the ObjectiveMaster for this faction
        foreach (var master in EntityManager.EntityQuery<ObjectiveMasterComponent>())
        {
            switch (factionKey)
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
        while (query.MoveNext(out _, out var objComp))
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
            Robust.Shared.Log.Logger.Info($"[OBJ CONSOLE DEBUG] Checking objective '{objComp.objectiveDescription}' for factionKey '{factionKey}'. FactionStatuses keys: [{string.Join(", ", objComp.FactionStatuses.Keys)}]");
            ObjectiveStatusDisplay statusDisplay;
            if (objComp.FactionStatuses.TryGetValue(consoleFaction, out var status))
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
            string? progress = null;
            if (EntityManager.TryGetComponent(objComp.Owner, out FetchObjectiveComponent? fetchComp))
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
                progress = $"{fetched}/{toFetch}";
            }

            int? repeatsCompleted = objComp.Repeating ? objComp.TimesCompleted : (int?)null;
            int? maxRepeatable = objComp.MaxRepeatable;
            // Calculate points: use CustomPoints if set, otherwise default (5 for minor, 20 for major/win)
            int points = objComp.CustomPoints != 0 ? objComp.CustomPoints : (objComp.ObjectiveLevel == 1 ? 5 : 20);
            objectives.Add(new ObjectiveEntry(objComp.objectiveDescription, statusDisplay, typeDisplay, progress, objComp.Repeating, repeatsCompleted, maxRepeatable, points));
        }
        var state = new ObjectivesConsoleBoundUserInterfaceState(objectives, currentWinPoints, requiredWinPoints);
        _ui.SetUiState(uid, ObjectivesConsoleKey.Key, state);
    }

    public void RefreshConsolesForFaction(string faction)
    {
        var factionKey = faction.ToLowerInvariant();
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
