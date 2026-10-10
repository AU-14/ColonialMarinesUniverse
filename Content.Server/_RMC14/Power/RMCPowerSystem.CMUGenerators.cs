using Content.Server.Power.Components;
using Content.Shared._RMC14.Power;
using Content.Shared.CMU14.Power;
using Robust.Shared.GameObjects;

namespace Content.Server._RMC14.Power;

// CMU14 Class: dual-mode stationary generators carry an RMCFusionReactor (RMC area power)
// and a PowerSupplier (wizden grid) on one entity. CMUMapUsesTilePowerComponent on the map
// decides the live channel, never the prototype: area-power maps keep the grid side dead,
// tile-power maps run it gated by reactor repair state. Suppliers ship disabled in yaml
// and only this system enables them, so a broken generator can never feed the grid.
public sealed partial class RMCPowerSystem
{
    private void UpdateCMUGenerators()
    {
        var query = EntityQueryEnumerator<RMCFusionReactorComponent, PowerSupplierComponent, TransformComponent>();
        while (query.MoveNext(out _, out var reactor, out var supplier, out var xform))
        {
            if (xform.MapUid is not { } map || !HasComp<CMUMapUsesTilePowerComponent>(map))
            {
                // The two systems never mix: even a mapper-authored enabled supplier
                // stays inert on an area-power map.
                if (supplier.Enabled)
                    supplier.Enabled = false;

                continue;
            }

            var enabled = reactor.State == RMCFusionReactorState.Working;
            if (supplier.Enabled != enabled)
                supplier.Enabled = enabled;
        }
    }
}
