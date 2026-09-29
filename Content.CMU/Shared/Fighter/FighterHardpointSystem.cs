using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.PowerLoader;
using Content.Shared._RMC14.PowerLoader.Events;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Fighter;

/// <summary>Uses the existing loader workflow, with direct missile mounts and a fixed cannon feed.</summary>
public sealed partial class FighterHardpointSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private PowerLoaderSystem _loader = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FighterHardpointComponent, GetAttachmentSlotEvent>(OnGetSlot);
        SubscribeLocalEvent<FighterHardpointComponent, DropshipAttachDoAfterEvent>(OnAttach);
        SubscribeLocalEvent<FighterHardpointComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<FighterHardpointComponent, DropshipDetachDoAfterEvent>(OnDetach);
        SubscribeLocalEvent<FighterHardpointComponent, ContainerIsInsertingAttemptEvent>(OnInserting);
        SubscribeLocalEvent<FighterHardpointComponent, ContainerIsRemovingAttemptEvent>(OnRemoving);
        SubscribeLocalEvent<FighterHardpointComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<FighterHardpointComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<FighterHardpointComponent, ExaminedEvent>(OnExamined);
    }

    private bool CanService(FighterHardpointComponent point) =>
        TryComp(point.Aircraft, out FighterAircraftComponent? flight) && !flight.Flying && flight.Phase == FighterPhase.Holding &&
        (flight.GroundEntity == null || flight.GroundState == FighterGroundState.Grounded);

    public FighterWeaponKind Kind(Entity<FighterHardpointComponent> point)
    {
        if (point.Comp.Internal) return FighterWeaponKind.Gau;
        if (point.Comp.Equipment is { } equipment && !TerminatingOrDeleted(equipment)) return FighterWeaponKind.Rockets;
        return point.Comp.Ammo is { } ammo && !TerminatingOrDeleted(ammo) ? FighterWeaponKind.Missile : FighterWeaponKind.Empty;
    }

    private bool Compatible(Entity<FighterHardpointComponent> point, EntityUid item, string slot)
    {
        if (slot == FighterHardpointComponent.EquipmentContainer)
            return !point.Comp.Internal && point.Comp.Ammo == null && MetaData(item).EntityPrototype?.ID == "RMCDropshipAttachmentRocketPod";
        if (slot != FighterHardpointComponent.AmmoContainer || !TryComp(item, out DropshipAmmoComponent? ammo))
            return false;
        var weapon = point.Comp.Internal ? "RMCDropshipAttachmentGau21Cannon"
            : point.Comp.Equipment != null ? "RMCDropshipAttachmentRocketPod" : "RMCDropshipAttachmentGuidedMissileLauncher";
        return ammo.Weapon.Id == weapon && ammo.Rounds >= ammo.RoundsPerShot && ammo.RoundsPerShot > 0;
    }

    public bool TryMount(Entity<FighterHardpointComponent> point, EntityUid item)
    {
        if (_net.IsClient || !CanService(point.Comp)) return false;
        var slot = HasComp<DropshipAmmoComponent>(item) ? FighterHardpointComponent.AmmoContainer : FighterHardpointComponent.EquipmentContainer;
        return Compatible(point, item, slot) && _containers.Insert(item, _containers.EnsureContainer<ContainerSlot>(point, slot));
    }

    private void OnGetSlot(Entity<FighterHardpointComponent> point, ref GetAttachmentSlotEvent ev)
    {
        if (ev.Used is not { } used || !TryGetEntity(used, out var item) || item == null) return;
        ev.SlotId = HasComp<DropshipAmmoComponent>(item) ? FighterHardpointComponent.AmmoContainer : FighterHardpointComponent.EquipmentContainer;
        var container = _containers.EnsureContainer<ContainerSlot>(point, ev.SlotId);
        ev.CanUse = CanService(point.Comp) && container.ContainedEntity == null && Compatible(point, item.Value, ev.SlotId);
        if (!ev.CanUse)
            _popup.PopupEntity(Loc.GetString("cmu-fighter-mount-rejected"), point, GetEntity(ev.User));
    }

    private void OnAttach(Entity<FighterHardpointComponent> point, ref DropshipAttachDoAfterEvent ev)
    {
        if (ev.Cancelled || ev.Handled || _net.IsClient || GetEntity(ev.Container) != point.Owner) return;
        ev.Handled = true;
        if (TryMount(point, GetEntity(ev.Contained)))
            _loader.TrySyncHands(ev.User);
    }

    private void OnActivate(Entity<FighterHardpointComponent> point, ref ActivateInWorldEvent ev)
    {
        if (ev.Handled || !CanService(point.Comp) || !_loader.TryGetActivePowerLoader(ev.User, out var loader)) return;
        var item = point.Comp.Ammo ?? point.Comp.Equipment;
        if (item == null || !_loader.CanPickupWithActiveHand(ev.User)) return;
        var slot = point.Comp.Ammo != null ? FighterHardpointComponent.AmmoContainer : FighterHardpointComponent.EquipmentContainer;
        ev.Handled = true;
        var after = new DropshipDetachDoAfterEvent(GetNetEntity(point), GetNetEntity(item.Value), slot);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ev.User, TimeSpan.FromSeconds(5), after, point, point)
        {
            BreakOnMove = true,
            DistanceThreshold = 2.5f,
        });
    }

    private void OnDetach(Entity<FighterHardpointComponent> point, ref DropshipDetachDoAfterEvent ev)
    {
        if (ev.Cancelled || ev.Handled || _net.IsClient || !CanService(point.Comp) || GetEntity(ev.Container) != point.Owner) return;
        ev.Handled = true;
        if (!_containers.TryGetContainer(point, ev.Slot, out var slot) || !slot.Contains(GetEntity(ev.Contained)) ||
            !_loader.CanPickupWithActiveHand(ev.User)) return;
        if (_containers.Remove(GetEntity(ev.Contained), slot))
            _loader.TryPickupWithActiveHand(ev.User, GetEntity(ev.Contained));
    }

    private void OnInserting(Entity<FighterHardpointComponent> point, ref ContainerIsInsertingAttemptEvent ev)
    {
        if (!CanService(point.Comp) || !Compatible(point, ev.EntityUid, ev.Container.ID)) ev.Cancel();
    }

    private void OnRemoving(Entity<FighterHardpointComponent> point, ref ContainerIsRemovingAttemptEvent ev)
    {
        if (!TerminatingOrDeleted(point) && !TerminatingOrDeleted(ev.EntityUid) &&
            (!CanService(point.Comp) || ev.Container.ID == FighterHardpointComponent.EquipmentContainer && point.Comp.Ammo != null)) ev.Cancel();
    }

    private void OnInserted(Entity<FighterHardpointComponent> point, ref EntInsertedIntoContainerMessage ev) => Refresh(point);
    private void OnRemoved(Entity<FighterHardpointComponent> point, ref EntRemovedFromContainerMessage ev) => Refresh(point);

    private void Refresh(Entity<FighterHardpointComponent> point)
    {
        if (_net.IsClient || TerminatingOrDeleted(point)) return;
        point.Comp.Equipment = _containers.TryGetContainer(point, FighterHardpointComponent.EquipmentContainer, out var equipment)
            && equipment.ContainedEntities.Count > 0 ? equipment.ContainedEntities[0] : null;
        point.Comp.Ammo = _containers.TryGetContainer(point, FighterHardpointComponent.AmmoContainer, out var ammo)
            && ammo.ContainedEntities.Count > 0 ? ammo.ContainedEntities[0] : null;
        Dirty(point);
        if (TryComp(point.Comp.Aircraft, out FighterWeaponsComponent? weapons)) weapons.NextRefresh = TimeSpan.Zero;
    }

    private void OnExamined(Entity<FighterHardpointComponent> point, ref ExaminedEvent ev)
    {
        ev.PushText(Loc.GetString(point.Comp.Internal ? "cmu-fighter-gau-feed" : "cmu-fighter-pylon-examine", ("number", point.Comp.Index + 1)));
        if (point.Comp.Equipment is { } equipment) ev.PushText(Loc.GetString("rmc-dropship-attached", ("attachment", equipment)));
        if (point.Comp.Ammo is { } ammo) ev.PushText(Loc.GetString("rmc-dropship-attached", ("attachment", ammo)));
    }
}
