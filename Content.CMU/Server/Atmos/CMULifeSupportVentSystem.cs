using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.CMU14.Atmos;

namespace Content.Server.CMU14.Atmos;

public sealed class CMULifeSupportVentSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMULifeSupportVentComponent, AtmosDeviceUpdateEvent>(OnUpdate);
    }

    private void OnUpdate(Entity<CMULifeSupportVentComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        var vent = ent.Comp;
        var transform = Transform(ent);
        var environment = _atmosphere.GetContainingMixture((ent, transform), true, true);

        if (environment == null || environment.Immutable)
            return;

        var oxygenShare = vent.OxygenFraction;
        var targetMoles = vent.TargetPressure * environment.Volume / (Atmospherics.T20C * Atmospherics.R);
        var cap = vent.TransferRate * args.dt;

        for (var gas = 0; gas < Atmospherics.TotalNumberOfGases; gas++)
        {
            var target = gas == (int) Gas.Oxygen
                ? targetMoles * oxygenShare
                : gas == (int) Gas.Nitrogen
                    ? targetMoles * (1f - oxygenShare)
                    : 0f;

            var excess = environment.GetMoles(gas) - target;
            if (excess > Atmospherics.GasMinMoles)
                environment.AdjustMoles(gas, -MathF.Min(excess, cap));
            else if (excess < -Atmospherics.GasMinMoles)
                environment.AdjustMoles(gas, MathF.Min(-excess, cap));
        }
    }
}
