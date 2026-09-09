using Content.Shared.CMU14.Medical.Anatomy.Metabolism.Events;
using Content.Shared._RMC14.Chemistry.Reagent;

namespace Content.Shared.CMU14.Yautja;

public sealed partial class YautjaChemResponseSystem : EntitySystem
{
    [Dependency] private RMCReagentSystem _reagent = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaComponent, MetabolismRateModifyEvent>(OnRateModify);
    }

    private void OnRateModify(Entity<YautjaComponent> ent, ref MetabolismRateModifyEvent args)
    {
        if (HasComp<YautjaBadBloodComponent>(ent) || ent.Comp.NativeReagents.Contains(args.Reagent))
            return;

        if (args.ToxicityClasses.Contains(CMUMetabolismClass.Alcohol))
        {
            args.Multiplier *= ent.Comp.AlcoholClearanceMultiplier;
            return;
        }

        if (args.ToxicityClasses.Contains(CMUMetabolismClass.Poison))
        {
            args.Multiplier *= ent.Comp.PoisonClearanceMultiplier;
            return;
        }

        if (_reagent.Index(args.Reagent).Group == "Medicine")
            args.Multiplier *= ent.Comp.HumanMedicineClearanceMultiplier;
    }
}
