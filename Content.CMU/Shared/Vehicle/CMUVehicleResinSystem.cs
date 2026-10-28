using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Popups;
using Content.Shared.Vehicle.Components;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Vehicle;

public sealed partial class CMUVehicleResinSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleResinComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUVehicleResinComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUVehicleResinComponent, CMUVehicleClearResinDoAfterEvent>(OnClear);
    }

    public void TryStick(EntityUid vehicle, CMUVehicleStickyResinComponent resin)
    {
        if (_net.IsClient ||
            TryComp(vehicle, out CMUVehicleResinComponent? existing) && existing.Fouled ||
            !_random.Prob(resin.Chance))
            return;

        var fouled = EnsureComp<CMUVehicleResinComponent>(vehicle);
        fouled.Fouled = true;
        Dirty(vehicle, fouled);
        if (TryComp(vehicle, out VehicleComponent? driven) && driven.Operator is { } driver)
            _popup.PopupCursor(Loc.GetString("cmu-vehicle-resin-stuck"), driver, PopupType.LargeCaution);
    }

    private void OnExamined(Entity<CMUVehicleResinComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Fouled)
            args.PushMarkup(Loc.GetString("cmu-vehicle-resin-examine"));
    }

    private void OnGetVerbs(Entity<CMUVehicleResinComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!ent.Comp.Fouled || !args.CanAccess || !args.CanInteract || !HasComp<HandsComponent>(args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cmu-vehicle-resin-clear"),
            Act = () => _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.ClearTime,
                new CMUVehicleClearResinDoAfterEvent(), ent, target: ent)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
                BlockDuplicate = true,
                DuplicateCondition = DuplicateConditions.SameTarget,
            }),
        });
    }

    private void OnClear(Entity<CMUVehicleResinComponent> ent, ref CMUVehicleClearResinDoAfterEvent args)
    {
        if (_net.IsClient || args.Cancelled || args.Handled || !ent.Comp.Fouled)
            return;

        args.Handled = true;
        ent.Comp.Fouled = false;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("cmu-vehicle-resin-cleared"), ent, args.User);
    }
}
