using System.Linq;
using Content.Shared._CMU14.Round.Objectives.Component;
using Content.Shared._CMU14.Round.Objectives.Type;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.NPC.Components;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;

namespace Content.Shared._CMU14.Round.Objectives;

public sealed class SharedInteractObjectiveSystem : EntitySystem
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
        foreach (var fac in new[] { "govfor", "opfor", "clf", "scientist" })
            if (npcFaction.Factions.Any(f => f.ToString().ToLowerInvariant() == fac))
                return fac;
        return npcFaction.Factions.First().ToString().ToLowerInvariant();
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
                _popup.PopupEntity($"You need a {requiredTool} for this step.", targetUid, user, PopupType.SmallCaution);
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
                    args.PushMarkup("[color=green]This has already been completed.[/color]");
                    return;
                }
            }
            var currentInteractions = !string.IsNullOrEmpty(faction) ? GetCurrentInteractions(tracker, faction) : 0;
            var requiredTool = GetRequiredTool(interactComp, currentInteractions);
            args.PushMarkup(requiredTool != null
                ? $"Use a [color=cyan]{requiredTool}[/color] on this."
                : "Use an [color=cyan]empty hand[/color] to interact with this.");

            if (interactComp.Tools is { Count: > 1 })
                args.PushMarkup($"Tools needed: {string.Join(", ", interactComp.Tools.Select(t => $"[color=cyan]{t}[/color]"))}");
            if (interactComp.Skills.Count > 0)
                args.PushMarkup($"Requires skills: {string.Join(", ", interactComp.Skills.Select(s => $"[color=yellow]{s}[/color]"))}");
            if (interactComp.Access.Count > 0)
                args.PushMarkup($"Requires access: {string.Join(", ", interactComp.Access.Select(a => $"[color=yellow]{a}[/color]"))}");
            if (interactComp.InteractionsNeeded > 1 && !string.IsNullOrEmpty(faction))
                args.PushMarkup($"Progress: [color=white]{currentInteractions}[/color]/[color=white]{interactComp.InteractionsNeeded}[/color] interactions.");
        }
    }
}
