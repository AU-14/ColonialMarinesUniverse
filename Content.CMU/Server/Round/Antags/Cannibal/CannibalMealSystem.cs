using Content.Server.CMU14.Round.Antags.ColonyBounty;
using Content.Server.CMU14.Systems;
using Content.Server.Damage.Systems;
using Content.Server.Popups;
using Content.Shared.CMU14.Round.Antags.Cannibal;
using Content.Shared.CMU14.Round.Antags.ColonyBounty;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Round.Antags.Cannibal;

/// <summary>
/// Tracks a cannibal's meals: every piece of human meat eaten escalates the CMB response
/// and raises the bounty on them.
/// </summary>
public sealed partial class CannibalMealSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly WantedSystem _wanted = default!;
    [Dependency] private readonly ColonyBountySystem _colonyBounty = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private const string HumanMeatPrototype = "FoodMeatHuman";
    private const string HumanOrganPrefix = "OrganHuman";
    private const float BadFoodStaminaDamage = 15f;

    private static readonly ProtoId<TagPrototype> MeatTag = "Meat";
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    public override void Initialize()
    {
        SubscribeLocalEvent<MetaDataComponent, FullyEatenEvent>(OnFoodEaten);
    }

    private void OnFoodEaten(Entity<MetaDataComponent> food, ref FullyEatenEvent args)
    {
        if (!TryComp(args.User, out CannibalComponent? cannibal))
            return;

        // Drinks and pills stay untouched or they die of thirst with their meds.
        if (!TryComp<EdibleComponent>(food, out var edible)
            || edible.Edible != IngestionSystem.Food)
            return;

        if (_tag.HasTag(food, MeatTag))
        {
            var heal = new DamageSpecifier(_proto.Index<DamageGroupPrototype>(BruteGroup), -2)
                + new DamageSpecifier(_proto.Index<DamageGroupPrototype>(BurnGroup), -1);
            _damageable.TryChangeDamage(args.User, heal, true);

            // Only human stock escalates the CMB response.
            if (MetaData(food).EntityPrototype is not { } proto
                || proto.ID != HumanMeatPrototype && !proto.ID.StartsWith(HumanOrganPrefix))
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
                _colonyBounty.SyncRecordBounty(args.User, bounty);
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
            return;
        }

        _stamina.TakeStaminaDamage(args.User, BadFoodStaminaDamage);
        _popup.PopupEntity(Loc.GetString("cmu-cannibal-diet", ("food", food)), args.User, args.User);
    }
}
