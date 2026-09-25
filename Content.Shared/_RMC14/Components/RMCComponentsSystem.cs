using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Components;

public sealed partial class RMCComponentsSystem : EntitySystem
{
    [Dependency] private IComponentFactory _compFactory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RemoveComponentsComponent, ComponentInit>(OnRemoveComponentsInit);
    }

    private void OnRemoveComponentsInit(Entity<RemoveComponentsComponent> ent, ref ComponentInit args)
    {
        EntityManager.RemoveComponents(ent, ent.Comp.Components);
    }

    public bool RemovesComponent<T>(EntityPrototype prototype) where T : Component, new()
    {
        return prototype.TryComp(out RemoveComponentsComponent? removed, _compFactory) &&
               removed.Components.ContainsKey(_compFactory.GetComponentName<T>());
    }
}
