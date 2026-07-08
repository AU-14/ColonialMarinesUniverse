using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.Body.Part;
using Content.Shared.Inventory;

namespace Content.Shared._RMC14.Armor;

[ByRefEvent]
public record struct CMGetArmorEvent(
    SlotFlags TargetSlots,
    int XenoArmor = 0,
    int Melee = 0,
    int Bullet = 0,
    int Bio = 0,
    int FrontalArmor = 0,
    int SideArmor = 0,
    double ArmorModifier = 1,
    int ExplosionArmor = 0,
    BodyPartType? TargetPart = null,
    TargetBodyZone? TargetZone = null
) : IInventoryRelayEvent;
