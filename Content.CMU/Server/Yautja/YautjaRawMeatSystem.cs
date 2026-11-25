using Content.Shared.CMU14.Yautja;
using Content.Shared.Nutrition;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaRawMeatSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaComponent, IngestingEvent>(OnIngesting);
    }

    private void OnIngesting(Entity<YautjaComponent> ent, ref IngestingEvent args)
    {
        // the bite hasn't reached the stomach yet, so whatever the split holds is what gets metabolized
        foreach (var ignored in ent.Comp.IgnoredFoodReagents)
        {
            var amount = args.Split.GetTotalPrototypeQuantity(ignored);
            if (amount > 0)
                args.Split.RemoveReagent(ignored, amount);
        }

        var raw = args.Split.GetTotalPrototypeQuantity(ent.Comp.RawMeatReagent);
        if (raw <= 0)
            return;

        args.Split.RemoveReagent(ent.Comp.RawMeatReagent, raw);
        args.Split.AddReagent(ent.Comp.RawMeatDigestsAs, raw);
    }
}
