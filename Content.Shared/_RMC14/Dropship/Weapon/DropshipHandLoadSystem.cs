using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.PowerLoader;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Dropship.Weapon;

/// <summary>
/// Handles hand-loading and unloading of <see cref="DropshipHandLoadedAmmoComponent"/>
/// ammo (40mm BOFORS magazines, 105mm howitzer shells) into the ammo container of the
/// attach point that the matching weapon is installed on. Works for both weapon and
/// utility (crew compartment) attach points.
/// </summary>
public sealed class DropshipHandLoadSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly PowerLoaderSystem _powerLoader = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DropshipWeaponPointComponent, InteractUsingEvent>(OnWeaponPointInteractUsing);
        SubscribeLocalEvent<DropshipUtilityPointComponent, InteractUsingEvent>(OnUtilityPointInteractUsing);
    }

    private void OnWeaponPointInteractUsing(Entity<DropshipWeaponPointComponent> ent, ref InteractUsingEvent args)
    {
        OnInteractUsing(ent, ref args);
    }

    private void OnUtilityPointInteractUsing(Entity<DropshipUtilityPointComponent> ent, ref InteractUsingEvent args)
    {
        OnInteractUsing(ent, ref args);
    }

    private void OnInteractUsing(EntityUid point, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<DropshipHandLoadedAmmoComponent>(args.Used) ||
            !TryComp(args.Used, out DropshipAmmoComponent? ammo))
        {
            return;
        }

        if (!TryGetInstalledWeapon(point, out var weapon))
            return;

        args.Handled = true;

        if (ammo.Weapon.Id != Prototype(weapon)?.ID)
        {
            _popup.PopupClient(Loc.GetString("rmc-power-loader-wrong-weapon"), point, args.User, PopupType.SmallCaution);
            return;
        }

        var slot = GetAmmoSlot(point);
        if (slot == null)
            return;

        if (slot.ContainedEntity != null)
        {
            _popup.PopupClient(Loc.GetString("rmc-dropship-hand-load-occupied", ("weapon", weapon)), point, args.User, PopupType.SmallCaution);
            return;
        }

        if (_net.IsClient)
            return;

        _container.Insert(args.Used, slot);

        if (TryComp(point, out DropshipWeaponPointComponent? weaponPoint))
            _powerLoader.SyncAppearance((point, weaponPoint));
    }

    /// <summary>
    ///     Tries to unload hand-loaded ammo from the attach point's ammo container
    ///     into the user's hands. Returns true if ammo was unloaded.
    ///     Called from the existing InteractHand handlers, since directed subscriptions
    ///     must be unique per component/event pair.
    /// </summary>
    public bool TryHandUnload(EntityUid point, EntityUid user)
    {
        var slot = GetAmmoSlot(point);
        if (slot?.ContainedEntity is not { } contained)
            return false;

        if (!HasComp<DropshipHandLoadedAmmoComponent>(contained))
            return false;

        if (!_net.IsClient)
        {
            _container.TryRemoveFromContainer(contained);
            _hands.TryPickupAnyHand(user, contained);

            if (TryComp(point, out DropshipWeaponPointComponent? weaponPoint))
                _powerLoader.SyncAppearance((point, weaponPoint));
        }

        return true;
    }

    /// <summary>
    /// Gets the weapon installed on an attach point, whether it is mounted on a
    /// weapon point or a utility (crew compartment) point.
    /// </summary>
    private bool TryGetInstalledWeapon(EntityUid point, out EntityUid weapon)
    {
        weapon = default;

        string? weaponSlotId = null;
        if (TryComp(point, out DropshipWeaponPointComponent? weaponPoint))
            weaponSlotId = weaponPoint.WeaponContainerSlotId;
        else if (TryComp(point, out DropshipUtilityPointComponent? utilityPoint))
            weaponSlotId = utilityPoint.UtilitySlotId;

        if (weaponSlotId == null ||
            !_container.TryGetContainer(point, weaponSlotId, out var container))
        {
            return false;
        }

        foreach (var contained in container.ContainedEntities)
        {
            if (!HasComp<DropshipWeaponComponent>(contained))
                continue;

            weapon = contained;
            return true;
        }

        return false;
    }

    private ContainerSlot? GetAmmoSlot(EntityUid point)
    {
        string? ammoSlotId = null;
        if (TryComp(point, out DropshipWeaponPointComponent? weaponPoint))
            ammoSlotId = weaponPoint.AmmoContainerSlotId;
        else if (TryComp(point, out DropshipUtilityPointComponent? utilityPoint))
            ammoSlotId = utilityPoint.AmmoContainerSlotId;

        if (ammoSlotId == null)
            return null;

        return _container.EnsureContainer<ContainerSlot>(point, ammoSlotId);
    }
}
