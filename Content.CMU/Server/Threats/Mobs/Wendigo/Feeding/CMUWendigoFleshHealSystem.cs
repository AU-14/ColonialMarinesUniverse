using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Round.Antags.Cannibal;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Feeding;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;

namespace Content.Server.CMU14.Threats.Mobs.Wendigo.Feeding;

/// <summary>
/// Heals a Wendigo when it finishes a piece of human flesh: carved human meat, a human limb or a human organ.
/// Only Wendigos carry <see cref="CMUWendigoFleshHealComponent"/>, so the lab procedure's human test subjects are untouched.
/// </summary>
public sealed partial class CMUWendigoFleshHealSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUWendigoFleshHealComponent, IngestingEvent>(OnIngesting);
        SubscribeLocalEvent<CMUWendigoFleshMealComponent, IngestedEvent>(OnMealBitten);
        SubscribeLocalEvent<CMUWendigoFleshMealComponent, FullyEatenEvent>(OnMealEaten);
    }

    public static bool IsHumanFlesh(EntityUid food, IEntityManager entMan)
    {
        return entMan.TryGetComponent(food, out EdibleComponent? edible)
            && edible.Edible == IngestionSystem.Food
            && CMUHumanMeat.IsHumanFlesh(entMan.GetComponent<MetaDataComponent>(food).EntityPrototype);
    }

    private void OnIngesting(Entity<CMUWendigoFleshHealComponent> ent, ref IngestingEvent args)
    {
        // Raised on the eater before the food's own bite event, so the marker is in place for OnMealBitten.
        if (IsHumanFlesh(args.Food, EntityManager))
            EnsureComp<CMUWendigoFleshMealComponent>(args.Food);
    }

    private void OnMealBitten(Entity<CMUWendigoFleshMealComponent> food, ref IngestedEvent args)
    {
        // Whoever takes the last bite decides the heal; a human finishing a Wendigo's leftovers heals nobody.
        food.Comp.Eater = HasComp<CMUWendigoFleshHealComponent>(args.Target) ? args.Target : null;
    }

    private void OnMealEaten(Entity<CMUWendigoFleshMealComponent> food, ref FullyEatenEvent args)
    {
        if (food.Comp.Eater is not { } eater
            || TerminatingOrDeleted(eater)
            || _mobState.IsDead(eater)
            || !TryComp(eater, out CMUWendigoFleshHealComponent? heal))
        {
            return;
        }

        Heal((eater, heal));
    }

    /// <summary>
    /// Applies one finished piece of human flesh worth of healing.
    /// </summary>
    public void Heal(Entity<CMUWendigoFleshHealComponent> ent)
    {
        _damageable.TryChangeDamage(ent.Owner, ent.Comp.Heal, ignoreResistances: true, interruptsDoAfters: false);

        if (ent.Comp.BleedReduction > 0f)
            _bloodstream.TryModifyBleedAmount(ent.Owner, -ent.Comp.BleedReduction);

        _popup.PopupEntity(Loc.GetString(ent.Comp.Popup), ent.Owner, ent.Owner, PopupType.Medium);
    }
}
