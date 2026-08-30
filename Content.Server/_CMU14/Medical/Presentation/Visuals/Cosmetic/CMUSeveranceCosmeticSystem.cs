using System.Collections.Generic;
using System.Numerics;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Presentation.Visuals.Cosmetic;
using Content.Shared.CMU14.Medical.Presentation.Visuals;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Medical.Presentation.Visuals.Cosmetic;

public sealed partial class CMUSeveranceCosmeticSystem : EntitySystem
{
    [Dependency] private SharedHideableHumanoidLayersSystem _hideableLayers = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private OrganRelationSystem _organRelations = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IRobustRandom _random = default!;

    /// <summary>
    ///     Bodies queued for next-tick glove-drop / shoe-drop / force-down.
    ///     HandOrganSystem owns hand registration; delaying the clothing check
    ///     lets the complete severed subtree leave the old flat body first.
    /// </summary>
    private readonly Queue<DeferredHandSever> _deferredHandSever = new();
    private readonly Queue<DeferredLegSever> _deferredLegSever = new();
    private readonly Queue<EntityUid> _deferredHeadSever = new();

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
            if (Deleted(d.Body) || !TryComp<HandsComponent>(d.Body, out var hands))
                continue;

            if (_hands.TryGetHand((d.Body, hands), d.HandId, out _))
                continue;

            UnequipAndFling(d.Body, "gloves");
        }

        while (_deferredLegSever.TryDequeue(out var d))
        {
            if (Deleted(d.Body))
                continue;

            if (TryComp<BodyComponent>(d.Body, out var body) && body.LegEntities.Count >= 2)
                continue;

            UnequipAndFling(d.Body, "shoes");

            _standing.Down(d.Body);
        }

        while (_deferredHeadSever.TryDequeue(out var body))
        {
            if (Deleted(body))
                continue;

            UnequipAndFling(body, "ears");
            UnequipAndFling(body, "eyes");
            UnequipAndFling(body, "mask");
            UnequipAndFling(body, "head");
        }
    }

    private void OnPartRemoved(Entity<CMUHumanMedicalComponent> ent, ref BodyPartRemovedEvent args)
    {
        var partType = args.Part.Comp.PartType;
        var symmetry = args.Part.Comp.Symmetry;

        if (CMUMedicalVisualLayers.ForBodyPart(partType, symmetry) is { } layer &&
            HasComp<HideableHumanoidLayersComponent>(ent.Owner))
        {
            _hideableLayers.SetPermanentLayerOcclusion(ent.Owner, layer, hidden: true);
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
            && HandIdForArm(args.Part.Owner) is { } handId
            && HasComp<HandsComponent>(ent.Owner))
        {
            _deferredHandSever.Enqueue(new DeferredHandSever(ent.Owner, handId));
        }
        else if (partType == BodyPartType.Hand)
        {
            // A glove is one inventory item shared by both hand visuals, so a
            // severed hand must eject it even when its parent arm remains.
            UnequipAndFling(ent.Owner, "gloves");
        }

        if (partType == BodyPartType.Leg)
            _deferredLegSever.Enqueue(new DeferredLegSever(ent.Owner));
        else if (partType == BodyPartType.Foot)
            UnequipAndFling(ent.Owner, "shoes");

        if (partType == BodyPartType.Head)
            _deferredHeadSever.Enqueue(ent.Owner);
    }

    private void OnPartAdded(Entity<CMUHumanMedicalComponent> ent, ref BodyPartAddedEvent args)
    {
        var partType = args.Part.Comp.PartType;
        var symmetry = args.Part.Comp.Symmetry;

        if (CMUMedicalVisualLayers.ForBodyPart(partType, symmetry) is { } layer &&
            HasComp<HideableHumanoidLayersComponent>(ent.Owner))
        {
            _hideableLayers.SetPermanentLayerOcclusion(ent.Owner, layer, hidden: false);
            _appearance.SetData(ent.Owner, layer, false);
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

    private string? HandIdForArm(EntityUid arm)
    {
        foreach (var child in _organRelations.AllChildren(arm))
        {
            if (TryComp<HandOrganComponent>(child, out var hand))
                return hand.HandID;
        }

        return null;
    }

    private void UnequipAndFling(EntityUid wearer, string slot)
    {
        if (!_inventory.TryUnequip(wearer, slot, out var removedItem, silent: true, force: true) ||
            removedItem is not { } item ||
            TerminatingOrDeleted(item))
        {
            return;
        }

        // TryUnequip drops the item beside the wearer. Give every ejected item
        // its own short random trajectory, matching the severed-part fling.
        _transform.AttachToGridOrMap(item);
        var angle = _random.NextFloat(0f, MathF.Tau);
        var distance = _random.NextFloat(1.0f, 2.0f);
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
        _throwing.TryThrow(item, direction, baseThrowSpeed: 4f, doSpin: true, compensateFriction: true);
    }
}
