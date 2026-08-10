using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Shared._AU14.Nutrition;

public sealed partial class SpawnHungryThirstySystem : EntitySystem
{
    [Dependency] private SatiationSystem _satiation = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnHungryThirstyComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<SpawnHungryThirstyComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp(ent, out SatiationComponent? satiation))
            return;

        _satiation.SetValue((ent.Owner, satiation), SatiationSystem.Thirst, ent.Comp.StartingThirstThreshold);
        _satiation.SetValue((ent.Owner, satiation), SatiationSystem.Hunger, ent.Comp.StartingHungerThreshold);
    }
}
