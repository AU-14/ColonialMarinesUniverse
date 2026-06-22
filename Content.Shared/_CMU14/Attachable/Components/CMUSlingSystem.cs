using Content.Shared._RMC14.Attachable.Events;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Hands;
using Content.Shared.Popups;
using Robust.Shared.Timing;
using Robust.Shared.Containers;

namespace Content.Shared._CMU14.Attachable;

public sealed partial class CMUSlingSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedContainerSystem _container = default!; 
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<
            AttachableSlingComponent,
            AttachableAlteredEvent>(OnAttachableAltered);

        // Instant intercept for manual drops
        SubscribeLocalEvent<
            CMUSlingItemComponent,
            DropAttemptEvent>(OnDropAttempt);

        // Fallback for automated drops (falling over, getting stripped, etc.)
        SubscribeLocalEvent<
            CMUSlingItemComponent,
            DroppedEvent>(OnDropped);
    }

    private void OnAttachableAltered(
        Entity<AttachableSlingComponent> attachable,
        ref AttachableAlteredEvent args)
    {
        if (_timing.ApplyingState)
            return;

        switch (args.Alteration)
        {
            case AttachableAlteredType.Attached:
                EnsureComp<CMUSlingItemComponent>(args.Holder);
                break;

            case AttachableAlteredType.Detached:
                RemCompDeferred<CMUSlingItemComponent>(args.Holder);
                break;
        }
    }

    private void OnDropAttempt(Entity<CMUSlingItemComponent> ent, ref DropAttemptEvent args)
    {
        if (!_container.TryGetContainingContainer(ent.Owner, out var handContainer))
            return;

        var user = handContainer.Owner;

        if (TrySlingItem(user, ent.Owner))
        {
            // Stop the drop completely so it snaps instantly
            args.Cancel();
        }
    }

    private void OnDropped(Entity<CMUSlingItemComponent> ent, ref DroppedEvent args)
    {
        // FIX #1: Guard the timer creation itself against network rollbacks.
        // This instantly cuts the 7x spam down before the timers can even spawn!
        if (!_timing.IsFirstTimePredicted)
            return;

        var user = args.User;
        var item = ent.Owner; 

        Timer.Spawn(0, () =>
        {
            if (!Exists(user) || !Exists(item))
                return;

            TrySlingItem(user, item);
        });
    }

    private bool TrySlingItem(EntityUid user, EntityUid item)
    {
        // FIX #2: Idempotency check. If this item is ALREADY in their suit storage or back,
        // exit immediately. This stops the double-equips and extra popups dead in their tracks.
        if (_inventory.TryGetSlotEntity(user, "suitstorage", out var currentSuit) && currentSuit == item)
            return false;
        if (_inventory.TryGetSlotEntity(user, "back", out var currentBack) && currentBack == item)
            return false;

        // STEP 1: Try suit storage if it's completely empty
        var suitStorageSlots = _inventory.GetSlotEnumerator(user, SlotFlags.SUITSTORAGE);
        while (suitStorageSlots.MoveNext(out var slot))
        {
            if (slot.Count > 0)
                continue;

            if (_inventory.TryEquip(user, item, slot.ID, silent: true, force: true))
            {
                if (_timing.IsFirstTimePredicted)
                {
                    var popup = Loc.GetString("cmu-sling-stored-armor", ("item", item));
                    _popup.PopupClient(popup, user, user, PopupType.Medium);
                }
                return true; 
            }
        }

        // STEP 2: Fallback to Back Slot if it's completely empty
        var backSlots = _inventory.GetSlotEnumerator(user, SlotFlags.BACK);
        while (backSlots.MoveNext(out var slot))
        {
            if (slot.Count > 0)
                continue;

            if (_inventory.TryEquip(user, item, slot.ID, silent: true, force: true))
            {
                if (_timing.IsFirstTimePredicted)
                {
                    var popup = Loc.GetString("cmu-sling-stored-back", ("item", item));
                    _popup.PopupClient(popup, user, user, PopupType.Medium);
                }
                return true; 
            }
        }

        return false;
    }
}