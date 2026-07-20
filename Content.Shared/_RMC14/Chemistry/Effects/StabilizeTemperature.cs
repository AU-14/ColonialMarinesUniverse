using Content.Shared._RMC14.Temperature;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Chemistry.Effects;

public sealed partial class StabilizeTemperature : RMCChemicalEffect
{
    [DataField(required: true)]
    public float Stable;

    [DataField(required: true)]
    public float Change;

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Stabilizes the temperature of the body that it is in to {Stable} degrees, by {Change} degrees at a time";
    }

    protected override void Apply(RMCChemicalEffectArgs args)
    {
        var sys = args.EntityManager.EntitySysManager.GetEntitySystem<SharedRMCTemperatureSystem>();
        var current = sys.GetTemperature(args.TargetEntity);
        if (Math.Abs(current - Stable) < 0.01)
            return;

        var change = args.Reagent == null ? Change : Change * args.Scale;

        var temp = current > Stable
            ? Math.Max(Stable, current - change)
            : Math.Min(Stable, current + change);

        sys.ForceChangeTemperature(args.TargetEntity, temp);
    }
}
