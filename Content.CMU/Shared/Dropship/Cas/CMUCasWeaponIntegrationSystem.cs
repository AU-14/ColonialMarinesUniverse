using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.CMU14.Dropship.DirectFire;

namespace Content.Shared.CMU14.Dropship.Cas;

/// <summary>Connects optional CAS boot, flight and ammunition policies to RTO AUTO carry-out.</summary>
public sealed class CMUCasWeaponIntegrationSystem : EntitySystem
{
    [Dependency] private DropshipFireControlBootSystem _boot = default!;
    [Dependency] private SharedDropshipWeaponSystem _weapons = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUCasAutoWeaponQueryEvent>(OnWeaponQuery);
    }

    private void OnWeaponQuery(ref CMUCasAutoWeaponQueryEvent args)
    {
        args.Ready &= _boot.IsWeaponReady(args.Weapon);
        if (!TryComp(args.Weapon, out DropshipWeaponComponent? weapon))
        {
            args.Ready = false;
            args.AvailableBursts = 0;
            return;
        }
        args.InFlightMode = _weapons.CanFireInFlightMode(args.Ship, weapon);
        args.AvailableBursts = _weapons.TryGetWeaponAmmo(args.Weapon, out var ammo)
            ? _weapons.GetAvailableBursts(ammo)
            : 0;
    }
}
