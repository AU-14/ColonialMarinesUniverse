using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared._RMC14.Scoping;
using Robust.Shared.Containers;

namespace Content.Shared._RMC14.Weapons.Ranged.Vulture;

public sealed partial class VultureSpotterTripodSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedScopeSystem _scope = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VultureSpotterTripodComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<VultureSpotterTripodComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<VultureSpotterTripodComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<VultureSpotterTripodComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<VultureSpotterTripodComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnInit(Entity<VultureSpotterTripodComponent> ent, ref ComponentInit args)
    {
        UpdateVisuals(ent);
    }

    private void OnInsertAttempt(Entity<VultureSpotterTripodComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.User is not { } user ||
            args.Slot.ID != ent.Comp.ScopeSlot)
        {
            return;
        }

        ent.Comp.PendingScopeDirection = Transform(user).LocalRotation.GetCardinalDir();
    }

    private void OnContainerChanged(Entity<VultureSpotterTripodComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        OnContainerChanged(ent, args.Container.ID, true);
    }

    private void OnContainerChanged(Entity<VultureSpotterTripodComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        OnContainerChanged(ent, args.Container.ID, false);
    }

    private void OnContainerChanged(Entity<VultureSpotterTripodComponent> ent, string containerId, bool inserted)
    {
        if (containerId != ent.Comp.ScopeSlot)
            return;

        if (inserted && ent.Comp.PendingScopeDirection is { } direction)
            _transform.SetLocalRotation(ent.Owner, direction.ToAngle());

        ent.Comp.PendingScopeDirection = null;
        UpdateVisuals(ent);
    }

    private void OnInteractHand(Entity<VultureSpotterTripodComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled ||
            !TryComp(ent, out ItemSlotsComponent? slots) ||
            !_itemSlots.TryGetSlot(ent.Owner, ent.Comp.ScopeSlot, out var slot, slots) ||
            slot.Item is not { } scopeUid ||
            !TryComp(scopeUid, out ScopeComponent? scope))
        {
            return;
        }

        if (scope.User == args.User)
            _scope.Unscope((scopeUid, scope));
        else
            _scope.StartScoping((scopeUid, scope), args.User);

        args.Handled = true;
    }

    private void UpdateVisuals(Entity<VultureSpotterTripodComponent> ent)
    {
        var hasScope =
            TryComp(ent, out ItemSlotsComponent? slots) &&
            _itemSlots.TryGetSlot(ent.Owner, ent.Comp.ScopeSlot, out var slot, slots) &&
            slot.HasItem;

        _appearance.SetData(ent.Owner, VultureSpotterTripodVisuals.HasScope, hasScope);
    }
}
