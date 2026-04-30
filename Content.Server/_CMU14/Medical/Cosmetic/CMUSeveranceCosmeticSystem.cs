using System.Collections.Generic;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Cosmetic;
using Content.Shared._CMU14.Medical.Wounds;
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

namespace Content.Server._CMU14.Medical.Cosmetic;

public sealed class CMUSeveranceCosmeticSystem : EntitySystem
{
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    /// <summary>
    ///     Bodies queued for next-tick hand-removal / shoe-drop / force-down.
    ///     Doing it inline races with FlingPartFromBody's reparent of the
    ///     severed limb — RemoveHand's TryDrop + ShutdownContainer mutations
    ///     occurring mid-arm-reparent suppressed the dropped-arm spawn when
    ///     the marine held an item.
    /// </summary>
    private readonly Queue<DeferredHandSever> _deferredHandSever = new();
    private readonly Queue<DeferredLegSever> _deferredLegSever = new();

    private readonly record struct DeferredHandSever(EntityUid Body, string HandId);
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
            _hands.RemoveHand(d.Body, d.HandId);
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

    private void OnPartRemoved(Entity<CMUHumanMedicalComponent> ent, ref BodyPartRemovedEvent args)
    {
        var partType = args.Part.Comp.PartType;
        var symmetry = args.Part.Comp.Symmetry;

        if (LayerForPart(partType, symmetry) is { } layer && HasComp<HumanoidAppearanceComponent>(ent.Owner))
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
            && HandIdForSymmetry(symmetry) is { } handId
            && HasComp<HandsComponent>(ent.Owner))
        {
            _deferredHandSever.Enqueue(new DeferredHandSever(ent.Owner, handId));
        }

        if (partType == BodyPartType.Leg)
            _deferredLegSever.Enqueue(new DeferredLegSever(ent.Owner));
    }

    private void OnPartAdded(Entity<CMUHumanMedicalComponent> ent, ref BodyPartAddedEvent args)
    {
        var partType = args.Part.Comp.PartType;
        var symmetry = args.Part.Comp.Symmetry;

        if (LayerForPart(partType, symmetry) is { } layer && HasComp<HumanoidAppearanceComponent>(ent.Owner))
        {
            _humanoid.SetLayerVisibility(ent.Owner, layer, visible: true);
            _appearance.SetData(ent.Owner, layer, false);
        }

        if (partType == BodyPartType.Arm
            && HasComp<HandsComponent>(ent.Owner))
        {
            RestoreArmHand(ent.Owner, args.Part.Owner, symmetry);
        }
    }

    private void RestoreArmHand(EntityUid body, EntityUid arm, BodyPartSymmetry armSymmetry)
    {
        if (!TryComp<BodyPartComponent>(arm, out var armComp))
            return;

        foreach (var (slotId, slot) in armComp.Children)
        {
            if (slot.Type != BodyPartType.Hand)
                continue;

            var location = armSymmetry switch
            {
                BodyPartSymmetry.Left => HandLocation.Left,
                BodyPartSymmetry.Right => HandLocation.Right,
                _ => HandLocation.Middle,
            };

            var handId = SharedBodySystem.PartSlotContainerIdPrefix + slotId;
            _hands.AddHand((body, null), handId, location);

            if (LayerForPart(BodyPartType.Hand, armSymmetry) is { } handLayer
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
    private static string? HandIdForSymmetry(BodyPartSymmetry symmetry) => symmetry switch
    {
        BodyPartSymmetry.Left => SharedBodySystem.PartSlotContainerIdPrefix + "left_hand",
        BodyPartSymmetry.Right => SharedBodySystem.PartSlotContainerIdPrefix + "right_hand",
        _ => null,
    };

    private static HumanoidVisualLayers? LayerForPart(BodyPartType type, BodyPartSymmetry symmetry) =>
        (type, symmetry) switch
        {
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
