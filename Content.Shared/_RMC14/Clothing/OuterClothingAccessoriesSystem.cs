using Content.Shared.Clothing;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Storage;
using Robust.Shared.Containers;

namespace Content.Shared._RMC14.Clothing;

public sealed class OuterClothingAccessoriesSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!;

    private EntityQuery<StorageComponent> _storageQuery;
    private EntityQuery<OuterClothingAccessoryComponent> _accessoryQuery;

    public override void Initialize()
    {
        base.Initialize();

        _storageQuery = GetEntityQuery<StorageComponent>();
        _accessoryQuery = GetEntityQuery<OuterClothingAccessoryComponent>();

        SubscribeLocalEvent<OuterClothingAccessoryHolderComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<OuterClothingAccessoryHolderComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<OuterClothingAccessoryHolderComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals, after: [typeof(ClothingSystem)]);

        SubscribeLocalEvent<OuterClothingAccessoryComponent, ItemToggledEvent>(OnToggled);
    }

    private void OnEntInserted(Entity<OuterClothingAccessoryHolderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        _item.VisualsChanged(ent);
    }

    private void OnEntRemoved(Entity<OuterClothingAccessoryHolderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        _item.VisualsChanged(ent);
    }

    private void OnToggled(Entity<OuterClothingAccessoryComponent> ent, ref ItemToggledEvent args)
    {
        if (!TryComp(ent, out TransformComponent? xform) ||
            TerminatingOrDeleted(xform.ParentUid))
        {
            return;
        }

        _item.VisualsChanged(xform.ParentUid);
    }

    private void OnGetEquipmentVisuals(Entity<OuterClothingAccessoryHolderComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (_inventory.TryGetSlot(args.Equipee, args.Slot, out var slot) &&
            (slot.SlotFlags & ent.Comp.Slot) == 0)
        {
            return;
        }

        if (!_storageQuery.TryComp(ent.Owner, out var storage))
            return;

        if (storage.Container == null)
            return;

        var index = 0;
        foreach (var item in storage.Container.ContainedEntities)
        {
            var layer = $"enum.{nameof(OuterClothingAccessoryLayers)}.{OuterClothingAccessoryLayers.OuterClothing}{index}_{Name(ent.Owner)}";

            if (!_accessoryQuery.TryComp(item, out var accessoryComp))
                continue;

            var rsi = _itemToggle.IsActivated(item) && accessoryComp.ToggledRsi != null
                ? accessoryComp.ToggledRsi
                : accessoryComp.Rsi;

            args.Layers.Add((layer, new PrototypeLayerData
            {
                RsiPath = rsi.RsiPath.ToString(),
                State = rsi.RsiState,
                Visible = true,
            }));

            index++;
        }
    }
}