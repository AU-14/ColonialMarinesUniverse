using Content.Shared._RMC14.Inventory;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Inventory;

public sealed partial class CMInventorySystem : SharedCMInventorySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMItemSlotsComponent, AppearanceChangeEvent>(OnItemSlotsAppearanceChange);
    }

    private void OnItemSlotsAppearanceChange(Entity<CMItemSlotsComponent> ent, ref AppearanceChangeEvent args)
    {
        UpdateSlotSprite(ent);
    }

    protected override void ContentsUpdated(Entity<CMItemSlotsComponent> ent)
    {
        base.ContentsUpdated(ent);

        UpdateSlotSprite(ent);
    }

    /// <summary>
    /// Applies the current item-slot contents to the local sprite without writing back to the
    /// networked appearance component. Appearance-change events are raised while applying server
    /// state, so routing them through <see cref="ContentsUpdated"/> would dirty the entity again
    /// during prediction rollback.
    /// </summary>
    private void UpdateSlotSprite(Entity<CMItemSlotsComponent> ent)
    {
        if (!TryComp(ent, out SpriteComponent? sprite) ||
            !_sprite.LayerMapTryGet((ent.Owner, sprite), CMItemSlotsLayers.Fill, out var layer, false))
        {
            return;
        }

        if (!TryComp(ent, out ItemSlotsComponent? itemSlots))
        {
            _sprite.LayerSetVisible((ent.Owner, sprite), layer, false);
            return;
        }

        foreach (var (_, slot) in itemSlots.Slots)
        {
            if (slot.ContainerSlot?.ContainedEntity is { } contained &&
                !TerminatingOrDeleted(contained))
            {
                _sprite.LayerSetVisible((ent.Owner, sprite), layer, true);
                return;
            }
        }

        _sprite.LayerSetVisible((ent.Owner, sprite), layer, false);
    }
}
