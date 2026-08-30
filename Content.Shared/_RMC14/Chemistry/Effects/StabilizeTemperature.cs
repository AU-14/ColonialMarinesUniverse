using Content.Shared._RMC14.Temperature;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Chemistry.Effects;

public sealed partial class StabilizeTemperature : EntityEffectBase<StabilizeTemperature>
{
    [DataField(required: true)]
    public float Stable;

    [DataField(required: true)]
    public float Change;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Stabilizes the temperature of the body that it is in to {Stable} degrees, by {Change} degrees at a time";
    }

}

public sealed partial class StabilizeTemperatureEntityEffectSystem
    : EntityEffectSystem<MetaDataComponent, StabilizeTemperature>
{
    [Dependency] private readonly SharedRMCTemperatureSystem _temperature = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<StabilizeTemperature> args)
    {
        var current = _temperature.GetTemperature(entity);
        if (Math.Abs(current - args.Effect.Stable) < 0.01)
            return;

        var change = args.Effect.Change;
        if (args.ReagentContext != null)
            change *= args.Scale;

        var temp = current > args.Effect.Stable
            ? Math.Max(args.Effect.Stable, current - change)
            : Math.Min(args.Effect.Stable, current + change);

        _temperature.ForceChangeTemperature(entity, temp);
    }
}
