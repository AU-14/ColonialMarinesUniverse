using System.Collections.Generic;
using Content.Server._CMU14.Medical.Presentation.Visibility;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Presentation.Visuals.Cosmetic;
using Content.Shared._CMU14.Medical.Presentation.Visuals;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Server._CMU14.Medical.Presentation.Visuals.Cosmetic;

public sealed partial class CMUSeveranceCosmeticSystem : EntitySystem
{
    [Dependency] private SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private CMUMedicalVisibilitySystem _medicalVisibility = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedCMURoboticLimbSystem _roboticLimbs = default!;

    /// <summary>
    ///     Bodies queued for next-tick hand-removal / glove-drop / shoe-drop / force-down.
    ///     Doing it inline races with FlingPartFromBody's reparent of the
    ///     severed limb — RemoveHand's TryDrop + ShutdownContainer mutations
    ///     occurring mid-arm-reparent suppressed the dropped-arm spawn when
    ///     the marine held an item.
    /// </summary>
    private readonly Queue<DeferredHandSever> _deferredHandSever = new();
    private readonly Queue<DeferredLegSever> _deferredLegSever = new();

    private readonly record struct DeferredHandSever(EntityUid Body, string ArmSlot, string HandId);
    private readonly record struct DeferredLegSever(EntityUid Body);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUHumanMedicalComponent, BodyPartRemovedEvent>(OnPartRemoved);
        SubscribeLocalEvent<CMUHumanMedicalComponent, BodyPartAddedEvent>(OnPartAdded);
        SubscribeLocalEvent<CMUHumanMedicalComponent, StandAttemptEvent>(OnStandAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        while (_deferredHandSever.TryDequeue(out var d))
        {
            if (Deleted(d.Body) || !HasComp<HandsComponent>(d.Body))
                continue;

            if (HasAttachedHandForArmSlot(d.Body, d.ArmSlot))
                continue;

            if (_inventory.TryGetSlotEntity(d.Body, "gloves", out _))
                _inventory.TryUnequip(d.Body, "gloves", force: true);

            _hands.RemoveHand(d.Body, d.HandId);
        }

        while (_deferredLegSever.TryDequeue(out var d))
        {
            if (Deleted(d.Body))
                continue;

            if (TryComp<BodyComponent>(d.Body, out var body) && body.LegEntities.Count >= 2)
                continue;

            if (_inventory.TryGetSlotEntity(d.Body, "shoes", out _))
                _inventory.TryUnequip(d.Body, "shoes", force: true);

            _standing.Down(d.Body);
        }
    }

    private void OnPartRemoved(Entity<CMUHumanMedicalComponent> ent, ref BodyPartRemovedEvent args)
    {
        _medicalVisibility.RefreshSubtree(args.Part.Owner);
        _roboticLimbs.BodyPartRemoved(ent.Owner);

        var partType = args.Part.Comp.PartType;
        var symmetry = args.Part.Comp.Symmetry;

        if (CMUMedicalVisualLayers.ForBodyPart(partType, symmetry) is { } layer && HasComp<HumanoidAppearanceComponent>(ent.Owner))
        {
            _humanoid.SetLayerVisibility(ent.Owner, layer, visible: false);
            // DamageVisualsSystem.UpdateDisabledLayers reads a `bool disabled`
            // appearance datum keyed by the layer enum; without setting it,
            // the Brute/Burn overlay floats over the now-missing limb.
            _appearance.SetData(ent.Owner, layer, true);
        }

        if (HasComp<InternalBleedingComponent>(args.Part.Owner))
            RemComp<InternalBleedingComponent>(args.Part.Owner);

        TagDroppedPartWithClothing(ent.Owner, args.Part.Owner);

        // Deferred — see _deferredHandSever doc above for the race.
        if (partType == BodyPartType.Arm
            && HandIdForArmSlot(args.Slot) is { } handId
            && HasComp<HandsComponent>(ent.Owner))
        {
            _deferredHandSever.Enqueue(new DeferredHandSever(ent.Owner, args.Slot, handId));
        }

        if (partType == BodyPartType.Leg)
            _deferredLegSever.Enqueue(new DeferredLegSever(ent.Owner));
    }

    private void OnPartAdded(Entity<CMUHumanMedicalComponent> ent, ref BodyPartAddedEvent args)
    {
        _medicalVisibility.RefreshSubtree(args.Part.Owner);
        _roboticLimbs.BodyPartAdded(ent.Owner, args.Part);

        var partType = args.Part.Comp.PartType;
        var symmetry = args.Part.Comp.Symmetry;

        if (CMUMedicalVisualLayers.ForBodyPart(partType, symmetry) is { } layer && HasComp<HumanoidAppearanceComponent>(ent.Owner))
        {
            _humanoid.SetLayerVisibility(ent.Owner, layer, visible: true);
            _appearance.SetData(ent.Owner, layer, false);
        }

        if (partType == BodyPartType.Arm
            && HasComp<HandsComponent>(ent.Owner))
        {
            RestoreArmHand(ent.Owner, args.Part.Owner, args.Slot);
        }
    }

    private void RestoreArmHand(EntityUid body, EntityUid arm, string armSlot)
    {
        if (!HasComp<BodyPartComponent>(arm))
            return;

        if (SymmetryForArmSlot(armSlot) is not { } slotSymmetry)
            return;

        foreach (var slot in _medicalIndex.GetBodyPartSlots(arm))
        {
            if (slot.Type != BodyPartType.Hand)
                continue;

            var location = slotSymmetry switch
            {
                BodyPartSymmetry.Left => HandLocation.Left,
                BodyPartSymmetry.Right => HandLocation.Right,
                _ => HandLocation.Middle,
            };

            var handId = HandIdForArmSlot(armSlot) ?? SharedBodySystem.PartSlotContainerIdPrefix + slot.SlotId;
            if (!_hands.TrySetHandLocation((body, null), handId, location))
                _hands.AddHand((body, null), handId, location);

            if (CMUMedicalVisualLayers.ForBodyPart(BodyPartType.Hand, slotSymmetry) is { } handLayer
                && HasComp<HumanoidAppearanceComponent>(body))
            {
                _humanoid.SetLayerVisibility(body, handLayer, visible: true);
                _appearance.SetData(body, handLayer, false);
            }
        }
    }

    private void OnStandAttempt(Entity<CMUHumanMedicalComponent> ent, ref StandAttemptEvent args)
    {
        if (args.Cancelled)
            return;
        if (!TryComp<BodyComponent>(ent.Owner, out var body))
            return;
        if (body.LegEntities.Count < 2)
            args.Cancel();
    }

    private void TagDroppedPartWithClothing(EntityUid wearer, EntityUid droppedPart)
    {
        if (TerminatingOrDeleted(wearer) || TerminatingOrDeleted(droppedPart))
            return;

        var marker = EnsureComp<CMUSeveredPartClothingComponent>(droppedPart);

        if (!_inventory.TryGetSlotEntity(wearer, "outerClothing", out var clothing))
        {
            marker.OuterClothingProto = null;
            Dirty(droppedPart, marker);
            return;
        }

        var meta = MetaData(clothing.Value);
        marker.OuterClothingProto = meta.EntityPrototype?.ID;
        Dirty(droppedPart, marker);
    }

    /// <summary>
    ///     Vanilla HandsSystem.HandleBodyPartAdded registers the hand using
    ///     the *prefixed* container id (SharedBodySystem.PartSlotContainerIdPrefix
    ///     + slotId), not the bare slot id — we must match that for RemoveHand
    ///     to find the entry.
    /// </summary>
    private bool HasAttachedHandForArmSlot(EntityUid body, string armSlot)
    {
        if (SymmetryForArmSlot(armSlot) is null || !_medicalIndex.TryGetRootPart(body, out var root))
            return false;

        var bareArmSlot = BarePartSlot(armSlot);
        if (!_medicalIndex.TryGetBodyPartInSlot(root.Owner, bareArmSlot, out var arm))
            return false;

        foreach (var slot in _medicalIndex.GetBodyPartSlots(arm))
        {
            if (slot.Type != BodyPartType.Hand)
                continue;
            if (slot.Part is not null)
                return true;
        }

        return false;
    }

    private static string? HandIdForArmSlot(string armSlot) => SymmetryForArmSlot(armSlot) switch
    {
        BodyPartSymmetry.Left => SharedBodySystem.PartSlotContainerIdPrefix + "left_hand",
        BodyPartSymmetry.Right => SharedBodySystem.PartSlotContainerIdPrefix + "right_hand",
        _ => null,
    };

    private static BodyPartSymmetry? SymmetryForArmSlot(string armSlot)
    {
        return BarePartSlot(armSlot) switch
        {
            "left_arm" => BodyPartSymmetry.Left,
            "right_arm" => BodyPartSymmetry.Right,
            _ => null,
        };
    }

    private static string BarePartSlot(string slot)
    {
        const string prefix = SharedBodySystem.PartSlotContainerIdPrefix;
        return slot.StartsWith(prefix, StringComparison.Ordinal)
            ? slot[prefix.Length..]
            : slot;
    }

}
