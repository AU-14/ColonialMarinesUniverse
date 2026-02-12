using System.Linq;
using Content.Shared.AU14.Objectives;
using Content.Shared.AU14.Objectives.Capture;
using Content.Shared.AU14.Objectives.Fetch;
using Content.Shared.AU14.Objectives.Kill;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Content.Shared._RMC14.Intel;
using Content.Shared.Ghost;
using Content.Shared._RMC14.Marines;

namespace Content.Server.AU14.Objectives;

public sealed class ObjectivesConsoleSystem : SharedObjectivesConsoleSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly Content.Shared._RMC14.Intel.IntelSystem _intel = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<ObjectivesConsoleComponent>(
            ObjectivesConsoleKey.Key,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnUiOpened);
                subs.Event<ObjectivesConsoleRequestObjectivesMessage>(OnRequestObjectives);
                subs.Event<ObjectivesConsoleRequestIntelMessage>(OnRequestIntel);
                subs.Event<ObjectivesConsoleUnlockIntelMessage>(OnUnlockIntel);
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
        Logger.Debug($"[ObjectivesConsole] SendObjectives called for console={ToPrettyString(uid)} faction={comp.Faction}");
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

                // Determine display title/description based on unlocked intel tier for this console faction
                var displayDesc = objComp.objectiveDescription;
                var displayTitle = objComp.ID;
                var protoMan = IoCManager.Resolve<Robust.Shared.Prototypes.IPrototypeManager>();
                if (objComp.IntelTiers.Count > 0)
                {
                    // Default to tier 0 unlocked (count == 1) if no entry exists
                    var unlockedCount = 1;
                    if (objComp.IntelTierPerFaction.TryGetValue(consoleFaction, out var v))
                        unlockedCount = v;
                    if (unlockedCount > 0)
                    {
                        // If unlockedCount exceeds number of tiers, show the last tier.
                        var idx = System.Math.Min(unlockedCount, objComp.IntelTiers.Count) - 1;
                        var protoId = objComp.IntelTiers[idx];
                        if (protoMan.TryIndex<ObjectiveIntelTierPrototype>(protoId, out var proto))
                        {
                            if (!string.IsNullOrEmpty(proto.TitleText))
                                displayTitle = proto.TitleText;
                            if (!string.IsNullOrEmpty(proto.DescriptionText))
                                displayDesc = proto.DescriptionText;
                        }
                    }
                }

                objectives.Add(new ObjectiveEntry(
                    objComp.ID,
                    displayDesc,
                    statusDisplay,
                    objComp.ObjectiveLevel == 3 ? ObjectiveTypeDisplay.Win : objComp.ObjectiveLevel == 2 ? ObjectiveTypeDisplay.Major : ObjectiveTypeDisplay.Minor,
                    capProgress,
                    objComp.Repeating,
                    objComp.Repeating ? objComp.TimesCompleted : (int?)null,
                    objComp.MaxRepeatable,
                    objComp.CustomPoints != 0 ? objComp.CustomPoints : (objComp.ObjectiveLevel == 1 ? 5 : 20)));
                Logger.Debug($"[ObjectivesConsole] Added objective to list: id={objComp.ID} displayDesc={displayDesc} status={statusDisplay}");
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

            // Determine display title/description based on unlocked intel tier for this console faction
            var displayDesc2 = objComp.objectiveDescription;
            var displayTitle2 = objComp.ID;
            var protoMan2 = IoCManager.Resolve<Robust.Shared.Prototypes.IPrototypeManager>();
            if (objComp.IntelTiers.Count > 0)
            {
                // Default to tier 0 unlocked (count == 1) if no entry exists
                var unlockedCount2 = 1;
                if (objComp.IntelTierPerFaction.TryGetValue(consoleFaction, out var v2))
                    unlockedCount2 = v2;
                if (unlockedCount2 > 0)
                {
                    var idx2 = System.Math.Min(unlockedCount2, objComp.IntelTiers.Count) - 1;
                    var protoId2 = objComp.IntelTiers[idx2];
                    if (protoMan2.TryIndex<ObjectiveIntelTierPrototype>(protoId2, out var proto2))
                    {
                        if (!string.IsNullOrEmpty(proto2.TitleText))
                            displayTitle2 = proto2.TitleText;
                        if (!string.IsNullOrEmpty(proto2.DescriptionText))
                            displayDesc2 = proto2.DescriptionText;
                    }
                }
            }

            int? repeatsCompleted2 = objComp.Repeating ? objComp.TimesCompleted : (int?)null;
            int? maxRepeatable2 = objComp.MaxRepeatable;
            int points2 = objComp.CustomPoints != 0 ? objComp.CustomPoints : (objComp.ObjectiveLevel == 1 ? 5 : 20);
            objectives.Add(new ObjectiveEntry(objComp.ID, displayDesc2, statusDisplay, typeDisplay, fetchProgress, objComp.Repeating, repeatsCompleted2, maxRepeatable2, points2));
            Logger.Debug($"[ObjectivesConsole] Added objective to list: id={objComp.ID} displayDesc={displayDesc2} status={statusDisplay}");
        }
        var state = new ObjectivesConsoleBoundUserInterfaceState(objectives, currentWinPoints, requiredWinPoints);
        Logger.Debug($"[ObjectivesConsole] Sending Objectives state: count={objectives.Count} win={currentWinPoints}/{requiredWinPoints}");
        _ui.SetUiState(uid, ObjectivesConsoleKey.Key, state);
    }

    private void OnRequestIntel(EntityUid uid, ObjectivesConsoleComponent comp, ObjectivesConsoleRequestIntelMessage msg)
    {
        // Debug: log request arrival
        Logger.Debug($"[ObjectivesConsole] OnRequestIntel called for objective={msg.ObjectiveId} console={ToPrettyString(uid)} actor={msg.Actor} consoleFaction={comp.Faction}");

        // Find the objective by ID
        var query = EntityManager.EntityQueryEnumerator<AuObjectiveComponent>();
        while (query.MoveNext(out var objUid, out var objComp))
        {
            if (objComp.ID != msg.ObjectiveId)
                continue;

            // Prepare tiers
            var tiers = new List<ObjectiveIntelTierEntry>();
            if (objComp.IntelTiers.Count == 0)
            {
                // No tiers => full intel by default. Represent as a single unlocked tier that shows full title/desc.
                tiers.Add(new ObjectiveIntelTierEntry(0, objComp.ID, objComp.objectiveDescription, 0));

                // Always display intel based on the console's faction
                var teamKeyDefault = string.IsNullOrEmpty(comp.Faction) ? Team.None : comp.Faction.ToLowerInvariant();
                // Tier 0 is always unlocked by default, so report unlocked count as 1
                var stateFull = new ObjectiveIntelBoundUserInterfaceState(objComp.ID, objComp.objectiveDescription, tiers, 1, _intel.GetIntelPoints(teamKeyDefault));

                Logger.Debug($"[ObjectivesConsole] Sending intel UI state: objective={objComp.ID} team={teamKeyDefault} tiers={tiers.Count} unlocked=1 points={_intel.GetIntelPoints(teamKeyDefault)}");
                _ui.SetUiState(uid, ObjectivesConsoleKey.Key, stateFull);
                return;
            }

            var protoMan = IoCManager.Resolve<Robust.Shared.Prototypes.IPrototypeManager>();
            for (int i = 0; i < objComp.IntelTiers.Count; i++)
            {
                var protoId = objComp.IntelTiers[i];
                if (!protoMan.TryIndex<ObjectiveIntelTierPrototype>(protoId, out var proto))
                {
                    // skip invalid prototype
                    Logger.Debug($"[ObjectivesConsole] Missing ObjectiveIntelTierPrototype protoId={protoId} for objective={objComp.ID}");
                    continue;
                }

                // For UI we need to show previous tier title/description as well (so include index-1)
                var title = string.IsNullOrEmpty(proto.TitleText) ? objComp.ID : proto.TitleText;
                var desc = string.IsNullOrEmpty(proto.DescriptionText) ? objComp.objectiveDescription : proto.DescriptionText;
                tiers.Add(new ObjectiveIntelTierEntry(i, title, desc, proto.CostToUnlock));
            }

            // Always base displayed unlocked tier and points on the console's faction
            var team = string.IsNullOrEmpty(comp.Faction) ? Team.None : comp.Faction.ToLowerInvariant();

            // Ensure the objective component has an entry for this team so the UI can always show a value
            if (!objComp.IntelTierPerFaction.ContainsKey(team))
            {
                // Tier 0 is always unlocked by default, so initialize to 1 (count of unlocked tiers)
                objComp.IntelTierPerFaction[team] = 1;
                Dirty(objUid, objComp);
                Logger.Debug($"[ObjectivesConsole] Initialized IntelTierPerFaction for objective={objComp.ID} team={team}");
            }

            // Treat missing entry as tier0 unlocked (count == 1)
            int unlocked = 1;
            if (objComp.IntelTierPerFaction.TryGetValue(team, out var factionTier))
            {
                unlocked = factionTier;
            }

            Logger.Debug($"[ObjectivesConsole] Sending intel UI state: objective={objComp.ID} team={team} tiers={tiers.Count} unlocked={unlocked} points={_intel.GetIntelPoints(team)}");

            var state2 = new ObjectiveIntelBoundUserInterfaceState(objComp.ID, objComp.objectiveDescription, tiers, unlocked, _intel.GetIntelPoints(team));
            _ui.SetUiState(uid, ObjectivesConsoleKey.Key, state2);
            return;
        }
    }

    private void OnUnlockIntel(EntityUid uid, ObjectivesConsoleComponent comp, ObjectivesConsoleUnlockIntelMessage msg)
    {
        // Server-side: validate objective exists and tier index
        var objQuery = EntityManager.EntityQueryEnumerator<AuObjectiveComponent>();
        while (objQuery.MoveNext(out var objUid, out var objComp))
        {
            if (objComp.ID != msg.ObjectiveId)
                continue;

            if (msg.TierIndex < 0 || msg.TierIndex >= objComp.IntelTiers.Count)
                return;

            // Determine actor's team (the team paying for the unlock)
            // Use the console's faction as the authoritative team for intel purchases
            var teamKey = string.IsNullOrEmpty(comp.Faction) ? Team.None : comp.Faction.ToLowerInvariant();

            // Default to tier 0 unlocked (count == 1) if no entry exists
            int currentUnlocked = 1;
            if (objComp.IntelTierPerFaction.TryGetValue(teamKey, out var val))
            {
                currentUnlocked = val;
            }

            // Expect the requested tier index to equal the currentUnlocked (i.e. unlock the next tier at index currentUnlocked)
            if (msg.TierIndex != currentUnlocked)
            {
                // Either already unlocked or trying to skip tiers -> just refresh UI
                RefreshConsolesForFaction(teamKey);
                return;
            }

            var protoMan = IoCManager.Resolve<Robust.Shared.Prototypes.IPrototypeManager>();
            var protoId = objComp.IntelTiers[msg.TierIndex];
            if (!protoMan.TryIndex<ObjectiveIntelTierPrototype>(protoId, out var proto))
            {
                Logger.Debug($"[ObjectivesConsole] Unlock failed - missing proto for protoId={protoId} objective={objComp.ID}");
                return;
            }

            var costDouble = proto.CostToUnlock;

            // Use intel points (from IntelSystem) to unlock objective intel tiers for the actor's team
            var spent = _intel.TrySpendIntelPoints(teamKey, costDouble);
            if (!spent)
            {
                // Not enough intel points; just refresh UI so client sees current points
                Logger.Debug($"[ObjectivesConsole] Unlock failed - insufficient intel for team={teamKey} cost={costDouble} objective={objComp.ID}");
                RefreshConsolesForFaction(teamKey);
                return;
            }

            // Apply the unlock for this faction and mark component dirty (store count of unlocked tiers)
            objComp.IntelTierPerFaction[teamKey] = currentUnlocked + 1;
            Dirty(objUid, objComp);
            Logger.Debug($"[ObjectivesConsole] Unlock applied for objective={objComp.ID} team={teamKey} newUnlockedCount={objComp.IntelTierPerFaction[teamKey]}");

            // Refresh the console UI for this faction
            RefreshConsolesForFaction(teamKey);
            return;
        }
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
