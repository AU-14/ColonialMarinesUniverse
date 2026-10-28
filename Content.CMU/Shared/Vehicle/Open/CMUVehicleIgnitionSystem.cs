using Content.Shared.Buckle.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// The ignition key slot of <see cref="CMUVehicleIgnitionComponent"/>: without a key in it the vehicle
/// can't run, so its engine won't start and stops when the key comes out. Each vehicle's key is cut
/// for it alone.
/// </summary>
public sealed class CMUVehicleIgnitionSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;

    // Key codes are cut in this range; 0 is a blank.
    private const int MinCode = 1000;
    private const int MaxCode = 10000;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleIgnitionComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<CMUVehicleIgnitionComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<CMUVehicleIgnitionComponent, MapInitEvent>(OnMapInit, after: [typeof(ItemSlotsSystem)]);
        SubscribeLocalEvent<CMUVehicleIgnitionComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<CMUVehicleIgnitionComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<CMUVehicleKeyComponent, ExaminedEvent>(OnKeyExamined);
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

    /// <summary>
    /// A vehicle that comes with its key cuts it with a new code of its own.
    /// </summary>
    private void OnMapInit(Entity<CMUVehicleIgnitionComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || ent.Comp.KeyCode != 0 || ent.Comp.KeySlot.Item is not { } key)
            return;

        Pair(ent, key, _random.Next(MinCode, MaxCode));
    }

    /// <summary>
    /// Only the vehicle's own key goes in; before it has one, any key does (see <see cref="OnInserted"/>).
    /// </summary>
    private void OnInsertAttempt(Entity<CMUVehicleIgnitionComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || args.Slot != ent.Comp.KeySlot || ent.Comp.KeyCode == 0)
            return;

        if (CompOrNull<CMUVehicleKeyComponent>(args.Item)?.Code == ent.Comp.KeyCode)
            return;

        args.Cancelled = true;
        if (args.User is { } user)
            _popup.PopupClient(Loc.GetString("cmu-vehicle-ignition-wrong-key", ("vehicle", ent.Owner)), ent, user);
    }

    /// <summary>
    /// The first key into a vehicle without one becomes its key: a blank is cut with a new code, a cut
    /// key's code becomes the vehicle's.
    /// </summary>
    private void OnInserted(Entity<CMUVehicleIgnitionComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (_net.IsClient || args.Container.ID != CMUVehicleIgnitionComponent.SlotId || ent.Comp.KeyCode != 0)
            return;

        var code = CompOrNull<CMUVehicleKeyComponent>(args.Entity)?.Code ?? 0;
        Pair(ent, args.Entity, code != 0 ? code : _random.Next(MinCode, MaxCode));
    }

    private void OnKeyExamined(Entity<CMUVehicleKeyComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(ent.Comp.Code == 0
            ? Loc.GetString("cmu-vehicle-key-examine-blank")
            : Loc.GetString("cmu-vehicle-key-examine-code", ("code", ent.Comp.Code)));
    }

    private void Pair(Entity<CMUVehicleIgnitionComponent> ent, EntityUid key, int code)
    {
        ent.Comp.KeyCode = code;
        Dirty(ent);
        var keyComp = EnsureComp<CMUVehicleKeyComponent>(key);
        keyComp.Code = code;
        Dirty(key, keyComp);
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
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.KeySlot.HasItem ? "cmu-vehicle-ignition-examine-key" : "cmu-vehicle-ignition-examine-no-key"));
        if (ent.Comp.KeyCode != 0)
            args.PushMarkup(Loc.GetString("cmu-vehicle-ignition-examine-code", ("code", ent.Comp.KeyCode)));
    }

    private bool IsAboard(EntityUid user, EntityUid vehicle)
    {
        return TryComp(user, out BuckleComponent? buckle) &&
               TryComp(buckle.BuckledTo, out CMUVehicleSeatComponent? seat) &&
               seat.Vehicle == vehicle;
    }
}
