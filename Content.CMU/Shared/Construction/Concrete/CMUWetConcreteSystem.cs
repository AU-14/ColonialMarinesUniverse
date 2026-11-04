using Content.Shared._RMC14.Barricade;
using Content.Shared._RMC14.Barricade.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Shared.CMU14.Construction.Concrete;

public sealed partial class CMUWetConcreteSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUWetConcreteComponent, InteractUsingEvent>(OnInteractUsing, before: [typeof(SharedBarbedSystem)]);
        SubscribeLocalEvent<CMUWetConcreteComponent, DoAfterAttemptEvent<BarbedDoAfterEvent>>(OnBarbedDoAfterAttempt);
    }

    // Backstop: never let a wiring do-after on wet concrete finish, however it was started.
    private void OnBarbedDoAfterAttempt(Entity<CMUWetConcreteComponent> ent, ref DoAfterAttemptEvent<BarbedDoAfterEvent> args)
    {
        args.Cancel();
    }

    private void OnInteractUsing(Entity<CMUWetConcreteComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<BarbedWireComponent>(args.Used))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("cmu-concrete-wet-no-barbed-wire"), ent, args.User, PopupType.SmallCaution);
    }
}
