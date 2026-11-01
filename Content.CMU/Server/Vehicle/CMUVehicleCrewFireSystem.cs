using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Atmos.Components;
using Content.Shared.CMU14.Blackfoot;

namespace Content.Server.CMU14.Vehicle;

public sealed partial class CMUVehicleCrewFireSystem : EntitySystem
{
    [Dependency] private SharedRMCFlammableSystem _flammable = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FlammableComponent, CMUVehicleOccupantEjectedEvent>(OnEjected);
    }

    private void OnEjected(Entity<FlammableComponent> ent, ref CMUVehicleOccupantEjectedEvent args)
    {
        if (!args.CookOff || !TryGetCrewFire(args.Vehicle, out var intensity, out var duration) || duration <= 0)
            return;

        _flammable.Ignite(ent.AsNullable(), intensity, duration, duration);
    }

    private bool TryGetCrewFire(EntityUid vehicle, out int intensity, out int duration)
    {
        if (TryComp(vehicle, out TankCookOffComponent? tank))
        {
            intensity = tank.CrewFireIntensity;
            duration = tank.CrewFireDuration;
            return true;
        }

        if (TryComp(vehicle, out CMUBlackfootDestructionComponent? blackfoot))
        {
            intensity = blackfoot.CrewFireIntensity;
            duration = blackfoot.CrewFireDuration;
            return true;
        }

        intensity = 0;
        duration = 0;
        return false;
    }
}
