using System;
using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Rejuvenate;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Anatomy.BodyParts;

public sealed partial class CMUProstheticLimbTraitSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;

    private static readonly EntProtoId LeftArmPrototype = "CMUPartRoboticLeftArm";
    private static readonly EntProtoId RightArmPrototype = "CMUPartRoboticRightArm";
    private static readonly EntProtoId LeftLegPrototype = "CMUPartRoboticLeftLeg";
    private static readonly EntProtoId RightLegPrototype = "CMUPartRoboticRightLeg";

    private readonly Dictionary<EntityUid, ProstheticLimbFlags> _queued = new();
    private readonly List<(EntityUid Body, ProstheticLimbFlags Limbs)> _toApply = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUProstheticLeftArmComponent, ComponentStartup>(OnLeftArmStartup);
        SubscribeLocalEvent<CMUProstheticRightArmComponent, ComponentStartup>(OnRightArmStartup);
        SubscribeLocalEvent<CMUProstheticLeftLegComponent, ComponentStartup>(OnLeftLegStartup);
        SubscribeLocalEvent<CMUProstheticRightLegComponent, ComponentStartup>(OnRightLegStartup);

        SubscribeLocalEvent<CMUProstheticLeftArmComponent, RejuvenateEvent>(OnLeftArmRejuvenate);
        SubscribeLocalEvent<CMUProstheticRightArmComponent, RejuvenateEvent>(OnRightArmRejuvenate);
        SubscribeLocalEvent<CMUProstheticLeftLegComponent, RejuvenateEvent>(OnLeftLegRejuvenate);
        SubscribeLocalEvent<CMUProstheticRightLegComponent, RejuvenateEvent>(OnRightLegRejuvenate);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_queued.Count == 0)
            return;

        _toApply.Clear();
        foreach (var (body, limbs) in _queued)
            _toApply.Add((body, limbs));

        _queued.Clear();

        foreach (var (body, limbs) in _toApply)
        {
            if (TerminatingOrDeleted(body))
                continue;

            ApplyQueuedLimbs(body, limbs);
        }
    }

    private void OnLeftArmStartup(Entity<CMUProstheticLeftArmComponent> ent, ref ComponentStartup args)
    {
        Queue(ent.Owner, ProstheticLimbFlags.LeftArm);
    }

    private void OnRightArmStartup(Entity<CMUProstheticRightArmComponent> ent, ref ComponentStartup args)
    {
        Queue(ent.Owner, ProstheticLimbFlags.RightArm);
    }

    private void OnLeftLegStartup(Entity<CMUProstheticLeftLegComponent> ent, ref ComponentStartup args)
    {
        Queue(ent.Owner, ProstheticLimbFlags.LeftLeg);
    }

    private void OnRightLegStartup(Entity<CMUProstheticRightLegComponent> ent, ref ComponentStartup args)
    {
        Queue(ent.Owner, ProstheticLimbFlags.RightLeg);
    }

    private void OnLeftArmRejuvenate(Entity<CMUProstheticLeftArmComponent> ent, ref RejuvenateEvent args)
    {
        Queue(ent.Owner, ProstheticLimbFlags.LeftArm);
    }

    private void OnRightArmRejuvenate(Entity<CMUProstheticRightArmComponent> ent, ref RejuvenateEvent args)
    {
        Queue(ent.Owner, ProstheticLimbFlags.RightArm);
    }

    private void OnLeftLegRejuvenate(Entity<CMUProstheticLeftLegComponent> ent, ref RejuvenateEvent args)
    {
        Queue(ent.Owner, ProstheticLimbFlags.LeftLeg);
    }

    private void OnRightLegRejuvenate(Entity<CMUProstheticRightLegComponent> ent, ref RejuvenateEvent args)
    {
        Queue(ent.Owner, ProstheticLimbFlags.RightLeg);
    }

    private void Queue(EntityUid body, ProstheticLimbFlags limb)
    {
        _queued[body] = _queued.GetValueOrDefault(body) | limb;
    }

    private void ApplyQueuedLimbs(EntityUid body, ProstheticLimbFlags limbs)
    {
        if ((limbs & ProstheticLimbFlags.LeftArm) != 0)
            ReplaceLimb(body, BodyPartType.Arm, BodyPartSymmetry.Left, LeftArmPrototype);

        if ((limbs & ProstheticLimbFlags.RightArm) != 0)
            ReplaceLimb(body, BodyPartType.Arm, BodyPartSymmetry.Right, RightArmPrototype);

        if ((limbs & ProstheticLimbFlags.LeftLeg) != 0)
            ReplaceLimb(body, BodyPartType.Leg, BodyPartSymmetry.Left, LeftLegPrototype);

        if ((limbs & ProstheticLimbFlags.RightLeg) != 0)
            ReplaceLimb(body, BodyPartType.Leg, BodyPartSymmetry.Right, RightLegPrototype);
    }

    private void ReplaceLimb(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry, EntProtoId replacementPrototype)
    {
        if (!TryFindAttachedLimb(body, type, symmetry, out var currentPart))
            return;

        if (HasComp<CMURoboticLimbComponent>(currentPart))
            return;

        if (_body.GetParentPartAndSlotOrNull(currentPart) is not { } parentSlot)
            return;

        if (!_containers.TryGetContainingContainer((currentPart, null, null), out var container))
            return;

        var replacement = Spawn(replacementPrototype, Transform(currentPart).Coordinates);
        if (!TryComp<BodyPartComponent>(replacement, out var replacementPart) ||
            replacementPart.PartType != type ||
            replacementPart.Symmetry != symmetry)
        {
            QueueDel(replacement);
            return;
        }

        if (!_containers.Remove(currentPart, container, reparent: false, force: true))
        {
            QueueDel(replacement);
            return;
        }

        QueueDel(currentPart);

        if (!_body.AttachPart(parentSlot.Parent, parentSlot.Slot, replacement))
            QueueDel(replacement);
    }

    private bool TryFindAttachedLimb(
        EntityUid body,
        BodyPartType type,
        BodyPartSymmetry symmetry,
        out EntityUid part)
    {
        foreach (var (partUid, bodyPart) in _medicalIndex.GetBodyParts(body))
        {
            if (bodyPart.PartType != type ||
                bodyPart.Symmetry != symmetry)
            {
                continue;
            }

            part = partUid;
            return true;
        }

        part = default;
        return false;
    }

    [Flags]
    private enum ProstheticLimbFlags : byte
    {
        None = 0,
        LeftArm = 1 << 0,
        RightArm = 1 << 1,
        LeftLeg = 1 << 2,
        RightLeg = 1 << 3,
    }
}
