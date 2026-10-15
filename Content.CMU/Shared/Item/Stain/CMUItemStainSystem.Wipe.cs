namespace Content.Shared.CMU14.Item.Stain;

public sealed partial class CMUItemStainSystem
{
    private static readonly string[] WipeableSlots =
    [
        OuterClothingSlot,
        JumpsuitSlot,
        HeadSlot,
        MaskSlot,
        GlovesSlot,
        ShoesSlot,
    ];

    /// <summary>Whether anything the wearer has on is stained with blood or acid.</summary>
    public bool HasBloodStainedEquipment(EntityUid wearer)
    {
        foreach (var slot in WipeableSlots)
        {
            if (_inventory.TryGetSlotEntity(wearer, slot, out var item) &&
                TryComp<CMUItemStainComponent>(item, out var stain) &&
                stain is { Color: not null, Kind: CMUItemStainKind.Blood })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Cleans blood and acid off everything the wearer has on. Oil stays.</summary>
    public bool WipeBloodOffEquipment(EntityUid wearer)
    {
        var changed = false;
        foreach (var slot in WipeableSlots)
        {
            if (_inventory.TryGetSlotEntity(wearer, slot, out var item) &&
                TryComp<CMUItemStainComponent>(item, out var stain) &&
                stain.Kind == CMUItemStainKind.Blood)
            {
                changed |= TryClean(item.Value, stain);
            }
        }

        return changed;
    }
}
