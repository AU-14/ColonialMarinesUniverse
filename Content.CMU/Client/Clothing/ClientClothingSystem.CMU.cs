using Content.Client.CMU14.Clothing;
using Content.Client.Inventory;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;

namespace Content.Client.Clothing;

public sealed partial class ClientClothingSystem
{
    private void RefreshCMUAppearanceEquipment(Entity<InventoryComponent> ent, SpriteComponent sprite)
    {
        if (!_inventorySlotsQuery.TryComp(ent, out var slots))
            return;

        var cache = EnsureComp<CMUClothingAppearanceComponent>(ent);
        var sex = CompOrNull<HumanoidProfileComponent>(ent)?.Sex;
        if (ReferenceEquals(cache.Sprite, sprite) && cache.Sex == sex && cache.Species == ent.Comp.SpeciesId &&
            ReferenceEquals(cache.Displacements, ent.Comp.Displacements) &&
            ReferenceEquals(cache.MaleDisplacements, ent.Comp.MaleDisplacements) &&
            ReferenceEquals(cache.FemaleDisplacements, ent.Comp.FemaleDisplacements) &&
            CMUClothingLayersMatch((ent, sprite), slots, cache))
            return;

        UpdateAllSlots(ent.AsNullable());
        cache.Sprite = sprite;
        cache.Sex = sex;
        cache.Species = ent.Comp.SpeciesId;
        cache.Displacements = ent.Comp.Displacements;
        cache.MaleDisplacements = ent.Comp.MaleDisplacements;
        cache.FemaleDisplacements = ent.Comp.FemaleDisplacements;
        cache.Layers.Clear();
        foreach (var keys in slots.VisualLayerKeys.Values)
        {
            foreach (var key in keys)
            {
                if (_sprite.TryGetLayer((ent, sprite), key, out var layer, false))
                    cache.Layers[key] = layer;
            }
        }
    }

    private bool CMUClothingLayersMatch(Entity<SpriteComponent?> ent, InventorySlotsComponent slots,
        CMUClothingAppearanceComponent cache)
    {
        var count = 0;
        foreach (var keys in slots.VisualLayerKeys.Values)
        {
            foreach (var key in keys)
            {
                count++;
                // A copied/replaced sprite can retain the same keys with different layers.
                if (!cache.Layers.TryGetValue(key, out var previous) ||
                    !_sprite.TryGetLayer(ent, key, out var layer, false) || !ReferenceEquals(previous, layer))
                    return false;
            }
        }

        return count == cache.Layers.Count;
    }
}
