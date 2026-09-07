using System.Linq;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Chemistry.Effects;

public sealed partial class RMCAlchemistPurgeNonToxins : EntityEffectBase<RMCAlchemistPurgeNonToxins>
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

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Purges [color=red]{Amount}[/color] units of matching non-toxin chemicals per second.";
    }
}

public sealed partial class RMCAlchemistPurgeNonToxinsEntityEffectSystem
    : EntityEffectSystem<MetaDataComponent, RMCAlchemistPurgeNonToxins>
{
    [Dependency] private readonly RMCReagentSystem _reagent = default!;

    protected override void Effect(
        Entity<MetaDataComponent> entity,
        ref EntityEffectEvent<RMCAlchemistPurgeNonToxins> args)
    {
        if (args.ReagentContext is not { Source: { } source })
            return;

        var effect = args.Effect;
        var amount = effect.Amount * args.Scale;
        if (amount <= FixedPoint2.Zero)
            return;

        foreach (var quantity in source.Contents.ToArray())
        {
            if (!_reagent.TryIndex(quantity.Reagent, out var reagent) ||
                reagent.Toxin ||
                !effect.Groups.Contains(reagent.Group))
            {
                continue;
            }

            source.RemoveReagent(quantity.Reagent, amount);
        }
    }
}
