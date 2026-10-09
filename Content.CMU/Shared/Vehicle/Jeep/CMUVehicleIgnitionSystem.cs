using Content.Shared.Buckle.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// The ignition key slot of <see cref="CMUVehicleIgnitionComponent"/>: without a key in it the vehicle
/// can't run, so its engine won't start and stops when the key comes out.
/// </summary>
public sealed class CMUVehicleIgnitionSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleIgnitionComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<CMUVehicleIgnitionComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<CMUVehicleIgnitionComponent, VehicleCanRunEvent>(OnCanRun);
        SubscribeLocalEvent<CMUVehicleIgnitionComponent, ItemSlotEjectAttemptEvent>(OnEjectAttempt);
        SubscribeLocalEvent<CMUVehicleIgnitionComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>
    /// The key slot, on client and server alike. A kit's chassis gets it empty.
    /// </summary>
    private void OnInit(Entity<CMUVehicleIgnitionComponent> ent, ref ComponentInit args)
    {
        if (!ent.Comp.StartWithKey)
            ent.Comp.KeySlot.StartingItem = null;

        _itemSlots.AddItemSlot(ent.Owner, CMUVehicleIgnitionComponent.SlotId, ent.Comp.KeySlot);
    }

    private void OnRemove(Entity<CMUVehicleIgnitionComponent> ent, ref ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(ent.Owner, ent.Comp.KeySlot);
    }

    private void OnCanRun(Entity<CMUVehicleIgnitionComponent> ent, ref VehicleCanRunEvent args)
    {
        if (!ent.Comp.KeySlot.HasItem)
            args.CanRun = false;
    }

    /// <summary>
    /// While someone drives, only those aboard can reach the key.
    /// </summary>
    private void OnEjectAttempt(Entity<CMUVehicleIgnitionComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Cancelled ||
            args.Slot != ent.Comp.KeySlot ||
            args.User is not { } user ||
            !TryComp(ent, out VehicleComponent? vehicle) ||
            vehicle.Operator == null ||
            IsAboard(user, ent))
        {
            return;
        }

        args.Cancelled = true;
    }

    private void OnExamined(Entity<CMUVehicleIgnitionComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString(ent.Comp.KeySlot.HasItem ? "cmu-vehicle-ignition-examine-key" : "cmu-vehicle-ignition-examine-no-key"));
    }

    private bool IsAboard(EntityUid user, EntityUid vehicle)
    {
        return TryComp(user, out BuckleComponent? buckle) &&
               TryComp(buckle.BuckledTo, out CMUVehicleSeatComponent? seat) &&
               seat.Vehicle == vehicle;
    }
}
