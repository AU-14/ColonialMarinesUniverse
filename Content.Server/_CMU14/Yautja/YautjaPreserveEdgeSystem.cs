using System.Collections.Generic;
using System.Linq;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Dialog;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Yautja;

public sealed class YautjaPreserveEdgeSystem : EntitySystem
{
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaPreserveEdgeComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<YautjaPreserveEdgeComponent, YautjaPreserveEscapeChoiceEvent>(OnEscapeChoice);
        SubscribeLocalEvent<YautjaPreserveEdgeComponent, YautjaPreserveEscapeDoAfterEvent>(OnEscapeDoAfter);
    }

    private void OnInteractHand(Entity<YautjaPreserveEdgeComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (HasComp<YautjaComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-preserve-edge-yautja-denied"), ent.Owner, args.User, PopupType.SmallCaution);
            return;
        }

        if (HasActiveDoAfter(args.User))
            return;

        var options = new List<DialogOption>
        {
            new(
                Loc.GetString("cmu-yautja-preserve-edge-yes"),
                new YautjaPreserveEscapeChoiceEvent(GetNetEntity(args.User), true)),
            new(
                Loc.GetString("cmu-yautja-preserve-edge-no"),
                new YautjaPreserveEscapeChoiceEvent(GetNetEntity(args.User), false)),
        };

        _dialog.OpenOptions(
            ent.Owner,
            args.User,
            Name(ent.Owner),
            options,
            Loc.GetString("cmu-yautja-preserve-edge-confirm"),
            timeout: ent.Comp.DialogTimeout);
    }

    private void OnEscapeChoice(Entity<YautjaPreserveEdgeComponent> ent, ref YautjaPreserveEscapeChoiceEvent args)
    {
        if (!args.Escape ||
            !TryGetEntity(args.User, out var user) ||
            Deleted(user.Value) ||
            HasComp<YautjaComponent>(user.Value) ||
            HasActiveDoAfter(user.Value))
        {
            return;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            user.Value,
            ent.Comp.EscapeDelay,
            new YautjaPreserveEscapeDoAfterEvent(),
            ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("cmu-yautja-preserve-edge-start"), ent.Owner, user.Value);
    }

    private void OnEscapeDoAfter(Entity<YautjaPreserveEdgeComponent> ent, ref YautjaPreserveEscapeDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-preserve-edge-cancelled"), ent.Owner, args.User, PopupType.SmallCaution);
            return;
        }

        if (Deleted(args.User))
            return;

        _popup.PopupEntity(Loc.GetString("cmu-yautja-preserve-edge-escaped"), ent.Owner, args.User);

        if (_mind.TryGetMind(args.User, out var mindId, out var mind))
            _ghost.SpawnGhost((mindId, mind), args.User);

        // Keep the escaped body out of the active map instead of deleting a player entity.
        // This avoids mutating a client's transform-child collection while it applies the
        // deletion state, which is also safer for connected prey roles.
        _transform.SetParent(args.User, EntityUid.Invalid);
    }

    private bool HasActiveDoAfter(EntityUid user)
    {
        return TryComp(user, out DoAfterComponent? component) &&
               component.DoAfters.Values.Any(active => !active.Cancelled && !active.Completed);
    }
}
