using Content.Shared.Examine;
using Content.Shared._RMC14.PowerLoader;
using Content.Shared.Popups;
using Robust.Shared.Containers;

namespace Content.Shared._RMC14.Dropship.Weapon;

public abstract partial class SharedDropshipWeaponSystem
{
    private const string GauWeapon = "RMCDropshipAttachmentGau21Cannon";

    private void InitializeMixedBelts()
    {
        SubscribeLocalEvent<DropshipMixedBeltComponent, ContainerIsInsertingAttemptEvent>(OnBeltInsertAttempt);
        SubscribeLocalEvent<DropshipMixedBeltComponent, EntInsertedIntoContainerMessage>(OnBeltInserted);
        SubscribeLocalEvent<DropshipMixedBeltComponent, EntRemovedFromContainerMessage>(OnBeltRemoved);
        SubscribeLocalEvent<DropshipMixedBeltComponent, ExaminedEvent>(OnBeltExamined);
    }

    private void OnBeltInsertAttempt(Entity<DropshipMixedBeltComponent> belt,
        ref ContainerIsInsertingAttemptEvent args)
    {
        if (!TryComp(args.EntityUid, out DropshipAmmoComponent? ammo) || ammo.Weapon.Id != GauWeapon ||
            HasComp<DropshipMixedBeltComponent>(args.EntityUid))
            args.Cancel();
    }

    private void OnBeltInserted(Entity<DropshipMixedBeltComponent> belt, ref EntInsertedIntoContainerMessage args)
    {
        if (_net.IsServer)
            SyncBelt(belt);
    }

    private void OnBeltRemoved(Entity<DropshipMixedBeltComponent> belt, ref EntRemovedFromContainerMessage args)
    {
        if (_net.IsServer)
            SyncBelt(belt);
    }

    private void OnBeltExamined(Entity<DropshipMixedBeltComponent> belt, ref ExaminedEvent args)
    {
        for (var i = 0; i < DropshipMixedBeltComponent.Slots.Length; i++)
        {
            var contents = _container.TryGetContainer(belt, DropshipMixedBeltComponent.Slots[i], out var slot) &&
                           slot.ContainedEntities.Count > 0
                ? Name(slot.ContainedEntities[0]) : Loc.GetString("cmu-dropship-belt-empty");
            args.PushText(Loc.GetString("cmu-dropship-belt-slot", ("slot", i + 1), ("ammo", contents)));
        }
    }

    private bool TryLoadMixedBelt(Entity<DropshipAmmoComponent> crate, ref PowerLoaderInteractEvent args)
    {
        if (!TryComp(args.Target, out DropshipMixedBeltComponent? belt))
            return false;

        args.Handled = true;
        if (crate.Comp.Weapon.Id != GauWeapon || HasComp<DropshipMixedBeltComponent>(crate) ||
            crate.Comp.Rounds < crate.Comp.RoundsPerShot)
        {
            foreach (var actor in args.Buckled)
                _popup.PopupEntity(Loc.GetString("cmu-dropship-belt-wrong-ammo"), args.Target, actor, PopupType.SmallCaution);
            return true;
        }

        foreach (var id in DropshipMixedBeltComponent.Slots)
        {
            var slot = _container.EnsureContainer<ContainerSlot>(args.Target, id);
            if (slot.ContainedEntity != null)
                continue;
            if (_container.Insert(crate.Owner, slot))
            {
                _powerloader.TrySyncHands(args.PowerLoader);
                foreach (var actor in args.Buckled)
                    _popup.PopupEntity(Loc.GetString("cmu-dropship-belt-loaded"), args.Target, actor);
            }
            else
            {
                foreach (var actor in args.Buckled)
                    _popup.PopupEntity(Loc.GetString("cmu-dropship-belt-wrong-ammo"), args.Target, actor, PopupType.SmallCaution);
            }
            return true;
        }

        foreach (var actor in args.Buckled)
            _popup.PopupEntity(Loc.GetString("cmu-dropship-belt-full"), args.Target, actor, PopupType.SmallCaution);
        return true;
    }

    public bool TryGetBeltAmmo(EntityUid uid, out Entity<DropshipAmmoComponent> ammo)
    {
        ammo = default;
        if (!TryComp(uid, out DropshipMixedBeltComponent? belt))
            return false;
        for (var offset = 0; offset < DropshipMixedBeltComponent.Slots.Length; offset++)
        {
            var index = (belt.NextSlot + offset) % DropshipMixedBeltComponent.Slots.Length;
            if (!_container.TryGetContainer(uid, DropshipMixedBeltComponent.Slots[index], out var slot))
                continue;
            foreach (var crate in slot.ContainedEntities)
            {
                if (!TryComp(crate, out DropshipAmmoComponent? payload) ||
                    payload.Rounds < payload.RoundsPerShot || payload.Weapon.Id != GauWeapon)
                    continue;
                ammo = (crate, payload);
                return true;
            }
        }
        return false;
    }

    private void SyncBelt(Entity<DropshipMixedBeltComponent> belt)
    {
        if (!TryComp(belt, out DropshipAmmoComponent? ammo))
            return;
        var rounds = 0;
        foreach (var id in DropshipMixedBeltComponent.Slots)
        {
            if (!_container.TryGetContainer(belt, id, out var slot))
                continue;
            foreach (var crate in slot.ContainedEntities)
            {
                if (TryComp(crate, out DropshipAmmoComponent? payload))
                    rounds += payload.Rounds;
            }
        }
        ammo.Rounds = rounds;
        Dirty(belt, ammo);
        _powerloader.SyncAppearance(Transform(belt).ParentUid);
    }

    private void AdvanceMixedBelt(EntityUid crate)
    {
        if (!_container.TryGetContainingContainer((crate, null), out var slot) ||
            !TryComp(slot.Owner, out DropshipMixedBeltComponent? belt))
            return;
        var index = Array.IndexOf(DropshipMixedBeltComponent.Slots, slot.ID);
        belt.NextSlot = (index + 1) % DropshipMixedBeltComponent.Slots.Length;
        Dirty(slot.Owner, belt);
        SyncBelt((slot.Owner, belt));
    }

    private void RefreshMixedBeltSummary(EntityUid crate)
    {
        if (_net.IsServer && _container.TryGetContainingContainer((crate, null), out var slot) &&
            TryComp(slot.Owner, out DropshipMixedBeltComponent? belt))
            SyncBelt((slot.Owner, belt));
    }

    public int GetAvailableBursts(Entity<DropshipAmmoComponent> ammo)
    {
        if (!_container.TryGetContainingContainer((ammo.Owner, null), out var container) ||
            !HasComp<DropshipMixedBeltComponent>(container.Owner))
            return ammo.Comp.Rounds / Math.Max(1, ammo.Comp.RoundsPerShot);
        var count = 0;
        foreach (var id in DropshipMixedBeltComponent.Slots)
        {
            if (!_container.TryGetContainer(container.Owner, id, out var slot))
                continue;
            foreach (var crate in slot.ContainedEntities)
            {
                if (TryComp(crate, out DropshipAmmoComponent? payload))
                    count += payload.Rounds / Math.Max(1, payload.RoundsPerShot);
            }
        }
        return count;
    }

    public Entity<DropshipAmmoComponent> GetLoadedAmmoSummary(Entity<DropshipAmmoComponent> ammo)
    {
        if (_container.TryGetContainingContainer((ammo.Owner, null), out var container) &&
            HasComp<DropshipMixedBeltComponent>(container.Owner) &&
            TryComp(container.Owner, out DropshipAmmoComponent? belt))
            return (container.Owner, belt);
        return ammo;
    }
}
