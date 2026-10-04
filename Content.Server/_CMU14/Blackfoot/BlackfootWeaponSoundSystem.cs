using Content.Server._CMU14.ZLevels.Core;
using Content.Shared._CMU14.Blackfoot;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server._CMU14.Blackfoot;

public sealed partial class BlackfootWeaponSoundSystem : EntitySystem
{
    [Dependency] private CMUZLevelsSystem _zLevels = default!;
    [Dependency] private VehicleTopologySystem _topology = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlackfootGunshotComponent, GunShotEvent>(OnGunShot);
    }

    private void OnGunShot(Entity<BlackfootGunshotComponent> ent, ref GunShotEvent args)
    {
        if (ent.Comp.Sound is not { } sound ||
            !_topology.TryGetVehicle(ent.Owner, out var vehicle, includeSelf: false) ||
            !HasComp<BlackfootFlightComponent>(vehicle))
        {
            return;
        }

        _zLevels.PlayPvsDirectlyAcrossZ(sound, vehicle);
    }
}
