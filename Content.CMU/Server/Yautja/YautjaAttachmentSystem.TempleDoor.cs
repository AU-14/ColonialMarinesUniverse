using Content.Shared.CMU14.Yautja.Lodge;
using Content.Shared.Doors.Components;
using Content.Shared.Interaction;
using Content.Shared.Verbs;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaAttachmentSystem
{
    private void InitializeTempleDoor()
    {
        SubscribeLocalEvent<YautjaTempleDoorComponent, InteractUsingEvent>(OnTempleDoorInteractUsing);
        SubscribeLocalEvent<YautjaTempleDoorComponent, GetVerbsEvent<AlternativeVerb>>(OnTempleDoorGetVerbs);
    }

    private void OnTempleDoorInteractUsing(Entity<YautjaTempleDoorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !TryComp(ent, out DoorComponent? door) ||
            !CanUseBracerAttachmentOnDoor(args.User, args.Used))
        {
            return;
        }

        args.Handled = true;
        if (ent.Comp.Locked)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-temple-door-locked"), ent, args.User);
            return;
        }

        switch (door.State)
        {
            case DoorState.Closed:
                _doors.TryOpen(ent, door, args.User);
                break;
            case DoorState.Open:
                _doors.TryClose(ent, door, args.User);
                break;
        }
    }

    private void OnTempleDoorGetVerbs(Entity<YautjaTempleDoorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess ||
            !args.CanInteract ||
            args.Using is not { } used ||
            !CanUseBracerAttachmentOnDoor(args.User, used) ||
            !TryComp(ent, out DoorComponent? door) ||
            door.State is not (DoorState.Open or DoorState.Closed))
        {
            return;
        }

        var user = args.User;
        var locked = ent.Comp.Locked;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(locked ? "cmu-yautja-temple-door-verb-unlock" : "cmu-yautja-temple-door-verb-lock"),
            Act = () => SetTempleDoorLocked(ent, !locked, user),
        });
    }

    private void SetTempleDoorLocked(Entity<YautjaTempleDoorComponent> ent, bool locked, EntityUid user)
    {
        if (ent.Comp.Locked == locked)
            return;

        ent.Comp.Locked = locked;
        Dirty(ent);
        _audio.PlayPvs(ent.Comp.LockSound, ent);
        _popup.PopupEntity(Loc.GetString(locked ? "cmu-yautja-temple-door-lock" : "cmu-yautja-temple-door-unlock"), ent, user);
    }
}
