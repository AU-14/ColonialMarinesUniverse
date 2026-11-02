using System.Linq;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Client.CMU14.Yautja;

public sealed partial class YautjaCapeVisualsSystem : EntitySystem
{
    private const string MountLayerKey = "cmu-yautja-cape-mount";
    private const string CapeWornState = "equipped-BACKPACK";

    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<YautjaCapeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<YautjaCapeComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
        SubscribeLocalEvent<YautjaCapeComponent, GetEquipmentVisualsEvent>(OnCapeGetEquipmentVisuals, after: [typeof(ClothingSystem)]);

        SubscribeLocalEvent<YautjaCapeMountComponent, GetEquipmentVisualsEvent>(OnMountGetEquipmentVisuals, after: [typeof(ClothingSystem)]);
        SubscribeLocalEvent<YautjaCapeMountComponent, EntInsertedIntoContainerMessage>(OnMountInserted);
        SubscribeLocalEvent<YautjaCapeMountComponent, EntRemovedFromContainerMessage>(OnMountRemoved);
    }

    private void OnStartup(Entity<YautjaCapeComponent> ent, ref ComponentStartup args)
    {
        UpdateVisuals(ent);
    }

    private void OnAfterAutoHandleState(Entity<YautjaCapeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent);
        _item.VisualsChanged(ent);

        // A recoloured cape that is fastened to armour redraws through the armour.
        if (_container.TryGetContainingContainer((ent.Owner, null, null), out var container) &&
            HasComp<YautjaCapeMountComponent>(container.Owner))
        {
            _item.VisualsChanged(container.Owner);
        }
    }

    public void UpdateVisuals(Entity<YautjaCapeComponent> ent)
    {
        if (!ent.Comp.Recolorable)
            return;

        if (TryComp(ent, out SpriteComponent? sprite))
            _sprite.SetColor((ent.Owner, sprite), ent.Comp.Color);
    }

    // The worn cape sheet is greyscale; the item sprite colour never reached the on-mob layer.
    private void OnCapeGetEquipmentVisuals(Entity<YautjaCapeComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (!ent.Comp.Recolorable)
            return;

        foreach (var (_, layer) in args.Layers)
        {
            layer.Color = ent.Comp.Color;
        }
    }

    private void OnMountGetEquipmentVisuals(Entity<YautjaCapeMountComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container) ||
            container.ContainedEntities.FirstOrDefault() is not { Valid: true } cape ||
            !TryComp(cape, out ClothingComponent? clothing) ||
            clothing.RsiPath is not { } rsi ||
            args.Layers.Any(layer => layer.Item1 == MountLayerKey))
        {
            return;
        }

        Color? color = TryComp(cape, out YautjaCapeComponent? capeComp) && capeComp.Recolorable
            ? capeComp.Color
            : null;

        args.Layers.Add((MountLayerKey, new PrototypeLayerData
        {
            RsiPath = rsi,
            State = CapeWornState,
            Color = color,
            Visible = true,
        }));
    }

    private void OnMountInserted(Entity<YautjaCapeMountComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        _item.VisualsChanged(ent);
    }

    private void OnMountRemoved(Entity<YautjaCapeMountComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        _item.VisualsChanged(ent);
    }
}
