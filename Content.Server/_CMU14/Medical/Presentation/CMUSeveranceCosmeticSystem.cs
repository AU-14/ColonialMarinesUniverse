using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Machines;
using Content.Shared._CMU14.Medical.Presentation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Standing;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Server._CMU14.Medical.Presentation;

public sealed partial class CMUSeveranceCosmeticSystem : EntitySystem
{
    [Dependency] private SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedHumanMedicalSystem _humanMedical = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private CMUMedicalVisibilitySystem _medicalVisibility = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    /// <summary>
    ///     Bodies queued for next-tick hand-removal / glove-drop / shoe-drop / force-down.
    ///     Doing it inline races with FlingPartFromBody's reparent of the
    ///     severed limb - RemoveHand's TryDrop + ShutdownContainer mutations
    ///     occurring mid-arm-reparent suppressed the dropped-arm spawn when
    ///     the marine held an item.
    /// </summary>
    private readonly Queue<DeferredHandSever> _deferredHandSever = new();
    private readonly Queue<DeferredHandEquipmentDrop> _deferredHandEquipmentDrop = new();
    private readonly Queue<DeferredHeadSever> _deferredHeadSever = new();
    private readonly Queue<DeferredLegSever> _deferredLegSever = new();

    private readonly record struct DeferredHandSever(EntityUid Body, string ArmSlot, string HandId);
    private readonly record struct DeferredHandEquipmentDrop(EntityUid Body);
    private readonly record struct DeferredHeadSever(EntityUid Body, EntityUid Head, string SourceName);
    private readonly record struct DeferredLegSever(EntityUid Body);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanMedicalComponent, BodyPartRemovedEvent>(OnPartRemoved);
        SubscribeLocalEvent<HumanMedicalComponent, BodyPartAddedEvent>(OnPartAdded);
        SubscribeLocalEvent<HumanMedicalComponent, StandAttemptEvent>(OnStandAttempt);
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

        while (_deferredHandEquipmentDrop.TryDequeue(out var handDrop))
        {
            if (Deleted(handDrop.Body))
                continue;

            if (_inventory.TryGetSlotEntity(handDrop.Body, "gloves", out _))
                _inventory.TryUnequip(handDrop.Body, "gloves", force: true);
        }

        while (_deferredHeadSever.TryDequeue(out var headSever))
        {
            if (!Deleted(headSever.Head))
                _metaData.SetEntityName(headSever.Head, Loc.GetString("cmu-medical-severed-head-name", ("owner", headSever.SourceName)));

            if (Deleted(headSever.Body))
                continue;

            DropHeadEquipment(headSever.Body);
        }

        while (_deferredLegSever.TryDequeue(out var d))
        {
            if (Deleted(d.Body))
                continue;

            if (_inventory.TryGetSlotEntity(d.Body, "shoes", out _))
                _inventory.TryUnequip(d.Body, "shoes", force: true);

            _standing.Down(d.Body);
        }
    }

    private void OnPartRemoved(Entity<HumanMedicalComponent> ent, ref BodyPartRemovedEvent args)
    {
        _medicalVisibility.RefreshSubtree(args.Part.Owner);

        var partType = args.Part.Comp.PartType;
        RefreshAttachedPartLayers(ent.Owner, args.Part.Owner, args.Part.Comp, visible: false);

        TagDroppedPartWithClothing(ent.Owner, args.Part.Owner);

        // Deferred - see _deferredHandSever doc above for the race.
        if (partType == BodyPartType.Arm
            && HandIdForArmSlot(args.Slot) is { } handId
            && HasComp<HandsComponent>(ent.Owner))
        {
            _deferredHandSever.Enqueue(new DeferredHandSever(ent.Owner, args.Slot, handId));
        }

        if (partType == BodyPartType.Hand)
            _deferredHandEquipmentDrop.Enqueue(new DeferredHandEquipmentDrop(ent.Owner));

        if (partType == BodyPartType.Head)
            _deferredHeadSever.Enqueue(new DeferredHeadSever(ent.Owner, args.Part.Owner, MetaData(ent.Owner).EntityName));

        if (partType is BodyPartType.Leg or BodyPartType.Foot)
            _deferredLegSever.Enqueue(new DeferredLegSever(ent.Owner));
    }

    private void OnPartAdded(Entity<HumanMedicalComponent> ent, ref BodyPartAddedEvent args)
    {
        _medicalVisibility.RefreshSubtree(args.Part.Owner);

        RefreshAttachedPartLayers(ent.Owner, args.Part.Owner, args.Part.Comp, visible: true);
        RefreshLedgerPresence(ent, args.Part.Owner, args.Part.Comp);

        RestoreUsableHands(ent.Owner);
    }

    private void RefreshAttachedPartLayers(
        EntityUid body,
        EntityUid root,
        BodyPartComponent rootPart,
        bool visible)
    {
        if (!HasComp<HumanoidAppearanceComponent>(body))
            return;

        foreach (var (partUid, part) in _body.GetBodyPartChildren(root, rootPart))
        {
            _ = partUid;

            if (LayerForPart(part.PartType, part.Symmetry) is not { } layer)
                continue;

            _humanoid.SetLayerVisibility(body, layer, visible);
            // DamageVisualsSystem.UpdateDisabledLayers reads a `bool disabled`
            // appearance datum keyed by the layer enum; without setting it,
            // the Brute/Burn overlay can float over now-missing limbs.
            _appearance.SetData(body, layer, !visible);
        }
    }

    private void RefreshLedgerPresence(Entity<HumanMedicalComponent> body, EntityUid root, BodyPartComponent rootPart)
    {
        var primaryRegion = ResolvePartRegion(root, rootPart);
        if (primaryRegion == BodyRegion.None)
            primaryRegion = BodyRegion.Chest;

        var transaction = new MedicalTransaction(primaryRegion);

        foreach (var (partUid, part) in _body.GetBodyPartChildren(root, rootPart))
        {
            var region = ResolvePartRegion(partUid, part);
            if (region == BodyRegion.None)
                continue;

            var presence = HasComp<CMUProstheticLimbComponent>(partUid)
                ? LimbPresence.Prosthetic
                : LimbPresence.Present;

            if (HumanMedicalLedger.GetRegion(body.Comp, region).Presence == presence)
                continue;

            transaction.Add(MedicalEffect.SetRegionPresence(region, presence));
        }

        if (transaction.Count == 0)
            return;

        _humanMedical.ApplyTransaction(body, transaction);
    }

    private BodyRegion ResolvePartRegion(EntityUid partUid, BodyPartComponent part)
    {
        if (TryComp<AnatomyRegionComponent>(partUid, out var anatomy) &&
            anatomy.Region != BodyRegion.None)
        {
            return anatomy.Region;
        }

        return RegionForPart(part.PartType, part.Symmetry);
    }

    private void RestoreUsableHands(EntityUid body)
    {
        if (!TryComp<HandsComponent>(body, out var hands))
            return;

        foreach (var (partId, part) in _body.GetBodyChildren(body))
        {
            if (part.PartType != BodyPartType.Hand)
                continue;

            var location = part.Symmetry switch
            {
                BodyPartSymmetry.Left => HandLocation.Left,
                BodyPartSymmetry.Right => HandLocation.Right,
                _ => HandLocation.Middle,
            };

            string? handId = null;
            if (_body.GetParentPartAndSlotOrNull(partId) is { } parentSlot)
                handId = SharedBodySystem.GetPartSlotContainerId(parentSlot.Slot);
            else if (part.Symmetry is BodyPartSymmetry.Left or BodyPartSymmetry.Right)
                handId = SharedBodySystem.GetPartSlotContainerId(part.Symmetry == BodyPartSymmetry.Left
                    ? "left_hand"
                    : "right_hand");

            if (handId == null)
                continue;

            if (!_hands.TrySetHandLocation((body, hands), handId, location))
                _hands.AddHand((body, hands), handId, location);
        }

        if (NormalizeBodyHandOrder(hands))
            Dirty(body, hands);

        if (hands.ActiveHandId == null && hands.SortedHands.Count > 0)
            _hands.SetActiveHand((body, hands), hands.SortedHands[0]);
    }

    private bool NormalizeBodyHandOrder(HandsComponent hands)
    {
        var sortedHands = hands.SortedHands;
        if (sortedHands.Count < 2)
            return false;

        var ordered = new List<string>(sortedHands.Count);
        AddCanonicalHand(sortedHands, ordered, "right_hand");
        AddCanonicalHand(sortedHands, ordered, "left_hand");

        foreach (var hand in sortedHands)
        {
            if (!ordered.Contains(hand))
                ordered.Add(hand);
        }

        var changed = false;
        for (var i = 0; i < sortedHands.Count; i++)
        {
            if (sortedHands[i] == ordered[i])
                continue;

            changed = true;
            break;
        }

        if (!changed)
            return false;

        sortedHands.Clear();
        sortedHands.AddRange(ordered);
        return true;
    }

    private static void AddCanonicalHand(IReadOnlyList<string> sortedHands, List<string> ordered, string canonicalSlot)
    {
        foreach (var hand in sortedHands)
        {
            if (BarePartSlot(hand) != canonicalSlot || ordered.Contains(hand))
                continue;

            ordered.Add(hand);
            return;
        }
    }

    private void OnStandAttempt(Entity<HumanMedicalComponent> ent, ref StandAttemptEvent args)
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

    private void DropHeadEquipment(EntityUid body)
    {
        TryDropSlot(body, "head");
        TryDropSlot(body, "mask");
        TryDropSlot(body, "eyes");
        TryDropSlot(body, "ears");
    }

    private void TryDropSlot(EntityUid body, string slot)
    {
        if (_inventory.TryGetSlotEntity(body, slot, out _))
            _inventory.TryUnequip(body, slot, force: true);
    }

    /// <summary>
    ///     Vanilla HandsSystem.HandleBodyPartAdded registers the hand using
    ///     the *prefixed* container id (SharedBodySystem.PartSlotContainerIdPrefix
    ///     + slotId), not the bare slot id - we must match that for RemoveHand
    ///     to find the entry.
    /// </summary>
    private bool HasAttachedHandForArmSlot(EntityUid body, string armSlot)
    {
        if (SymmetryForArmSlot(armSlot) is null
            || !TryComp<BodyComponent>(body, out var bodyComp)
            || bodyComp.RootContainer.ContainedEntity is not { } root)
        {
            return false;
        }

        var bareArmSlot = BarePartSlot(armSlot);
        if (!_container.TryGetContainer(root, SharedBodySystem.GetPartSlotContainerId(bareArmSlot), out var armContainer))
            return false;

        foreach (var arm in armContainer.ContainedEntities)
        {
            if (!TryComp<BodyPartComponent>(arm, out var armComp))
                continue;

            foreach (var (slotId, slot) in armComp.Children)
            {
                if (slot.Type != BodyPartType.Hand)
                    continue;

                if (!_container.TryGetContainer(arm, SharedBodySystem.GetPartSlotContainerId(slotId), out var handContainer))
                    continue;

                if (handContainer.ContainedEntities.Count > 0)
                    return true;
            }
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
            ? slot.Substring(prefix.Length)
            : slot;
    }

    private static BodyRegion RegionForPart(BodyPartType type, BodyPartSymmetry symmetry)
    {
        return (type, symmetry) switch
        {
            (BodyPartType.Head, _) => BodyRegion.Head,
            (BodyPartType.Torso, _) => BodyRegion.Chest,
            (BodyPartType.Arm, BodyPartSymmetry.Left) => BodyRegion.LeftArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => BodyRegion.RightArm,
            (BodyPartType.Hand, BodyPartSymmetry.Left) => BodyRegion.LeftHand,
            (BodyPartType.Hand, BodyPartSymmetry.Right) => BodyRegion.RightHand,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => BodyRegion.LeftLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => BodyRegion.RightLeg,
            (BodyPartType.Foot, BodyPartSymmetry.Left) => BodyRegion.LeftFoot,
            (BodyPartType.Foot, BodyPartSymmetry.Right) => BodyRegion.RightFoot,
            _ => BodyRegion.None,
        };
    }

    private static HumanoidVisualLayers? LayerForPart(BodyPartType type, BodyPartSymmetry symmetry) =>
        (type, symmetry) switch
        {
            (BodyPartType.Head, BodyPartSymmetry.None) => HumanoidVisualLayers.Head,
            (BodyPartType.Arm, BodyPartSymmetry.Left) => HumanoidVisualLayers.LArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => HumanoidVisualLayers.RArm,
            (BodyPartType.Hand, BodyPartSymmetry.Left) => HumanoidVisualLayers.LHand,
            (BodyPartType.Hand, BodyPartSymmetry.Right) => HumanoidVisualLayers.RHand,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => HumanoidVisualLayers.LLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => HumanoidVisualLayers.RLeg,
            (BodyPartType.Foot, BodyPartSymmetry.Left) => HumanoidVisualLayers.LFoot,
            (BodyPartType.Foot, BodyPartSymmetry.Right) => HumanoidVisualLayers.RFoot,
            _ => null,
        };
}
