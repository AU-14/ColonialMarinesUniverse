using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Dialog;
using Content.Shared.Coordinates;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaSleepingHellhoundSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaSleepingHellhoundComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<YautjaSleepingHellhoundComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<YautjaSleepingHellhoundComponent, YautjaSleepingHellhoundConfirmEvent>(OnWakeConfirmed);
    }

    private void OnInteractHand(Entity<YautjaSleepingHellhoundComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryOpenWakeDialog(ent, args.User);
    }

    private void OnActivateInWorld(Entity<YautjaSleepingHellhoundComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryOpenWakeDialog(ent, args.User);
    }

    private void TryOpenWakeDialog(Entity<YautjaSleepingHellhoundComponent> ent, EntityUid user)
    {
        if (!HasComp<YautjaComponent>(user) && !HasComp<YautjaTechAuthorizedComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-sleeping-hellhound-denied"), ent, user, PopupType.SmallCaution);
            return;
        }

        _dialog.OpenConfirmation(
            ent,
            user,
            Loc.GetString("cmu-yautja-sleeping-hellhound-confirm-title"),
            Loc.GetString("cmu-yautja-sleeping-hellhound-confirm-message"),
            new YautjaSleepingHellhoundConfirmEvent(GetNetEntity(user)));
    }

    private void OnWakeConfirmed(Entity<YautjaSleepingHellhoundComponent> ent, ref YautjaSleepingHellhoundConfirmEvent args)
    {
        if (TerminatingOrDeleted(ent) ||
            !TryGetEntity(args.User, out var user) ||
            Deleted(user.Value))
        {
            return;
        }

        if (!HasComp<YautjaComponent>(user) && !HasComp<YautjaTechAuthorizedComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-sleeping-hellhound-denied"), ent, user.Value, PopupType.SmallCaution);
            return;
        }

        var hellhound = Spawn(ent.Comp.SpawnPrototype, ent.Owner.ToCoordinates());
        EnsureComp<YautjaHellhoundComponent>(hellhound).YautjaOwner = user;
        _transform.AttachToGridOrMap(hellhound);

        _audio.PlayPvs(ent.Comp.WakeSound, hellhound);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-sleeping-hellhound-woken", ("hellhound", hellhound)), ent, user.Value);
        QueueDel(ent.Owner);
    }
}
