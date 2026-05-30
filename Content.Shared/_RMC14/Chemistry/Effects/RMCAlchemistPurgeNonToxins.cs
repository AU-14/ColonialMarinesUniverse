using System.Linq;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Chemistry.Effects;

public sealed partial class RMCAlchemistPurgeNonToxins : EntityEffect
{
    [DataField]
    public FixedPoint2 Amount = 0.2f;

    [DataField]
    public HashSet<string> Groups = new()
    {
        "Medicine",
        "Generated",
        "Stimulant",
        "Stimulants",
    };

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs { Source: { } source } reagentArgs)
            return;

        var amount = Amount * reagentArgs.Scale;
        if (amount <= FixedPoint2.Zero)
            return;

        var reagents = args.EntityManager.System<RMCReagentSystem>();
        foreach (var quantity in source.Contents.ToArray())
        {
            if (!reagents.TryIndex(quantity.Reagent, out var reagent) ||
                reagent.Toxin ||
                !Groups.Contains(reagent.Group))
            {
                continue;
            }

            source.RemoveReagent(quantity.Reagent, amount);
        }
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Purges [color=red]{Amount}[/color] units of matching non-toxin chemicals per second.";
    }
}
