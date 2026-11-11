using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.K9;
using Content.Shared._RMC14.K9.Components;
using Content.Shared.Coordinates;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaSleepingHellhoundSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private K9System _k9 = default!;
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

        if (CountPackHounds(user) >= ent.Comp.MaxHoundsPerMaster)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-sleeping-hellhound-pack-full"), ent, user, PopupType.SmallCaution);
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

        if (CountPackHounds(user.Value) >= ent.Comp.MaxHoundsPerMaster)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-sleeping-hellhound-pack-full"), ent, user.Value, PopupType.SmallCaution);
            return;
        }

        var hellhound = Spawn(ent.Comp.SpawnPrototype, ent.Owner.ToCoordinates());
        EnsureComp<YautjaHellhoundComponent>(hellhound);
        // Handler bond: the master gains the command rites and the hound mirrors the owner via K9BondChangedEvent.
        if (TryComp<K9DogComponent>(hellhound, out var k9))
            _k9.BindMaster(hellhound, k9, user.Value, asHandler: true);
        _transform.AttachToGridOrMap(hellhound);

        _audio.PlayPvs(ent.Comp.WakeSound, hellhound);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-sleeping-hellhound-woken", ("hellhound", hellhound)), ent, user.Value);
        QueueDel(ent.Owner);
    }

    private int CountPackHounds(EntityUid master)
    {
        if (!TryComp<K9HandlerComponent>(master, out var handler))
            return 0;

        var count = 0;
        foreach (var dog in handler.Dogs)
        {
            if (HasComp<YautjaHellhoundComponent>(dog)
                && TryComp<MobStateComponent>(dog, out var state)
                && state.CurrentState != MobState.Dead)
                count++;
        }

        return count;
    }
}
