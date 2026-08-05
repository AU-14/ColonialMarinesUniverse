using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Chemistry.Effects;

public sealed partial class ConvertToReagent : RMCChemicalEffect
{
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> TargetReagent;

    [DataField]
    public FixedPoint2 PercentRate = 0.1;

    [DataField]
    public FixedPoint2 MinimumRate = 5;

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Converts to {TargetReagent} at {PercentRate * 100}% or {MinimumRate}u per second while in the body";
    }

    protected override void Apply(RMCChemicalEffectArgs args)
    {
        if (args is not { Source: { } source, Reagent: { } reagent })
            return;

        if (reagent.ID == TargetReagent.Id)
            return;

        if (args.Quantity <= FixedPoint2.Zero)
            return;

        var convertAmount = FixedPoint2.Min(FixedPoint2.Max(args.Quantity * PercentRate, MinimumRate) * args.Scale, args.Quantity);
        if (convertAmount <= FixedPoint2.Zero)
            return;

        source.RemoveReagent(reagent.ID, convertAmount);
        source.AddReagent(TargetReagent, convertAmount);
    }
}
