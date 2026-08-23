using Content.Shared._CMU14.Yautja;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._CMU14.Yautja;

public sealed class YautjaBracerProfileVisualSystem : EntitySystem
{
    [Dependency] private ClothingSystem _clothing = default!;
    [Dependency] private SharedItemSystem _items = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<YautjaBracerProfileVisualComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<YautjaBracerProfileVisualComponent, AfterAutoHandleStateEvent>(OnAfterHandleState);
    }

    private void OnStartup(Entity<YautjaBracerProfileVisualComponent> ent, ref ComponentStartup args)
    {
        ApplyVisuals(ent);
    }

    private void OnAfterHandleState(Entity<YautjaBracerProfileVisualComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ApplyVisuals(ent);
    }

    private void ApplyVisuals(Entity<YautjaBracerProfileVisualComponent> ent)
    {
        if (ent.Comp.VisualPrototype is not { } visualPrototype ||
            !_prototypes.TryIndex(visualPrototype, out EntityPrototype? prototype))
        {
            return;
        }

        if (TryComp(ent, out SpriteComponent? sprite) &&
            prototype.TryComp<SpriteComponent>(out _, Factory))
        {
            var dummy = Spawn(prototype.ID, MapCoordinates.Nullspace);
            if (TryComp(dummy, out SpriteComponent? sourceSprite))
                _sprite.CopySprite((dummy, sourceSprite), (ent, sprite));
            Del(dummy);
        }

        if (TryComp(ent, out IconComponent? icon) &&
            prototype.TryComp<IconComponent>(out var sourceIcon, Factory))
        {
            icon.Icon = sourceIcon.Icon;
        }

        if (TryComp(ent, out ItemComponent? item) &&
            prototype.TryComp<ItemComponent>(out var sourceItem, Factory))
        {
            _items.CopyVisuals(ent, sourceItem, item);
        }

        if (TryComp(ent, out ClothingComponent? clothing) &&
            prototype.TryComp<ClothingComponent>(out var sourceClothing, Factory))
        {
            _clothing.CopyVisuals(ent, sourceClothing, clothing);
        }
    }
}
