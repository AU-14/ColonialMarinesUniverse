using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Body.Part;

namespace Content.Shared.Body.Systems;

public sealed partial class SharedBodySystem
{
    public bool TryCreatePartSlot(
        EntityUid? partId,
        string slotId,
        BodyPartType partType,
        [NotNullWhen(true)] out BodyPartSlot? slot,
        BodyPartComponent? part = null)
    {
        slot = null;
        if (partId is null || !Resolve(partId.Value, ref part, false))
            return false;

        var created = new BodyPartSlot(slotId, partType);
        if (!part.Children.TryAdd(slotId, created))
            return false;

        Dirty(partId.Value, part);
        slot = created;
        return true;
    }

    public bool TryCreatePartSlotAndAttach(
        EntityUid parentId,
        string slotId,
        EntityUid childId,
        BodyPartType partType,
        BodyPartComponent? parent = null,
        BodyPartComponent? child = null)
    {
        return TryCreatePartSlot(parentId, slotId, partType, out _, parent) &&
               AttachPart(parentId, slotId, childId, parent, child);
    }

    public bool IsPartRoot(
        EntityUid bodyId,
        EntityUid partId,
        BodyComponent? body = null,
        BodyPartComponent? part = null)
    {
        return Resolve(bodyId, ref body, false) &&
               Resolve(partId, ref part, false) &&
               GetRootPartOrNull(bodyId, body)?.Entity == partId;
    }

    public bool CanAttachToRoot(
        EntityUid bodyId,
        EntityUid partId,
        BodyComponent? body = null,
        BodyPartComponent? part = null)
    {
        return Resolve(bodyId, ref body, false) &&
               Resolve(partId, ref part, false) &&
               GetRootPartOrNull(bodyId, body) is null &&
               GetParentPartOrNull(partId) is null;
    }

    public bool AttachPartToRoot(
        EntityUid bodyId,
        EntityUid partId,
        BodyComponent? body = null,
        BodyPartComponent? part = null)
    {
        return CanAttachToRoot(bodyId, partId, body, part) && MoveSubtreeToBody(partId, bodyId);
    }

    public bool CanAttachPart(
        EntityUid parentId,
        BodyPartSlot slot,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        return CanAttachPart(parentId, slot.Id, partId, parentPart, part);
    }

    public bool CanAttachPart(
        EntityUid parentId,
        string slotId,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        if (parentId == partId ||
            !Resolve(parentId, ref parentPart, false) ||
            !Resolve(partId, ref part, false) ||
            !parentPart.Children.TryGetValue(slotId, out var slot) ||
            slot.Type != part.PartType ||
            GetParentPartOrNull(partId) is not null ||
            !TryComp<ParentOrganComponent>(parentId, out var parentOrgan) ||
            !HasComp<ChildOrganComponent>(partId))
        {
            return false;
        }

        if (TryComp<OrganComponent>(partId, out var organ) && GetCanonicalSlotId(organ.Category) != slotId)
            return false;

        return !TryGetRelatedOccupant(parentOrgan, slotId, bodyPart: true, out _);
    }

    public bool AttachPart(
        EntityUid parentPartId,
        string slotId,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        if (!Resolve(parentPartId, ref parentPart, false) ||
            !parentPart.Children.TryGetValue(slotId, out var slot))
        {
            return false;
        }

        return AttachPart(parentPartId, slot, partId, parentPart, part);
    }

    public bool AttachPart(
        EntityUid parentPartId,
        BodyPartSlot slot,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null)
    {
        if (!CanAttachPart(parentPartId, slot, partId, parentPart, part) ||
            !Resolve(parentPartId, ref parentPart, false) ||
            !Resolve(partId, ref part, false) ||
            !TryComp<ParentOrganComponent>(parentPartId, out var parentOrgan) ||
            !TryComp<ChildOrganComponent>(partId, out var childOrgan))
        {
            return false;
        }

        if (parentPart.Body is { } body)
        {
            if (!MoveSubtreeToBody(partId, body))
                return false;
        }
        else if (part.Body is not null)
        {
            return false;
        }

        _relations.Relate((parentPartId, parentOrgan), (partId, childOrgan));
        return true;
    }

    private bool MoveSubtreeToBody(EntityUid root, EntityUid body)
    {
        if (!_containers.TryGetContainer(body, BodyComponent.ContainerID, out var container))
            return false;

        var subtree = new List<EntityUid> { root };
        subtree.AddRange(_relations.AllChildren(root).Select(child => child.Owner));

        foreach (var organ in subtree)
        {
            if (!_containers.Insert(organ, container, force: true))
                return false;
        }

        return true;
    }
}
