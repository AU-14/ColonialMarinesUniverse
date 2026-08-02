using System.Linq;
using Content.Shared._CMU14.Round.Objectives.Type;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.NPC.Components;

namespace Content.Shared._CMU14.Round.Objectives;

public sealed partial class SharedObjectiveCaptureSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CaptureObjectiveComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnInteractHand(EntityUid uid, CaptureObjectiveComponent comp, InteractHandEvent args)
    {
        if (args.Handled)
            return;
        if (comp.ActionState != CaptureObjectiveComponent.FlagActionState.Idle)
        {
            args.Handled = true;
            return;
        }
        if (!TryComp<NpcFactionMemberComponent>(args.User, out var npcFaction) || npcFaction.Factions.Count == 0)
        {
            args.Handled = true;
            return;
        }
        var userFactions = npcFaction.Factions.Select(f => f.ToString().ToLowerInvariant()).ToList();
        if (!string.IsNullOrEmpty(comp.CurrentController))
        {
            args.Handled = true;
            var startedEvent = new CaptureHoistFlagStartedEvent(args.User, comp.CurrentController);
            RaiseLocalEvent(uid, startedEvent);
            var doAfterArgs = new DoAfterArgs(EntityManager, args.User, comp.HoistTime, new CaptureHoistFlagDoAfterEvent { Faction = comp.CurrentController }, uid)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true
            };
            _doAfter.TryStartDoAfter(doAfterArgs);
            return;
        }
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
            return;
        }
        args.Handled = true;
        var startedRaiseEvent = new CaptureHoistFlagStartedEvent(args.User, allowed);
        RaiseLocalEvent(uid, startedRaiseEvent);
        var doAfterRaiseArgs = new DoAfterArgs(EntityManager, args.User, comp.HoistTime, new CaptureHoistFlagDoAfterEvent { Faction = allowed }, uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };
        _doAfter.TryStartDoAfter(doAfterRaiseArgs);
    }
}
