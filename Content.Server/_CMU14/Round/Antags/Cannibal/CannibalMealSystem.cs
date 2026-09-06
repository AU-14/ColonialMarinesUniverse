using Content.Server._CMU14.Round.Antags.Cannibal;
using Content.Server.AU14.Systems;
using Content.Server.Popups;
using Content.Server._CMU14.Round.Antags.ColonyBounty;
using Content.Shared._CMU14.Round.Antags.ColonyBounty;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Server._CMU14.Round.Antags.Cannibal;

/// <summary>
/// Tracks a cannibal's meals: every piece of human meat eaten escalates the CMB response
/// and raises the bounty on them.
/// </summary>
public sealed partial class CannibalMealSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly WantedSystem _wanted = default!;

    private const string HumanMeatPrototype = "FoodMeatHuman";

    public override void Initialize()
    {
        SubscribeLocalEvent<FoodComponent, AfterFullyEatenEvent>(OnFoodEaten);
    }

    private void OnFoodEaten(EntityUid uid, FoodComponent food, ref AfterFullyEatenEvent args)
    {
        if (!TryComp(args.User, out CannibalComponent? cannibal))
            return;

        if (MetaData(uid).EntityPrototype?.ID != HumanMeatPrototype)
            return;

        cannibal.MealsEaten++;
        _popup.PopupEntity(Loc.GetString("cmu-cannibal-meal",
            ("count", cannibal.MealsEaten)), args.User, args.User);

        if (cannibal.MealsEaten == 1)
        {
            var bounty = EnsureComp<ColonyBountyComponent>(args.User);
            bounty.Bounty = 1200;
            bounty.Reason = "Missing colonists - suspected cannibal";
            bounty.RecordName = "The Colony Cannibal (Unknown)";
            bounty.CapturedFaxPaper = "CMUPaperColonyAntagCaptured";
        }
        else
        {
            var bounty = EnsureComp<ColonyBountyComponent>(args.User);
            bounty.Bounty += 800;
        }

        _wanted.SendFaxToGroup(
            ColonyCmbFax.MarshalBureauFaxGroup,
            "Missing Persons Alert",
            ColonyCmbFax.Build("Missing Persons Alert",
                $"This is disappearance number {cannibal.MealsEaten} linked to cannibalism in your colony. " +
                "Find whoever is eating the missing colonists. The bounty has been raised accordingly."),
            "paper_stamp-cmb",
            new List<StampDisplayInfo>
            {
                new() { StampedColor = Color.FromHex("#b0901b"), StampedName = "CMB" },
            }, ColonyCmbFax.CmbPaperPrototype);
    }
}
