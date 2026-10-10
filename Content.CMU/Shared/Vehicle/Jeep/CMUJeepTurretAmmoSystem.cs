using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged.Ammo.BulletBox;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Loads a jeep's mounted gun from an ammo box used on its turret, or anywhere on the jeep: the box's
/// own transfer, as if the gun were used on the box.
/// </summary>
public sealed class CMUJeepTurretAmmoSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private VehicleTurretSystem _turret = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleTurretVisualComponent, InteractUsingEvent>(OnTurretInteractUsing);
        SubscribeLocalEvent<CMUJeepComponent, InteractUsingEvent>(OnJeepInteractUsing);
    }

    private void OnTurretInteractUsing(Entity<VehicleTurretVisualComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !HasComp<BulletBoxComponent>(args.Used) ||
            !TryGetEntity(ent.Comp.Turret, out var turret) ||
            !_turret.TryGetVehicle(turret.Value, out var vehicle) ||
            !HasComp<CMUJeepComponent>(vehicle))
        {
            return;
        }

        args.Handled = TryLoad(turret.Value, args.User, args.Used);
    }

    private void OnJeepInteractUsing(Entity<CMUJeepComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<BulletBoxComponent>(args.Used))
            return;

        args.Handled = TryLoad(ent, args.User, args.Used);
    }

    /// <summary>
    /// Starts loading the first gun fitted on or in <paramref name="owner"/> from the box, if it takes
    /// the box's rounds and has room. Says why not if it can't.
    /// </summary>
    private bool TryLoad(EntityUid owner, EntityUid user, EntityUid used)
    {
        if (!TryComp(used, out BulletBoxComponent? box) || !TryFindGun(owner, out var gun, out var refill, out var ammo))
            return false;

        string? why = null;
        if (box.BulletType != refill.BulletType)
            why = "rmc-bullet-box-wrong-rounds";
        else if (ammo.Count >= ammo.Capacity)
            why = "rmc-bullet-box-mag-full";
        else if (box.Amount <= 0)
            why = "rmc-bullet-box-box-empty";

        if (why != null)
        {
            _popup.PopupClient(Loc.GetString(why), owner, user);
            return true;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, box.Delay, new BulletBoxTransferDoAfterEvent(false), used, used, gun)
        {
            BreakOnMove = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
        return true;
    }

    private bool TryFindGun(
        EntityUid owner,
        out EntityUid gun,
        [NotNullWhen(true)] out RefillableByBulletBoxComponent? refill,
        [NotNullWhen(true)] out BallisticAmmoProviderComponent? ammo)
    {
        gun = owner;
        if (TryComp(owner, out refill) && TryComp(owner, out ammo))
            return true;

        // An entity without containers has no ContainerManager, and its container enumerator throws.
        if (!TryComp(owner, out ContainerManagerComponent? containers))
        {
            refill = null;
            ammo = null;
            return false;
        }

        foreach (var container in _container.GetAllContainers(owner, containers))
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (TryFindGun(contained, out gun, out refill, out ammo))
                    return true;
            }
        }

        refill = null;
        ammo = null;
        return false;
    }
}
