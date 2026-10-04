using System.Linq;
using Content.Shared._CMU14.Blackfoot;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Rejuvenate;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;

namespace Content.Server._CMU14.Blackfoot;

public sealed partial class VehicleRejuvenateSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private HardpointSystem _hardpoint = default!;
    [Dependency] private VehicleTopologySystem _topology = default!;
    [Dependency] private VehicleHardpointAmmoSystem _ammo = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VehicleComponent, RejuvenateEvent>(OnRejuvenateVehicle);
    }

    private void OnRejuvenateVehicle(EntityUid uid, VehicleComponent component, RejuvenateEvent args)
    {
        if (TryComp<DamageableComponent>(uid, out var damageable))
            _damageable.SetDamage(uid, damageable, new DamageSpecifier());

        _hardpoint.ResetAllHardpointsToFullHealth(uid); // heal hardpoints & recalc integrity
        _hardpoint.ClearAllFailures(uid); // bulk resets failures, repair/progress + refresh UI

        if (TryComp<BlackfootFuelPowerComponent>(uid, out var fuelPower))
        {
            fuelPower.Fuel = fuelPower.MaxFuel;
            fuelPower.Battery = fuelPower.MaxBattery;
            Dirty(uid, fuelPower);
        }

        RearmAllHardpoints(uid);
    }

    private void RearmAllHardpoints(EntityUid vehicle)
    {
        if (!TryComp<HardpointSlotsComponent>(vehicle, out var hardpoints)
            || !TryComp<ItemSlotsComponent>(vehicle, out var itemSlots))
            return;

        foreach (var mounted in _topology.GetMountedSlots(vehicle, hardpoints, itemSlots))
        {
            if (mounted.Item is not { } item)
                continue;

            if (!TryComp<VehicleHardpointAmmoComponent>(item, out var hardpointAmmo)
                    || !TryComp<BallisticAmmoProviderComponent>(item, out var ammoProvider))
                continue;

            if (_container.TryGetContainer(item, ammoProvider.Container.ID, out var container))
                foreach (var ent in container.ContainedEntities.ToArray())
                    Del(ent); // clear ammo
            _gun.SetBallisticUnspawned((item, ammoProvider), ammoProvider.Capacity); // refill ammo
            Dirty(item, ammoProvider);

            var magazineSize = _ammo.GetMagazineSize(hardpointAmmo, ammoProvider);
            var maxStored = _ammo.GetMaxStoredRounds(hardpointAmmo, magazineSize);
            _ammo.SetStoredRounds((item, hardpointAmmo), maxStored, magazineSize);
        }
    }
}
