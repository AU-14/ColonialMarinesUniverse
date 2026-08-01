using Content.Shared._AU14.Visuals;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Client._AU14.Visuals;

/// <summary>
///     Client side of <see cref="AU14ContainerIconLayerComponent"/>: keeps one sprite layer in sync with
///     the newest item of the configured container. Replaces the former separate tripwire-payload and
///     workbench-weapon visual systems, which were this exact logic twice.
/// </summary>
public sealed class ContainerIconLayerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    // One well-known layer key per entity; the component only ever drives a single layer.
    private const string LayerKey = "au14-container-icon";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AU14ContainerIconLayerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AU14ContainerIconLayerComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<AU14ContainerIconLayerComponent, EntRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnStartup(Entity<AU14ContainerIconLayerComponent> ent, ref ComponentStartup args) => Refresh(ent);

    private void OnInserted(Entity<AU14ContainerIconLayerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.Container)
            Refresh(ent);
    }

    private void OnRemoved(Entity<AU14ContainerIconLayerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.Container)
            Refresh(ent);
    }

    private void Refresh(Entity<AU14ContainerIconLayerComponent> ent)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        var index = _sprite.LayerMapReserve((ent.Owner, sprite), LayerKey);

        EntityUid? show = null;
        if (_container.TryGetContainer(ent, ent.Comp.Container, out var container) && container.ContainedEntities.Count > 0)
            show = container.ContainedEntities[^1];

        if (show is { } item && TryComp(item, out MetaDataComponent? meta) && meta.EntityPrototype is { } proto)
        {
            // LayerSetSprite can't take an EntityPrototype specifier (engine throws NotImplemented);
            // resolve the prototype's icon to a plain texture first.
            var icon = _sprite.Frame0(new SpriteSpecifier.EntityPrototype(proto.ID));
            _sprite.LayerSetTexture((ent.Owner, sprite), index, icon);
            _sprite.LayerSetScale((ent.Owner, sprite), index, ent.Comp.Scale);
            _sprite.LayerSetOffset((ent.Owner, sprite), index, ent.Comp.Offset);
            if (ent.Comp.Tint is { } tint)
                _sprite.LayerSetColor((ent.Owner, sprite), index, tint);
            _sprite.LayerSetVisible((ent.Owner, sprite), index, true);
        }
        else
        {
            _sprite.LayerSetVisible((ent.Owner, sprite), index, false);
        }
    }
}
