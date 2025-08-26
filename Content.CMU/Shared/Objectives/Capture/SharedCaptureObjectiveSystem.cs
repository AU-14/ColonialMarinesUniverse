using System.Linq;
using Content.Shared._RMC14.Dropship;
using Content.Shared.AU14.Objectives.Capture;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.NPC.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Player;
using Content.Shared.Interaction;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;

namespace Content.Shared.AU14.Objectives.Capture;

public sealed class SharedCaptureObjectiveSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly Content.Shared._RMC14.Dropship.SharedDropshipSystem _dropshipSystem = default!;
    private static readonly ISawmill Sawmill = Logger.GetSawmill("capture-obj");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CaptureObjectiveComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnInteractHand(EntityUid uid, CaptureObjectiveComponent comp, InteractHandEvent args)
    {
        if (args.Handled)
            return;
        // Only allow interaction if the flag is not busy (ActionState == Idle)
        if (comp.ActionState != CaptureObjectiveComponent.FlagActionState.Idle)
        {
            args.Handled = true;
            return;
        }
        if (!_entManager.TryGetComponent<NpcFactionMemberComponent>(args.User, out var npcFaction) || npcFaction.Factions.Count == 0)
        {
            args.Handled = true;
            return;
        }
        var userFactions = npcFaction.Factions.Select(f => f.ToString().ToLowerInvariant()).ToList();
        // If the flag is currently raised, allow anyone to lower it
        if (!string.IsNullOrEmpty(comp.CurrentController))
        {
            args.Handled = true;
            // Allow lowering for anyone
            var startedEvent = new FlagHoistStartedEvent(args.User, comp.CurrentController);
            RaiseLocalEvent(uid, startedEvent);
            var doAfterArgs = new DoAfterArgs(_entManager, args.User, comp.HoistTime, new HoistFlagDoAfterEvent { Faction = comp.CurrentController }, uid)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true
            };
            _doAfter.TryStartDoAfter(doAfterArgs);
            return;
        }
        // If the flag is lowered, only allow raising for allowed factions
        string? allowed = null;
        foreach (var fac in new[] { "govfor", "opfor", "clf" })
        {
            if (userFactions.Contains(fac))
            {
                allowed = fac;
                break;
            }
        }
        if (allowed == null)
        {
            args.Handled = true;
            // Optionally, show a popup here: "Your faction cannot raise this flag."
            return;
        }
        args.Handled = true;
        var startedRaiseEvent = new FlagHoistStartedEvent(args.User, allowed);
        RaiseLocalEvent(uid, startedRaiseEvent);
        var doAfterRaiseArgs = new DoAfterArgs(_entManager, args.User, comp.HoistTime, new HoistFlagDoAfterEvent { Faction = allowed }, uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };
        _doAfter.TryStartDoAfter(doAfterRaiseArgs);
    }
}
