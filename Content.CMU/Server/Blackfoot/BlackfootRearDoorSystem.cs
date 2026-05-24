using Content.Shared._CMU14.Blackfoot;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Server._CMU14.Blackfoot;

public sealed partial class BlackfootRearDoorSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private VehicleSystem _vehicle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlackfootRearDoorControlComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<BlackfootRearDoorComponent, VehicleEntryAttemptEvent>(OnEntryAttempt);
        SubscribeLocalEvent<BlackfootRearDoorComponent, VehicleExitAttemptEvent>(OnExitAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BlackfootRearDoorVisualsComponent>();
        while (query.MoveNext(out var uid, out var visuals))
        {
            if (!_vehicle.TryGetVehicleFromInterior(uid, out var vehicle) ||
                vehicle is not { } vehicleUid ||
                !TryComp(vehicleUid, out BlackfootRearDoorComponent? rearDoor) ||
                visuals.Open == rearDoor.Open)
            {
                continue;
            }

            visuals.Open = rearDoor.Open;
            Dirty(uid, visuals);
        }
    }

    private void OnActivate(Entity<BlackfootRearDoorControlComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (!_vehicle.TryGetVehicleFromInterior(ent.Owner, out var vehicle) ||
            vehicle is not { } vehicleUid ||
            !TryComp(vehicleUid, out BlackfootRearDoorComponent? rearDoor))
        {
            _popup.PopupEntity("This control is not linked to a Blackfoot rear door.", args.User, args.User, PopupType.SmallCaution);
            return;
        }

        rearDoor.Open = !rearDoor.Open;
        Dirty(vehicleUid, rearDoor);

        _popup.PopupEntity(rearDoor.Open ? "Rear door opened." : "Rear door closed.", args.User, args.User);
        args.Handled = true;
    }

    private void OnEntryAttempt(Entity<BlackfootRearDoorComponent> ent, ref VehicleEntryAttemptEvent args)
    {
        if (ent.Comp.Open || args.EntryIndex != ent.Comp.RearEntryIndex)
            return;

        _popup.PopupEntity("Open the rear door before boarding from the back.", args.User, args.User, PopupType.SmallCaution);
        args.Cancelled = true;
    }

    private void OnExitAttempt(Entity<BlackfootRearDoorComponent> ent, ref VehicleExitAttemptEvent args)
    {
        if (ent.Comp.Open || !HasComp<BlackfootRearDoorVisualsComponent>(args.Exit))
            return;

        _popup.PopupEntity("Open the rear door before exiting from the back.", args.User, args.User, PopupType.SmallCaution);
        args.Cancelled = true;
    }
}
