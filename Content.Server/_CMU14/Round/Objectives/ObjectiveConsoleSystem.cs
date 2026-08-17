using System.Linq;
using Content.Shared._CMU14.Round.Objectives;
using Content.Shared._CMU14.Round.Objectives.Components;
using Content.Shared._CMU14.Round.Objectives.Type;
using Content.Shared._RMC14.Intel;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using ObjectivesConsoleComponent = Content.Shared._CMU14.Round.Objectives.Components.ObjectivesConsoleComponent;

namespace Content.Server._CMU14.Round.Objectives;

public sealed partial class ObjectiveConsoleSystem : SharedObjectiveConsoleSystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IntelSystem _intel = default!;
    [Dependency] private ObjectiveControlSystem _objCtrl = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private ISawmill _logs = default!;

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("objectives");

        Subs.BuiEvents<ObjectivesConsoleComponent>(
            ObjectivesConsoleKey.Key,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnUiOpened);
                subs.Event<ObjectivesConsoleRequestObjectivesMessage>(OnRequestObjectives);
                subs.Event<ObjectivesConsoleRequestIntelMessage>(OnRequestIntel);
                subs.Event<ObjectivesConsoleUnlockIntelMessage>(OnUnlockIntel);
            });
    }

    private (string Title, string Description) ResolveIntelTierText(CMUObjectiveComponent objComp, int tierIndex)
    {
        string? title = null, desc = null;
        for (var i = Math.Min(tierIndex, objComp.IntelTiersProtos.Count - 1); i >= 0; i--)
        {
            if (!_proto.TryIndex(objComp.IntelTiersProtos[i], out var proto)) continue;
            if (title == null && !string.IsNullOrEmpty(proto.TitleText)) title = proto.TitleText;
            if (desc == null && !string.IsNullOrEmpty(proto.DescriptionText)) desc = proto.DescriptionText;
            if (title != null && desc != null) break;
        }
        return (title ?? objComp.Id, desc ?? objComp.ObjectiveDescription);
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
        _logs.Debug($"[OBJ-CON] SendObjectives called for console='{ToPrettyString(uid)}', where faction='{comp.Faction}'");
        var objectives = new List<ObjectiveEntry>();
        var query = EntityQueryEnumerator<CMUObjectiveComponent>();
        (int currentWinPoints, int requiredWinPoints) = _objCtrl.GetWinPoints(comp.Faction);

        var planetMap = _objCtrl.GetPlanetMapId();
        if (planetMap == null) return;
        while (query.MoveNext(out var objUid, out var objComp))
        {
            if (Transform(objUid).MapID != planetMap) continue;

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

            var showObjective = objComp.Active;

            var hasCapture = TryComp(objUid, out CaptureObjectiveComponent? captureComp);

            if (!showObjective)
            {
                if (hasCapture && captureComp != null)
                {
                    var capCheck = captureComp.GetObjectiveStatus(consoleFaction, objComp);
                    if (capCheck == CaptureObjectiveComponent.CaptureObjectiveStatus.Completed)
                        showObjective = true;
                }
                else
                {
                    if (objComp.StatusesPerFaction.TryGetValue(consoleFaction, out var statusCheck) &&
                        statusCheck == CMUObjectiveComponent.ObjectiveStatus.Completed)
                    {
                        showObjective = true;
                    }
                }
            }

            if (!showObjective)
                continue;

            ObjectiveStatusDisplay statusDisplay;
            if (hasCapture && captureComp != null)
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
                int factionProgress = 0;
                var factionKey = consoleFaction.ToLowerInvariant();
                if (captureComp.TimesIncrementedPerFaction.TryGetValue(factionKey, out var val))
                    factionProgress = val;
                string capProgress = captureComp.MaxHoldTimes > 0
                    ? $"{factionProgress}/{captureComp.MaxHoldTimes}"
                    : $"{factionProgress}";

                var displayDesc = objComp.ObjectiveDescription;
                if (objComp.IntelTiersProtos.Count > 0)
                {
                    var unlockedCount = 1;
                    if (objComp.DefaultIntelTiers.TryGetValue(consoleFaction, out var v))
                        unlockedCount = v;
                    if (unlockedCount > 0)
                    {
                        var idx = Math.Min(unlockedCount, objComp.IntelTiersProtos.Count) - 1;
                        displayDesc = ResolveIntelTierText(objComp, idx).Description;
                    }
                }

                objectives.Add(new ObjectiveEntry(
                    objComp.Id,
                    displayDesc,
                    statusDisplay,
                    objComp.ObjectiveLevel == 3 ? ObjectiveTypeDisplay.Win : objComp.ObjectiveLevel == 2 ? ObjectiveTypeDisplay.Major : ObjectiveTypeDisplay.Minor,
                    capProgress,
                    objComp.Repeating,
                    objComp.Repeating ? objComp.TimesCompleted : null,
                    objComp.MaxRepeatable,
                    objComp.CustomPoints != 0 ? objComp.CustomPoints : (objComp.ObjectiveLevel == 1 ? 5 : 20)));
                _logs.Debug($"[OBJ-CON] Added objective to list: id={objComp.Id} displayDesc={displayDesc} status={statusDisplay}");
                continue;
            }
            else if (objComp.StatusesPerFaction.TryGetValue(consoleFaction, out var status))
            {
                switch (status)
                {
                    case CMUObjectiveComponent.ObjectiveStatus.Completed:
                        statusDisplay = ObjectiveStatusDisplay.Completed;
                        break;
                    case CMUObjectiveComponent.ObjectiveStatus.Failed:
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

            string? fetchProgress = null;
            if (TryComp(objUid, out FetchObjectiveComponent? fetchComp))
            {
                int fetched;
                int toFetch = fetchComp.FetchCount;
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
            if (TryComp(objUid, out KillObjectiveComponent? killComp))
            {
                int toKill = killComp.KillCount;
                killComp.AmountKilledPerFaction.TryGetValue(consoleFaction.ToLowerInvariant(), out int killed);
                fetchProgress = $"{killed}/{toKill} kills";
            }

            var displayDesc2 = objComp.ObjectiveDescription;
            if (objComp.IntelTiersProtos.Count > 0)
            {
                var unlockedCount2 = 1;
                if (objComp.DefaultIntelTiers.TryGetValue(consoleFaction, out var v2))
                    unlockedCount2 = v2;
                if (unlockedCount2 > 0)
                {
                    var idx2 = Math.Min(unlockedCount2, objComp.IntelTiersProtos.Count) - 1;
                    displayDesc2 = ResolveIntelTierText(objComp, idx2).Description;
                }
            }

            int? repeatsCompleted2 = objComp.Repeating ? objComp.TimesCompleted : null;
            int? maxRepeatable2 = objComp.MaxRepeatable;
            int points2 = objComp.CustomPoints != 0 ? objComp.CustomPoints : (objComp.ObjectiveLevel == 1 ? 5 : 20);
            objectives.Add(new ObjectiveEntry(objComp.Id, displayDesc2, statusDisplay, typeDisplay, fetchProgress, objComp.Repeating, repeatsCompleted2, maxRepeatable2, points2));
            _logs.Debug($"[OBJ-CON] Added objective to list: id={objComp.Id} displayDesc={displayDesc2} status={statusDisplay}");
        }
        var state = new ObjectivesConsoleBoundUserInterfaceState(objectives, currentWinPoints, requiredWinPoints);
        _logs.Debug($"[OBJ-CON] Sending Objectives state: count={objectives.Count} win={currentWinPoints}/{requiredWinPoints}");
        _ui.SetUiState(uid, ObjectivesConsoleKey.Key, state);
    }

    private void OnRequestIntel(EntityUid uid, ObjectivesConsoleComponent comp, ObjectivesConsoleRequestIntelMessage msg)
    {
        _logs.Debug($"[OBJ-CON] OnRequestIntel called for objective={msg.ObjectiveId} console={ToPrettyString(uid)} consoleFaction={comp.Faction}");

        var planetMap = _objCtrl.GetPlanetMapId();
        if (planetMap == null) return;
        var consoleFaction = comp.Faction.ToLowerInvariant();

        var query = EntityQueryEnumerator<CMUObjectiveComponent>();
        while (query.MoveNext(out var objUid, out var objComp))
        {
            if (objComp.Id != msg.ObjectiveId)
                continue;

            if (Transform(objUid).MapID != planetMap)
                continue;

            if (objComp.FactionNeutral)
            {
                if (objComp.Factions.Count == 0 || objComp.Factions.All(f => f.ToLowerInvariant() != consoleFaction))
                    continue;
            }
            else if (string.IsNullOrEmpty(objComp.Faction) || objComp.Faction.ToLowerInvariant() != consoleFaction)
            {
                continue;
            }

            var tiers = new List<ObjectiveIntelTierEntry>();
            if (objComp.IntelTiersProtos.Count == 0)
            {
                tiers.Add(new ObjectiveIntelTierEntry(0, objComp.Id, objComp.ObjectiveDescription, 0));

                var teamKeyDefault = string.IsNullOrEmpty(comp.Faction) ? Team.None : comp.Faction.ToLowerInvariant();
                var stateFull = new ObjectiveIntelBoundUserInterfaceMessage(objComp.Id, objComp.ObjectiveDescription, tiers, 1, _intel.GetIntelPoints(teamKeyDefault));

                _logs.Debug($"[OBJ-CON] Sending intel UI state: objective={objComp.Id} team={teamKeyDefault} tiers={tiers.Count} unlocked=1 points={_intel.GetIntelPoints(teamKeyDefault)}");
                _ui.ServerSendUiMessage(uid, ObjectivesConsoleKey.Key, stateFull, msg.Actor);
                return;
            }

            for (int i = 0; i < objComp.IntelTiersProtos.Count; i++)
            {
                var protoId = objComp.IntelTiersProtos[i];
                if (!_proto.TryIndex(protoId, out var proto))
                {
                    _logs.Debug($"[OBJ-CON] Missing ObjectiveIntelTierPrototype protoId={protoId} for objective={objComp.Id}");
                    continue;
                }

                var (title, desc) = ResolveIntelTierText(objComp, i);
                tiers.Add(new ObjectiveIntelTierEntry(i, title, desc, proto.CostToUnlock));
            }

            var team = string.IsNullOrEmpty(comp.Faction) ? Team.None : comp.Faction.ToLowerInvariant();

            if (objComp.DefaultIntelTiers.TryAdd(team, 1))
            {
                Dirty(objUid, objComp);
                _logs.Debug($"[OBJ-CON] Initialized DefaultIntelTiers for objective={objComp.Id} team={team}");
            }

            int unlocked = 1;
            if (objComp.DefaultIntelTiers.TryGetValue(team, out var factionTier))
            {
                unlocked = factionTier;
            }

            _logs.Debug($"[OBJ-CON] Sending intel UI state: objective={objComp.Id} team={team} tiers={tiers.Count} unlocked={unlocked} points={_intel.GetIntelPoints(team)}");

            var state2 = new ObjectiveIntelBoundUserInterfaceMessage(objComp.Id, objComp.ObjectiveDescription, tiers, unlocked, _intel.GetIntelPoints(team));
            _ui.ServerSendUiMessage(uid, ObjectivesConsoleKey.Key, state2, msg.Actor);
            return;
        }
    }

    private void OnUnlockIntel(EntityUid uid, ObjectivesConsoleComponent comp, ObjectivesConsoleUnlockIntelMessage msg)
    {
        var planetMap = _objCtrl.GetPlanetMapId();
        if (planetMap == null) return;
        var consoleFaction = comp.Faction.ToLowerInvariant();

        var objQuery = EntityQueryEnumerator<CMUObjectiveComponent>();
        while (objQuery.MoveNext(out var objUid, out var objComp))
        {
            if (objComp.Id != msg.ObjectiveId)
                continue;

            if (Transform(objUid).MapID != planetMap)
                continue;

            if (objComp.FactionNeutral)
            {
                if (objComp.Factions.Count == 0 || objComp.Factions.All(f => f.ToLowerInvariant() != consoleFaction))
                    continue;
            }
            else if (string.IsNullOrEmpty(objComp.Faction) || objComp.Faction.ToLowerInvariant() != consoleFaction)
            {
                continue;
            }

            if (msg.TierIndex < 0 || msg.TierIndex >= objComp.IntelTiersProtos.Count)
                return;

            var teamKey = string.IsNullOrEmpty(comp.Faction) ? Team.None : comp.Faction.ToLowerInvariant();

            int currentUnlocked = 1;
            if (objComp.DefaultIntelTiers.TryGetValue(teamKey, out var val))
            {
                currentUnlocked = val;
            }

            if (msg.TierIndex != currentUnlocked)
            {
                RefreshConsolesForFaction(teamKey);
                return;
            }

            var protoId = objComp.IntelTiersProtos[msg.TierIndex];
            if (!_proto.TryIndex(protoId, out var proto))
            {
                _logs.Debug($"[OBJ-CON] Unlock failed - missing proto for protoId={protoId} objective={objComp.Id}");
                return;
            }

            var costDouble = proto.CostToUnlock;

            var spent = _intel.TrySpendIntelPoints(teamKey, costDouble);
            if (!spent)
            {
                _logs.Debug($"[OBJ-CON] Unlock failed - insufficient intel for team={teamKey} cost={costDouble} objective={objComp.Id}");
                RefreshConsolesForFaction(teamKey);
                return;
            }

            objComp.DefaultIntelTiers[teamKey] = currentUnlocked + 1;
            Dirty(objUid, objComp);
            _logs.Debug($"[OBJ-CON] Unlock applied for objective={objComp.Id} team={teamKey} newUnlockedCount={objComp.DefaultIntelTiers[teamKey]}");

            RefreshConsolesForFaction(teamKey);
            return;
        }
    }

    public void RefreshConsolesForFaction(string faction)
    {
        var query = EntityQueryEnumerator<ObjectivesConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (string.Equals(comp.Faction, faction, StringComparison.OrdinalIgnoreCase))
                SendObjectives(uid, comp);
        }
    }
}
