using Content.Shared._CMU14.Yautja;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._CMU14.Yautja;

public sealed partial class YautjaCapeVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<YautjaCapeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<YautjaCapeComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnStartup(Entity<YautjaCapeComponent> ent, ref ComponentStartup args)
    {
        UpdateVisuals(ent);
    }

    private void OnAfterAutoHandleState(Entity<YautjaCapeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent);
    }

    private void UpdateVisuals(Entity<YautjaCapeComponent> ent)
    {
        if (TryComp(ent, out SpriteComponent? sprite))
            _sprite.SetColor((ent.Owner, sprite), ent.Comp.Color);
    }
}
