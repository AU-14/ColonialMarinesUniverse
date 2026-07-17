using Content.Shared._CMU14.Yautja;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._CMU14.Yautja;

public sealed partial class YautjaScalpVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<YautjaScalpComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<YautjaScalpComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnStartup(Entity<YautjaScalpComponent> ent, ref ComponentStartup args)
    {
        UpdateVisuals(ent);
    }

    private void OnAfterAutoHandleState(Entity<YautjaScalpComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent);
    }

    private void UpdateVisuals(Entity<YautjaScalpComponent> ent)
    {
        if (TryComp(ent, out SpriteComponent? sprite))
            _sprite.LayerSetColor((ent.Owner, sprite), 0, ent.Comp.HairColor);
    }
}
