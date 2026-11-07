using System.Numerics;
using Content.Shared.Buckle.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Popups;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Network;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class VehicleWeaponsSystem
{
    private void OnVehicleArtilleryFire(Entity<VehicleWeaponsSeatComponent> ent, ref VehicleArtilleryFireMessage args)
    {
        if (_net.IsClient || args.Actor == default || !Exists(args.Actor))
            return;

        if (!TryComp(args.Actor, out BuckleComponent? buckle) ||
            buckle.BuckledTo != ent.Owner ||
            !CMUTryGetSeatVehicle(ent.Owner, out var vehicle) ||
            vehicle is not { } vehicleUid ||
            !CanUseHardpointActions(args.Actor, forUi: true) ||
            !TryComp(vehicleUid, out VehicleWeaponsComponent? weapons))
            return;

        if (!TryComp(args.Actor, out VehicleViewToggleComponent? viewToggle) || !viewToggle.IsOutside)
        {
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-artillery-outside-view-required"), args.Actor, args.Actor, PopupType.SmallCaution);
            return;
        }

        if (!weapons.OperatorSelections.TryGetValue(args.Actor, out var selectedWeapon) ||
            !HasComp<VehicleArtilleryWeaponComponent>(selectedWeapon) ||
            !TryComp(selectedWeapon, out GunComponent? gun))
        {
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-artillery-select-weapon"), args.Actor, args.Actor, PopupType.SmallCaution);
            return;
        }

        if (!_hardpoints.IsHardpointFunctional(selectedWeapon))
        {
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-artillery-weapon-damaged"), args.Actor, args.Actor, PopupType.SmallCaution);
            return;
        }

        if (!_guns.CanShoot(gun))
        {
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-artillery-weapon-not-ready"), args.Actor, args.Actor, PopupType.SmallCaution);
            return;
        }

        HardpointSlotsComponent? hardpoints = null;
        ItemSlotsComponent? itemSlots = null;
        if (!TryGetArtilleryModule(vehicleUid, hardpoints, itemSlots, out var artilleryModule))
        {
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-artillery-module-missing"), args.Actor, args.Actor, PopupType.SmallCaution);
            return;
        }

        if (!_turretSystem.TryGetTurretOrigin(selectedWeapon, out var origin))
        {
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-artillery-no-origin"), args.Actor, args.Actor, PopupType.SmallCaution);
            return;
        }

        var originMap = _transform.ToMapCoordinates(origin);
        var targetMap = new MapCoordinates(new Vector2(args.X + 0.5f, args.Y + 0.5f), originMap.MapId);
        var range = Vector2.Distance(originMap.Position, targetMap.Position);
        if (range > artilleryModule.MaxRange)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-vehicle-artillery-out-of-range", ("range", artilleryModule.MaxRange)),
                args.Actor,
                args.Actor,
                PopupType.SmallCaution);
            return;
        }

        var target = _transform.ToCoordinates(selectedWeapon, targetMap);
        var fired = _guns.AttemptShoot((selectedWeapon, gun), args.Actor, target);
        if (fired == null || fired.Count == 0)
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-artillery-fire-failed"), args.Actor, args.Actor, PopupType.SmallCaution);
    }

    private bool TryGetArtilleryModule(
        EntityUid vehicle,
        HardpointSlotsComponent? hardpoints,
        ItemSlotsComponent? itemSlots,
        out VehicleArtilleryModuleComponent artilleryModule)
    {
        artilleryModule = default!;
        if (!Resolve(vehicle, ref hardpoints, logMissing: false) ||
            !Resolve(vehicle, ref itemSlots, logMissing: false))
        {
            return false;
        }

        foreach (var slot in _topology.GetMountedSlots(vehicle, hardpoints, itemSlots))
        {
            if (slot.Item is not { } item ||
                !TryComp(item, out VehicleArtilleryModuleComponent? module) ||
                !_hardpoints.IsHardpointFunctional(item))
            {
                continue;
            }

            artilleryModule = module;
            return true;
        }

        return false;
    }
}
