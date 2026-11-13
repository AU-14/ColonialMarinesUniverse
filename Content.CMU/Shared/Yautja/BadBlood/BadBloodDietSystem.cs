using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Whitelist;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// the Bad Blood stomach only takes cooked prey and pills, but a food with a drainable solution (MRE cans, spreads,
// tubes, condiment packets) is "universally digestible" and skips the stomach entirely. the drink filter then
// strips everything but water, so the Bad Blood ate it for nothing. refuse those foods before the first bite instead.
// drinks stay allowed: they are held to water by BadBloodDrinkFilterSystem
public sealed partial class BadBloodDietSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        // only drainable foods get past the stomach (EdibleComponent/EdibleEvent belongs to IngestionSystem, and
        // every other food is already refused by the Bad Blood stomach). after IngestionSystem has found the solution,
        // so a refusal is not reported as an empty item
        SubscribeLocalEvent<DrainableSolutionComponent, EdibleEvent>(OnDrainableEdible, after: [typeof(IngestionSystem)]);
    }

    private void OnDrainableEdible(Entity<DrainableSolutionComponent> ent, ref EdibleEvent args)
    {
        if (args.Cancelled ||
            !TryComp(ent, out EdibleComponent? edible) ||
            edible.Edible != IngestionSystem.Food ||
            !CannotEat(args.Target, ent))
        {
            return;
        }

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-food-refused", ("food", ent.Owner)), args.Target, args.User,
            PopupType.SmallCaution);
    }

    /// <summary>
    ///     A Bad Blood whose stomach whitelist does not take <paramref name="food"/>. Only Bad Bloods are judged
    ///     here; anyone else is left to the usual stomach checks.
    /// </summary>
    public bool CannotEat(EntityUid eater, EntityUid food)
    {
        if (!HasComp<YautjaBadBloodComponent>(eater))
            return false;

        foreach (var stomach in _body.EnumerateOrgans<StomachComponent>(eater))
        {
            if (stomach.Comp2.SpecialDigestible == null ||
                !stomach.Comp2.IsSpecialDigestibleExclusive ||
                _whitelist.IsWhitelistPass(stomach.Comp2.SpecialDigestible, food))
            {
                return false;
            }
        }

        return true;
    }
}
