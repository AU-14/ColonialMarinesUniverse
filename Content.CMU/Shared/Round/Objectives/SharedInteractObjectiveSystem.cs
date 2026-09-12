using System.Linq;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.CMU14.Round.Objectives.Type;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.NPC.Components;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;

namespace Content.Shared.CMU14.Round.Objectives;

public sealed partial class SharedInteractObjectiveSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InteractTrackerComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<InteractTrackerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<InteractTrackerComponent, ExaminedEvent>(OnExamined);
    }

    private string? GetRequiredTool(InteractObjectiveComponent interactComp, int currentInteractions)
    {
        if (interactComp.Tools == null || interactComp.Tools.Count == 0)
            return null;
        return interactComp.Tools[currentInteractions % interactComp.Tools.Count];
    }

    private string? GetUserFaction(EntityUid user)
    {
        if (!TryComp<NpcFactionMemberComponent>(user, out var npcFaction) || npcFaction.Factions.Count == 0)
            return null;
        // WeYu roles carry the npcFaction id AUWeYu; the objective faction key is weyu
        var factions = npcFaction.Factions
            .Select(f => f.ToString().ToLowerInvariant() switch { "auweyu" => "weyu", var id => id })
            .ToList();
        foreach (var fac in new[] { "govfor", "opfor", "clf", "weyu" })
            if (factions.Contains(fac))
                return fac;
        return factions.First();
    }

    private int GetCurrentInteractions(InteractTrackerComponent tracker, string faction)
        => tracker.InteractionsPerFaction.GetValueOrDefault(faction, 0);

    private bool TryStartInteract(EntityUid targetUid, InteractTrackerComponent tracker, EntityUid user, EntityUid? toolUsed = null)
    {
        if (!TryComp(tracker.ObjectiveUid, out InteractObjectiveComponent? interactComp) ||
            !TryComp(tracker.ObjectiveUid, out CMUObjectiveComponent? objComp) || !objComp.Active)
            return false;

        var faction = GetUserFaction(user);
        if (string.IsNullOrEmpty(faction))
            return false;

        if (objComp.StatusesPerFaction.TryGetValue(faction, out var status) &&
            status == CMUObjectiveComponent.ObjectiveStatus.Completed)
            return false;

        var entityCompletions = tracker.CompletionsPerFaction.GetValueOrDefault(faction, 0);
        if (entityCompletions >= interactComp.CompletionsPerEnt)
            return false;

        var currentInteractions = GetCurrentInteractions(tracker, faction);
        var requiredTool = GetRequiredTool(interactComp, currentInteractions);

        if (requiredTool != null)
        {
            if (toolUsed == null || !TryComp<ToolComponent>(toolUsed.Value, out var toolComp)
                                 || !toolComp.Qualities.Contains(requiredTool))
            {
                _popup.PopupEntity(
                    Loc.GetString("cmu-interact-objective-need-tool", ("tool", requiredTool)),
                    targetUid,
                    user,
                    PopupType.SmallCaution);
                return false;
            }
        }

        _popup.PopupEntity(interactComp.DoAfterMessageBegin, targetUid, user, PopupType.Medium);

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            user,
            interactComp.InteractTime,
            new InteractObjectiveDoAfterEvent { Faction = faction, InteractTarget = GetNetEntity(targetUid) },
            targetUid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };
        _doAfter.TryStartDoAfter(doAfterArgs);
        return true;
    }

    private void OnInteractHand(EntityUid uid, InteractTrackerComponent tracker, InteractHandEvent args)
    {
        if (args.Handled || !TryComp(tracker.ObjectiveUid, out InteractObjectiveComponent? interactComp))
            return;
        var faction = GetUserFaction(args.User);
        if (string.IsNullOrEmpty(faction)) return;
        if (GetRequiredTool(interactComp, GetCurrentInteractions(tracker, faction)) != null)
            return; // tool required, skip empty hand
        if (TryStartInteract(uid, tracker, args.User))
            args.Handled = true;
    }

    private void OnInteractUsing(EntityUid uid, InteractTrackerComponent tracker, InteractUsingEvent args)
    {
        if (args.Handled) return;
        if (TryStartInteract(uid, tracker, args.User, args.Used))
            args.Handled = true;
    }

    private void OnExamined(EntityUid uid, InteractTrackerComponent tracker, ExaminedEvent args)
    {
        if (!TryComp(tracker.ObjectiveUid, out InteractObjectiveComponent? interactComp) ||
            !TryComp(tracker.ObjectiveUid, out CMUObjectiveComponent? objComp) || !objComp.Active)
            return;

        var faction = GetUserFaction(args.Examiner);
        using (args.PushGroup(nameof(InteractTrackerComponent)))
        {
            if (!string.IsNullOrEmpty(faction))
            {
                var entityCompletions = tracker.CompletionsPerFaction.GetValueOrDefault(faction, 0);
                if (entityCompletions >= interactComp.CompletionsPerEnt)
                {
                    args.PushMarkup(Loc.GetString("cmu-interact-objective-completed"));
                    return;
                }
            }
            var currentInteractions = !string.IsNullOrEmpty(faction) ? GetCurrentInteractions(tracker, faction) : 0;
            var requiredTool = GetRequiredTool(interactComp, currentInteractions);
            args.PushMarkup(requiredTool != null
                ? Loc.GetString("cmu-interact-objective-use-tool", ("tool", requiredTool))
                : Loc.GetString("cmu-interact-objective-use-empty-hand"));

            if (interactComp.Tools is { Count: > 1 })
            {
                var tools = string.Join(", ", interactComp.Tools.Select(t => $"[color=cyan]{t}[/color]"));
                args.PushMarkup(Loc.GetString("cmu-interact-objective-tools-needed", ("tools", tools)));
            }
            if (interactComp.Skills.Count > 0)
            {
                var skills = string.Join(", ", interactComp.Skills.Select(s => $"[color=yellow]{s}[/color]"));
                args.PushMarkup(Loc.GetString("cmu-interact-objective-skills-required", ("skills", skills)));
            }
            if (interactComp.Access.Count > 0)
            {
                var access = string.Join(", ", interactComp.Access.Select(a => $"[color=yellow]{a}[/color]"));
                args.PushMarkup(Loc.GetString("cmu-interact-objective-access-required", ("access", access)));
            }
            if (interactComp.InteractionsNeeded > 1 && !string.IsNullOrEmpty(faction))
            {
                args.PushMarkup(Loc.GetString("cmu-interact-objective-progress",
                    ("current", currentInteractions),
                    ("needed", interactComp.InteractionsNeeded)));
            }
        }
    }
}
