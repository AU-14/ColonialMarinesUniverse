using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Robust.Shared.Containers;

namespace Content.Shared.CMU14.Surrender;

/// <summary>
/// Keeps surrendered hands empty on both the authoritative server and predicting clients.
/// </summary>
public sealed class CMUSurrenderInteractionSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSurrenderComponent, PickupAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<CMUSurrenderComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<CMUSurrenderComponent, IsUnequippingAttemptEvent>(OnUnequipAttempt);
    }

    private void OnPickupAttempt(Entity<CMUSurrenderComponent> ent, ref PickupAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnInsertAttempt(Entity<CMUSurrenderComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        // Inventory/storage transfers may skip the action blocker, but still check the destination container.
        // Other containers (especially the cuff container) must remain usable.
        if (_hands.TryGetHand(ent.Owner, args.Container.ID, out _))
            args.Cancel();
    }

    private void OnUnequipAttempt(Entity<CMUSurrenderComponent> ent, ref IsUnequippingAttemptEvent args)
    {
        // Slot clicks unequip before attempting pickup; cancel here to keep the item in its slot.
        args.Reason = "cmu-surrender-hands-raised";
        args.Cancel();
    }
}
