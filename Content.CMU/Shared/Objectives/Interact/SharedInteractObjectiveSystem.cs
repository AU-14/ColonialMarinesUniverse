using System.Linq;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.NPC.Components;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;

namespace Content.Shared.AU14.Objectives.Interact;

/// <summary>
/// Shared system for Interact objectives. Handles InteractHandEvent and InteractUsingEvent
/// on tracked entities, validates requirements, and starts DoAfters.
/// </summary>
public sealed class SharedInteractObjectiveSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InteractObjectiveTrackerComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<InteractObjectiveTrackerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<InteractObjectiveTrackerComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>
    /// Gets the tool quality required for the current interaction step, or null if no tool is needed.
    /// Tools cycle sequentially through the Tools list based on the current interaction count.
    /// </summary>
    private string? GetRequiredTool(InteractObjectiveComponent interactComp, int currentInteractions)
    {
        if (interactComp.Tools == null || interactComp.Tools.Count == 0)
            return null;

        var index = currentInteractions % interactComp.Tools.Count;
        return interactComp.Tools[index];
    }

    /// <summary>
    /// Gets the user's faction string, or null if they have no recognized faction.
    /// </summary>
    private string? GetUserFaction(EntityUid user)
    {
        if (!_entManager.TryGetComponent<NpcFactionMemberComponent>(user, out var npcFaction) || npcFaction.Factions.Count == 0)
            return null;

        foreach (var fac in new[] { "govfor", "opfor", "clf", "scientist" })
        {
            if (npcFaction.Factions.Any(f => f.ToString().ToLowerInvariant() == fac))
                return fac;
        }

        return npcFaction.Factions.First().ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Gets the current interaction count for a specific faction on a tracker entity.
    /// </summary>
    private int GetCurrentInteractions(InteractObjectiveTrackerComponent tracker, string faction)
    {
        return tracker.InteractionsPerFaction.GetValueOrDefault(faction, 0);
    }

    /// <summary>
    /// Tries to start an interact DoAfter on the target entity.
    /// Returns true if the DoAfter was started.
    /// </summary>
    private bool TryStartInteract(EntityUid targetUid, InteractObjectiveTrackerComponent tracker, EntityUid user, EntityUid? toolUsed = null)
    {
        if (!_entManager.TryGetComponent<InteractObjectiveComponent>(tracker.ObjectiveUid, out var interactComp))
            return false;

        if (!_entManager.TryGetComponent<AuObjectiveComponent>(tracker.ObjectiveUid, out var objComp))
            return false;

        if (!objComp.Active)
            return false;

        var faction = GetUserFaction(user);
        if (string.IsNullOrEmpty(faction))
            return false;

        // Check if already fully completed for this faction
        if (objComp.FactionStatuses.TryGetValue(faction, out var status) &&
            status == AuObjectiveComponent.ObjectiveStatus.Completed)
            return false;

        // Check if this specific entity has reached its max completions for this faction
        var entityCompletions = tracker.CompletionsPerFaction.GetValueOrDefault(faction, 0);
        if (entityCompletions >= interactComp.CompletionsPerEnt)
            return false;

        var currentInteractions = GetCurrentInteractions(tracker, faction);

        // Check tool requirement
        var requiredTool = GetRequiredTool(interactComp, currentInteractions);
        if (requiredTool != null)
        {
            if (toolUsed == null || !_entManager.TryGetComponent<ToolComponent>(toolUsed.Value, out var toolComp))
            {
                _popup.PopupEntity($"You need a {requiredTool} for this step.", targetUid, user, PopupType.SmallCaution);
                return false;
            }

            if (!toolComp.Qualities.Contains(requiredTool))
            {
                _popup.PopupEntity($"You need a {requiredTool} for this step.", targetUid, user, PopupType.SmallCaution);
                return false;
            }
        }
        else if (toolUsed != null)
        {
            // A tool was used but none is required for this step - that's fine, still allow interaction
        }

        // Start the DoAfter
        _popup.PopupEntity(interactComp.DoAfterMessageBegin, targetUid, user, PopupType.Medium);

        var doAfterArgs = new DoAfterArgs(
            _entManager,
            user,
            interactComp.InteractTime,
            new InteractObjectiveDoAfterEvent
            {
                Faction = faction,
                InteractTarget = _entManager.GetNetEntity(targetUid)
            },
            targetUid) // eventTarget is the tracked entity
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        return true;
    }

    private void OnInteractHand(EntityUid uid, InteractObjectiveTrackerComponent tracker, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!_entManager.TryGetComponent<InteractObjectiveComponent>(tracker.ObjectiveUid, out var interactComp))
            return;

        var faction = GetUserFaction(args.User);
        if (string.IsNullOrEmpty(faction))
            return;

        var currentInteractions = GetCurrentInteractions(tracker, faction);
        var requiredTool = GetRequiredTool(interactComp, currentInteractions);

        // If a tool is required for this step, don't handle bare-hand interaction
        if (requiredTool != null)
            return;

        if (TryStartInteract(uid, tracker, args.User))
            args.Handled = true;
    }

    private void OnInteractUsing(EntityUid uid, InteractObjectiveTrackerComponent tracker, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_entManager.TryGetComponent<InteractObjectiveComponent>(tracker.ObjectiveUid, out _))
            return;

        if (TryStartInteract(uid, tracker, args.User, args.Used))
            args.Handled = true;
    }

    private void OnExamined(EntityUid uid, InteractObjectiveTrackerComponent tracker, ExaminedEvent args)
    {
        if (!_entManager.TryGetComponent<InteractObjectiveComponent>(tracker.ObjectiveUid, out var interactComp))
            return;

        if (!_entManager.TryGetComponent<AuObjectiveComponent>(tracker.ObjectiveUid, out var objComp))
            return;

        if (!objComp.Active)
            return;

        // Determine the examiner's faction to show per-faction progress
        var faction = GetUserFaction(args.Examiner);

        using (args.PushGroup(nameof(InteractObjectiveTrackerComponent)))
        {
            // Check if already fully completed for this entity
            if (!string.IsNullOrEmpty(faction))
            {
                var entityCompletions = tracker.CompletionsPerFaction.GetValueOrDefault(faction, 0);
                if (entityCompletions >= interactComp.CompletionsPerEnt)
                {
                    args.PushMarkup("[color=green]This has already been completed.[/color]");
                    return;
                }
            }

            // Show current tool requirement
            var currentInteractions = !string.IsNullOrEmpty(faction)
                ? GetCurrentInteractions(tracker, faction)
                : 0;
            var requiredTool = GetRequiredTool(interactComp, currentInteractions);

            if (requiredTool != null)
            {
                args.PushMarkup($"Use a [color=cyan]{requiredTool}[/color] on this.");
            }
            else
            {
                args.PushMarkup("Use an [color=cyan]empty hand[/color] to interact with this.");
            }

            // Show all tools in the cycle if there are multiple
            if (interactComp.Tools != null && interactComp.Tools.Count > 1)
            {
                var toolList = string.Join(", ", interactComp.Tools.Select(t => $"[color=cyan]{t}[/color]"));
                args.PushMarkup($"Tools needed: {toolList}");
            }

            // Show skill requirements if any
            if (interactComp.Skills.Count > 0)
            {
                var skillList = string.Join(", ", interactComp.Skills.Select(s => $"[color=yellow]{s}[/color]"));
                args.PushMarkup($"Requires skills: {skillList}");
            }

            // Show access requirements if any
            if (interactComp.Access.Count > 0)
            {
                var accessList = string.Join(", ", interactComp.Access.Select(a => $"[color=yellow]{a}[/color]"));
                args.PushMarkup($"Requires access: {accessList}");
            }

            // Show interaction progress if there are multiple interactions needed
            if (interactComp.Interactionsneeded > 1 && !string.IsNullOrEmpty(faction))
            {
                args.PushMarkup($"Progress: [color=white]{currentInteractions}[/color]/[color=white]{interactComp.Interactionsneeded}[/color] interactions.");
            }
        }
    }
}

