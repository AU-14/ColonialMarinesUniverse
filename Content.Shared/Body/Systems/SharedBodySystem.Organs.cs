using System.Diagnostics.CodeAnalysis;
using Content.Shared.Body.Part;
using Robust.Shared.Containers;

namespace Content.Shared.Body.Systems;

public sealed partial class SharedBodySystem
{
    public bool TryCreateOrganSlot(
        EntityUid? parent,
        string slotId,
        [NotNullWhen(true)] out OrganSlot? slot,
        BodyPartComponent? part = null)
    {
        slot = null;
        if (parent is null || !Resolve(parent.Value, ref part, false))
            return false;

        var created = new OrganSlot(slotId);
        if (!part.Organs.TryAdd(slotId, created))
            return false;

        Dirty(parent.Value, part);
        slot = created;
        return true;
    }

    public bool CanInsertOrgan(EntityUid partId, string slotId, BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, false) || !part.Organs.ContainsKey(slotId) ||
            !TryComp<ParentOrganComponent>(partId, out var parent))
        {
            return false;
        }

        return !TryGetRelatedOccupant(parent, slotId, bodyPart: false, out _);
    }

    public bool CanInsertOrgan(EntityUid partId, OrganSlot slot, BodyPartComponent? part = null)
    {
        return CanInsertOrgan(partId, slot.Id, part);
    }

    public bool InsertOrgan(
        EntityUid partId,
        EntityUid organId,
        string slotId,
        BodyPartComponent? part = null,
        OrganComponent? organ = null)
    {
        if (!Resolve(partId, ref part, false) ||
            !Resolve(organId, ref organ, false) ||
            !CanInsertOrgan(partId, slotId, part) ||
            GetCanonicalSlotId(organ.Category) != slotId ||
            GetParentPartOrNull(organId) is not null ||
            !TryComp<ParentOrganComponent>(partId, out var parent) ||
            !TryComp<ChildOrganComponent>(organId, out var child))
        {
            return false;
        }

        if (part.Body is { } body)
        {
            if (!MoveSubtreeToBody(organId, body))
                return false;
        }
        else if (organ.Body is not null)
        {
            return false;
        }

        _relations.Relate((partId, parent), (organId, child));
        return true;
    }

    public bool RemoveOrgan(EntityUid organId, OrganComponent? organ = null)
    {
        if (!Resolve(organId, ref organ, false) ||
            !TryComp<ChildOrganComponent>(organId, out var child) ||
            child.Parent is not { } parent ||
            !HasComp<BodyPartComponent>(parent))
        {
            return false;
        }

        BaseContainer? bodyContainer = null;
        if (organ.Body is { } body)
        {
            if (!_containers.TryGetContainer(body, BodyComponent.ContainerID, out bodyContainer) ||
                !_containers.CanRemove(organId, bodyContainer))
            {
                return false;
            }
        }

        _relations.Orphan((organId, child));
        if (bodyContainer is null || _containers.Remove(organId, bodyContainer))
            return true;

        // Container listeners can refuse removal after preflight. Restore the
        // anatomical relation so a failed extraction does not leave a missing slot.
        if (TryComp<ParentOrganComponent>(parent, out var parentOrgan))
            _relations.Relate((parent, parentOrgan), (organId, child));
        return false;
    }

    public bool AddOrganToFirstValidSlot(
        EntityUid partId,
        EntityUid organId,
        BodyPartComponent? part = null,
        OrganComponent? organ = null)
    {
        if (!Resolve(partId, ref part, false) || !Resolve(organId, ref organ, false))
            return false;

        var preferred = GetCanonicalSlotId(organ.Category);
        if (preferred is not null && part.Organs.ContainsKey(preferred))
            return InsertOrgan(partId, organId, preferred, part, organ);

        foreach (var slotId in part.Organs.Keys)
        {
            if (InsertOrgan(partId, organId, slotId, part, organ))
                return true;
        }

        return false;
    }

    public List<Entity<T, OrganComponent>> GetBodyOrganEntityComps<T>(Entity<BodyComponent?> entity)
        where T : IComponent
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return new();

        var result = new List<Entity<T, OrganComponent>>();
        foreach (var organ in GetBodyOrgans(entity.Owner, entity.Comp))
        {
            if (TryComp<T>(organ.Id, out var component))
                result.Add((organ.Id, component, organ.Component));
        }

        return result;
    }

    public bool TryGetBodyOrganEntityComps<T>(
        Entity<BodyComponent?> entity,
        [NotNullWhen(true)] out List<Entity<T, OrganComponent>>? components)
        where T : IComponent
    {
        components = GetBodyOrganEntityComps<T>(entity);
        if (components.Count != 0)
            return true;

        components = null;
        return false;
    }
}
