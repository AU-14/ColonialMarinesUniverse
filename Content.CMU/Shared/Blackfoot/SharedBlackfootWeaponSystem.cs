using System;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Blackfoot;

public sealed partial class SharedBlackfootWeaponSystem : EntitySystem
{
    private const string DoorGunHardpointType = "DoorGun";
    private const string LauncherHardpointType = "Launcher";

    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private VehicleTopologySystem _topology = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, ShotAttemptedEvent>(OnGunShotAttempted);
    }

    private void OnGunShotAttempted(Entity<GunComponent> ent, ref ShotAttemptedEvent args)
    {
        if (_net.IsClient)
            return;

        if (!_topology.TryGetVehicle(ent.Owner, out var vehicle, includeSelf: false) ||
            !TryComp(vehicle, out BlackfootFlightComponent? flight) ||
            !_topology.TryGetMountedSlotByItem(vehicle, ent.Owner, out var mountedSlot))
        {
            return;
        }

        if (CanFireMountedWeapon(vehicle, flight, mountedSlot.HardpointType, out var reason))
            return;

        args.Cancel();
        Popup(args.User, vehicle, reason);
    }

    private bool CanFireMountedWeapon(
        EntityUid vehicle,
        BlackfootFlightComponent flight,
        string hardpointType,
        out string reason)
    {
        if (!CanFireInState(flight.State))
        {
            reason = Loc.GetString(flight.State switch
            {
                BlackfootFlightState.Stowed => "cmu-blackfoot-weapon-stowed",
                BlackfootFlightState.TakingOff => "cmu-blackfoot-weapon-taking-off",
                BlackfootFlightState.Landing => "cmu-blackfoot-weapon-landing",
                BlackfootFlightState.Crashed => "cmu-blackfoot-weapon-crashed",
                _ => "cmu-blackfoot-weapon-invalid-state",
            });
            return false;
        }

        if (TryComp(vehicle, out BlackfootStealthComponent? stealth) &&
            stealth.Enabled &&
            stealth.DisableWeapons)
        {
            reason = Loc.GetString("cmu-blackfoot-weapon-stealth");
            return false;
        }

        if (IsDoorGun(hardpointType) &&
            (!TryComp(vehicle, out BlackfootRearDoorComponent? rearDoor) || !rearDoor.Open))
        {
            reason = Loc.GetString("cmu-blackfoot-weapon-rear-door");
            return false;
        }

        if (IsLauncher(hardpointType) && !IsAirborneWeaponState(flight.State))
        {
            reason = Loc.GetString("cmu-blackfoot-weapon-launcher-airborne");
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool CanFireInState(BlackfootFlightState state)
    {
        return state is
            BlackfootFlightState.Grounded or
            BlackfootFlightState.Idling or
            BlackfootFlightState.VTOL or
            BlackfootFlightState.Flight;
    }

    private static bool IsDoorGun(string hardpointType)
    {
        return string.Equals(hardpointType, DoorGunHardpointType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLauncher(string hardpointType)
    {
        return string.Equals(hardpointType, LauncherHardpointType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAirborneWeaponState(BlackfootFlightState state)
    {
        return state is BlackfootFlightState.VTOL or BlackfootFlightState.Flight;
    }

    private void Popup(EntityUid user, EntityUid fallback, string message)
    {
        var target = Exists(user) ? user : fallback;
        _popup.PopupEntity(message, target, target, PopupType.SmallCaution);
    }
}
