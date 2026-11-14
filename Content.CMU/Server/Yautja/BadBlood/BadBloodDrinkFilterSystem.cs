using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Fluids.Components;
using Content.Shared.Nutrition;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja.BadBlood;

// drinks get past the stomach whitelist, so this is where a Bad Blood is held to water
public sealed partial class BadBloodDrinkFilterSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly ProtoId<ReagentPrototype> Water = "Water";

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBadBloodComponent, IngestingEvent>(OnIngesting);
    }

    private void OnIngesting(Entity<YautjaBadBloodComponent> ent, ref IngestingEvent args)
    {
        // cooked prey and pills were already let through by the stomach. only the things it can't judge are filtered
        if (!HasComp<DrainableSolutionComponent>(args.Food) && !HasComp<PuddleComponent>(args.Food))
            return;

        var spat = false;
        for (var i = args.Split.Contents.Count - 1; i >= 0; i--)
        {
            var reagent = args.Split.Contents[i];
            if (reagent.Reagent.Prototype == Water)
                continue;

            args.Split.RemoveReagent(reagent);
            spat = true;
        }

        if (spat)
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-drink-refused"), ent, ent, PopupType.SmallCaution);
    }
}
