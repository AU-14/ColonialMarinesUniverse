using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Nutrition;

public sealed partial class DistressSignalNutritionSystem : EntitySystem
{
    [Dependency] private SatiationSystem _satiation = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DistressSignalNutritionComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DistressSignalNutritionComponent, SatiationChangeRateEvent>(OnChangeRate);
    }

    private void OnStartup(Entity<DistressSignalNutritionComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp(ent, out SatiationComponent? satiation))
            return;

        RefreshRate((ent, satiation), SatiationSystem.Hunger);
        RefreshRate((ent, satiation), SatiationSystem.Thirst);
    }

    private void RefreshRate(Entity<SatiationComponent> ent, ProtoId<SatiationTypePrototype> type)
    {
        if (_satiation.GetValueOrNull(ent, type) is { } value)
            _satiation.SetValue(ent, type, value);
    }

    private static void OnChangeRate(
        Entity<DistressSignalNutritionComponent> ent,
        ref SatiationChangeRateEvent args)
    {
        if (args.Type == SatiationSystem.Hunger || args.Type == SatiationSystem.Thirst)
            args.ChangeRate *= ent.Comp.RateMultiplier;
    }
}
