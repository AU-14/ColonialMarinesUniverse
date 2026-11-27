using Content.Shared._RMC14.Ghost;
using Content.Shared._RMC14.Vehicle;

namespace Content.CMU.Shared.Vehicle;

public sealed class CMUVehicleGhostExitSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleExitComponent, MapInitEvent>(OnExitMapInit);
    }

    // interior maps add VehicleExit straight onto wall/door pieces, so those exits never got the
    // ghost interaction bypass the prototype exit doors have, and ghosts that came in couldn't leave
    private void OnExitMapInit(Entity<VehicleExitComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<RMCIgnoreGhostInteractionLimitsComponent>(ent);
    }
}
