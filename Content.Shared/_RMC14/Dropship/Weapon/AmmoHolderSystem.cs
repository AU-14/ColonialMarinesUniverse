using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Dropship.Weapon;

public sealed class AmmoHolderSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AmmoHolderComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<AmmoHolderComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AmmoHolderComponent, EntInsertedIntoContainerMessage>(OnContainerModified);
        SubscribeLocalEvent<AmmoHolderComponent, EntRemovedFromContainerMessage>(OnContainerModified);
    }

    private void OnInit(EntityUid uid, AmmoHolderComponent component, ComponentInit args)
    {
        UpdateAmmoFill(uid);
    }

    private void OnMapInit(EntityUid uid, AmmoHolderComponent component, MapInitEvent args)
    {
        UpdateAmmoFill(uid);
    }

    private void OnContainerModified(EntityUid uid, AmmoHolderComponent component, ref EntInsertedIntoContainerMessage args)
    {
        UpdateAmmoFill(uid);
    }

    private void OnContainerModified(EntityUid uid, AmmoHolderComponent component, ref EntRemovedFromContainerMessage args)
    {
        UpdateAmmoFill(uid);
    }

    private void UpdateAmmoFill(EntityUid uid)
    {
        if (!TryComp<DropshipAmmoComponent>(uid, out var ammo))
            return;

        if (!TryComp<ItemSlotsComponent>(uid, out var slots))
            return;

        var count = 0;
        foreach (var slot in slots.Slots.Values)
        {
            if (slot.HasItem)
                count++;
        }

        ammo.Rounds = count;
        _appearance.SetData(uid, DropshipAmmoVisuals.Fill, count);
        Dirty(uid, ammo);
    }
}
