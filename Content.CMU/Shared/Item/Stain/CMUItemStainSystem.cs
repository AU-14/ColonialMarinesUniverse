using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Shared.Network;

namespace Content.Shared._CMU14.Item.Stain;

/// <summary>
/// Owns authoritative item stain state and common exposed-equipment helpers.
/// </summary>
public sealed partial class CMUItemStainSystem : EntitySystem
{
    public const string HeadSlot = "head";
    public const string JumpsuitSlot = "jumpsuit";
    public const string GlovesSlot = "gloves";
    public const string MaskSlot = "mask";
    public const string OuterClothingSlot = "outerClothing";
    public const string ShoesSlot = "shoes";

    private const float DefaultCleaningDistance = 1.5f;

    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUItemStainComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUItemStainComponent, CMUCleaningEligibilityEvent>(OnCleaningEligibility);
    }

    private void OnExamined(Entity<CMUItemStainComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Color is not { } color)
            return;

        var message = ent.Comp.Kind == CMUItemStainKind.Oil
            ? "cmu-item-stain-examine-oil"
            : "cmu-item-stain-examine-blood";
        args.PushMarkup(Loc.GetString(message, ("color", color.ToHex())));
    }

    private void OnCleaningEligibility(Entity<CMUItemStainComponent> ent, ref CMUCleaningEligibilityEvent args)
    {
        if (ent.Comp.Color == null)
            return;

        args.CanClean = true;
        if (args.DistanceThreshold <= 0f)
            args.DistanceThreshold = DefaultCleaningDistance;
    }

    /// <summary>
    /// Applies or replaces an item's stain. Mutations are server-authoritative.
    /// </summary>
    public bool TryStain(
        EntityUid item,
        CMUItemStainKind kind,
        Color color,
        EntityUid? source = null,
        CMUItemStainComponent? component = null)
    {
        if (_net.IsClient || !HasComp<ItemComponent>(item))
            return false;

        component ??= EnsureComp<CMUItemStainComponent>(item);
        if (!component.CanStain || component.Color == color && component.Kind == kind)
            return false;

        component.Kind = kind;
        component.Color = color;
        Dirty(item, component);

        return true;
    }

    /// <summary>
    /// Removes an item's visual stain without changing forensic evidence.
    /// </summary>
    public bool TryClean(EntityUid item, CMUItemStainComponent? component = null)
    {
        if (_net.IsClient || !Resolve(item, ref component, false) || component.Color == null)
            return false;

        component.Color = null;
        Dirty(item, component);
        return true;
    }

    public bool TryStainSlot(EntityUid wearer, string slot, CMUItemStainKind kind, Color color, EntityUid? source = null)
    {
        return _inventory.TryGetSlotEntity(wearer, slot, out var item) &&
            item is { } itemUid &&
            TryStain(itemUid, kind, color, source);
    }

    public bool TryCleanSlot(EntityUid wearer, string slot)
    {
        return _inventory.TryGetSlotEntity(wearer, slot, out var item) &&
            item is { } itemUid &&
            TryClean(itemUid);
    }

    /// <summary>
    /// Stains the outer suit when present, otherwise the uniform.
    /// </summary>
    public bool TryStainOuterBody(EntityUid wearer, CMUItemStainKind kind, Color color, EntityUid? source = null)
    {
        if (_inventory.TryGetSlotEntity(wearer, OuterClothingSlot, out var outer) && outer is { } outerUid)
            return TryStain(outerUid, kind, color, source);

        return TryStainSlot(wearer, JumpsuitSlot, kind, color, source);
    }

    public bool TryCleanOuterBody(EntityUid wearer)
    {
        if (_inventory.TryGetSlotEntity(wearer, OuterClothingSlot, out var outer) && outer is { } outerUid)
            return TryClean(outerUid);

        return TryCleanSlot(wearer, JumpsuitSlot);
    }

    /// <summary>
    /// Applies a contact stain to all supported exposed equipment slots.
    /// </summary>
    public bool StainExposedEquipment(EntityUid wearer, CMUItemStainKind kind, Color color, EntityUid? source = null)
    {
        var changed = TryStainOuterBody(wearer, kind, color, source);
        changed |= TryStainSlot(wearer, HeadSlot, kind, color, source);
        changed |= TryStainSlot(wearer, MaskSlot, kind, color, source);
        changed |= TryStainSlot(wearer, GlovesSlot, kind, color, source);
        changed |= TryStainSlot(wearer, ShoesSlot, kind, color, source);
        return changed;
    }

    public bool CleanExposedEquipment(EntityUid wearer)
    {
        var changed = TryCleanOuterBody(wearer);
        changed |= TryCleanSlot(wearer, HeadSlot);
        changed |= TryCleanSlot(wearer, MaskSlot);
        changed |= TryCleanSlot(wearer, GlovesSlot);
        changed |= TryCleanSlot(wearer, ShoesSlot);
        return changed;
    }
}
