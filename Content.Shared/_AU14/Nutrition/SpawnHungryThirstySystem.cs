using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Shared._AU14.Nutrition;

public sealed partial class SpawnHungryThirstySystem : EntitySystem
{
    [Dependency] private ThirstSystem _thirst = default!;
    [Dependency] private HungerSystem _hunger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnHungryThirstyComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<SpawnHungryThirstyComponent> ent, ref ComponentStartup args)
    {
        if (TryComp(ent, out ThirstComponent? thirst))
            _thirst.SetThirstToThreshold(ent, thirst, ent.Comp.StartingThirstThreshold);

        if (TryComp(ent, out HungerComponent? hunger))
            _hunger.SetHungerToThreshold(ent, hunger, ent.Comp.StartingHungerThreshold);
    }
}
