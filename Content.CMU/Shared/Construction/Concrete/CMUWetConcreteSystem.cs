using Content.Shared._RMC14.Barricade;
using Content.Shared._RMC14.Barricade.Components;
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
    }

    private void OnInteractUsing(Entity<CMUWetConcreteComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<BarbedWireComponent>(args.Used))
            return;

        args.Handled = true;
        _popup.PopupClient(Loc.GetString("cmu-concrete-wet-no-barbed-wire"), ent, args.User, PopupType.SmallCaution);
    }
}
