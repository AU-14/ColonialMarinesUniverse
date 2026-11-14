using Content.Shared._RMC14.Doors;
using Content.Shared._RMC14.Tools;
using Content.Shared.CMU14.Doors;
using Content.Shared.Construction;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Prying.Systems;
using Content.Shared.Verbs;

namespace Content.Server.CMU14.Doors;

public sealed partial class CMUConstructedDoorControlSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUConstructedDoorControlComponent, ConstructionCompletedEvent>(OnBuilt);
        SubscribeLocalEvent<CMUConstructedDoorControlComponent, InteractUsingEvent>(OnInteract,
            before: [typeof(PryingSystem)]);
        SubscribeLocalEvent<CMUConstructedDoorControlComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUConstructedDoorControlComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnBuilt(Entity<CMUConstructedDoorControlComponent> ent, ref ConstructionCompletedEvent args)
    {
        ent.Comp.Builder = args.Builder;
    }

    private void OnGetVerbs(Entity<CMUConstructedDoorControlComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Builder != args.User)
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(ent.Comp.LinksLocked ? "cmu-door-links-unlock" : "cmu-door-links-lock"),
            Act = () => ToggleLock(ent, user),
        });
    }

    private void ToggleLock(Entity<CMUConstructedDoorControlComponent> ent, EntityUid user)
    {
        if (TerminatingOrDeleted(ent) || ent.Comp.Builder != user)
            return;

        ent.Comp.LinksLocked = !ent.Comp.LinksLocked;
        _popup.PopupEntity(Loc.GetString(ent.Comp.LinksLocked ? "cmu-door-links-locked" : "cmu-door-links-unlocked"),
            ent, user);
    }

    private void OnInteract(Entity<CMUConstructedDoorControlComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<MultitoolComponent>(args.Used))
            return;

        args.Handled = true;
        if (!CanChange(ent.Comp, args.User))
        {
            _popup.PopupEntity(Loc.GetString("cmu-door-links-protected"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (HasComp<RMCDoorButtonComponent>(ent))
        {
            var tool = EnsureComp<CMUDoorLinkToolComponent>(args.Used);
            tool.Button = ent;
            tool.User = args.User;
            _popup.PopupEntity(Loc.GetString("cmu-door-links-selected"), ent, args.User);
            return;
        }

        if (!HasComp<RMCPodDoorComponent>(ent))
            return;

        if (!TryComp<CMUDoorLinkToolComponent>(args.Used, out var selection) ||
            selection.User != args.User ||
            selection.Button is not { } button ||
            TerminatingOrDeleted(button) ||
            !TryComp<CMUConstructedDoorControlComponent>(button, out var buttonControl) ||
            !HasComp<RMCDoorButtonComponent>(button))
        {
            _popup.PopupEntity(Loc.GetString("cmu-door-links-select-first"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (!CanChange(buttonControl, args.User))
        {
            _popup.PopupEntity(Loc.GetString("cmu-door-links-protected"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (Transform(button).MapID != Transform(ent).MapID)
        {
            _popup.PopupEntity(Loc.GetString("cmu-door-links-different-map"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        var linked = buttonControl.LinkedDoors.Add(ent);
        if (!linked)
            buttonControl.LinkedDoors.Remove(ent);

        _popup.PopupEntity(Loc.GetString(linked ? "cmu-door-links-linked" : "cmu-door-links-unlinked"), ent, args.User);
    }

    private static bool CanChange(CMUConstructedDoorControlComponent control, EntityUid user)
        => !control.LinksLocked || control.Builder == user;

    private void OnTerminating(Entity<CMUConstructedDoorControlComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!HasComp<RMCPodDoorComponent>(ent))
            return;

        var controls = EntityQueryEnumerator<CMUConstructedDoorControlComponent, RMCDoorButtonComponent>();
        while (controls.MoveNext(out _, out var control, out _))
            control.LinkedDoors.Remove(ent);
    }
}
