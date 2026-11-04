using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Body.Part;

namespace Content.Shared.Body.Systems;

public sealed partial class SharedBodySystem
{
    /// <summary>
    /// Enumerates body parts in legacy hierarchy order, including the root part.
    /// </summary>
    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildren(
        EntityUid? id,
        BodyComponent? body = null,
        BodyPartComponent? rootPart = null)
    {
        if (id is null || !Resolve(id.Value, ref body, false))
            yield break;

        var yielded = new HashSet<EntityUid>();
        if (GetRootPartOrNull(id.Value, body) is { } root)
        {
            foreach (var part in GetBodyPartChildren(root.Entity, root.BodyPart))
            {
                if (yielded.Add(part.Id))
                    yield return part;
            }
        }

        // InitialBody inserts every organ before wiring relationships. Include any
        // not-yet-related compatibility part so insertion events can build a useful index.
        foreach (var organ in body.Organs?.ContainedEntities ?? [])
        {
            if (!yielded.Add(organ) || !TryComp<BodyPartComponent>(organ, out var part))
                continue;

            yield return (organ, part);
        }
    }

    /// <summary>
    /// Enumerates internal organs only. External Nubody organs are exposed as body parts.
    /// </summary>
    public IEnumerable<(EntityUid Id, OrganComponent Component)> GetBodyOrgans(
        EntityUid? bodyId,
        BodyComponent? body = null)
    {
        if (bodyId is null || !Resolve(bodyId.Value, ref body, false))
            yield break;

        foreach (var part in GetBodyChildren(bodyId, body))
        {
            foreach (var organ in GetPartOrgans(part.Id, part.Component))
                yield return organ;
        }
    }

    public IEnumerable<(EntityUid Id, OrganComponent Component)> GetPartOrgans(
        EntityUid partId,
        BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, false) || !TryComp<ParentOrganComponent>(partId, out var parent))
            yield break;

        var yielded = new HashSet<EntityUid>();
        foreach (var slotId in part.Organs.Keys)
        {
            if (!TryGetRelatedOccupant(parent, slotId, bodyPart: false, out var organ) ||
                !yielded.Add(organ) ||
                !TryComp<OrganComponent>(organ, out var component))
            {
                continue;
            }

            yield return (organ, component);
        }

        // Preserve visibility of new internal categories that have no old slot metadata.
        foreach (var child in parent.Children
                     .Where(uid => !HasComp<BodyPartComponent>(uid))
                     .OrderBy(GetCategorySortKey, StringComparer.Ordinal))
        {
            if (!yielded.Add(child) || !TryComp<OrganComponent>(child, out var component))
                continue;

            yield return (child, component);
        }
    }

    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyPartChildren(
        EntityUid partId,
        BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, false))
            yield break;

        yield return (partId, part);

        foreach (var child in GetDirectPartChildren(partId, part))
        {
            foreach (var descendant in GetBodyPartChildren(child.Id, child.Component))
                yield return descendant;
        }
    }

    private IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetDirectPartChildren(
        EntityUid partId,
        BodyPartComponent part)
    {
        if (!TryComp<ParentOrganComponent>(partId, out var parent))
            yield break;

        var yielded = new HashSet<EntityUid>();
        foreach (var slotId in part.Children.Keys)
        {
            if (!TryGetRelatedOccupant(parent, slotId, bodyPart: true, out var child) ||
                !yielded.Add(child) ||
                !TryComp<BodyPartComponent>(child, out var component))
            {
                continue;
            }

            yield return (child, component);
        }

        foreach (var child in parent.Children
                     .Where(uid => HasComp<BodyPartComponent>(uid))
                     .OrderBy(GetCategorySortKey, StringComparer.Ordinal))
        {
            if (!yielded.Add(child) || !TryComp<BodyPartComponent>(child, out var component))
                continue;

            yield return (child, component);
        }
    }

    public EntityUid? GetParentPartOrNull(EntityUid uid)
    {
        if (!TryComp<ChildOrganComponent>(uid, out var child) || child.Parent is not { } parent ||
            !HasComp<BodyPartComponent>(parent))
        {
            return null;
        }

        return parent;
    }

    public (EntityUid Parent, string Slot)? GetParentPartAndSlotOrNull(EntityUid uid)
    {
        if (GetParentPartOrNull(uid) is not { } parent || !TryComp<OrganComponent>(uid, out var organ) ||
            GetCanonicalSlotId(organ.Category) is not { } slot || slot == BodyRootContainerId)
        {
            return null;
        }

        return (parent, slot);
    }

    public bool TryGetParentBodyPart(
        EntityUid partUid,
        [NotNullWhen(true)] out EntityUid? parentUid,
        [NotNullWhen(true)] out BodyPartComponent? parentComponent)
    {
        parentUid = GetParentPartOrNull(partUid);
        if (parentUid is { } parent && TryComp(parent, out parentComponent))
            return true;

        parentUid = null;
        parentComponent = null;
        return false;
    }

    public (EntityUid Entity, BodyPartComponent BodyPart)? GetRootPartOrNull(
        EntityUid bodyId,
        BodyComponent? body = null)
    {
        if (!Resolve(bodyId, ref body, false))
            return null;

        (EntityUid Entity, BodyPartComponent BodyPart)? fallback = null;
        foreach (var organ in body.Organs?.ContainedEntities ?? [])
        {
            if (!TryComp<BodyPartComponent>(organ, out var part))
                continue;

            var parent = GetParentPartOrNull(organ);
            if (parent is not null && TryComp<OrganComponent>(parent.Value, out var parentOrgan) && parentOrgan.Body == bodyId)
                continue;

            if (part.PartType == BodyPartType.Torso)
                return (organ, part);

            fallback ??= (organ, part);
        }

        return fallback;
    }

    public bool BodyHasPartType(EntityUid bodyId, BodyPartType type, BodyComponent? body = null)
    {
        return GetBodyChildrenOfType(bodyId, type, body).Any();
    }

    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildrenOfType(
        EntityUid bodyId,
        BodyPartType type,
        BodyComponent? body = null)
    {
        foreach (var part in GetBodyChildren(bodyId, body))
        {
            if (part.Component.PartType == type)
                yield return part;
        }
    }

    public IEnumerable<BodyPartSlot> GetBodyAllSlots(EntityUid bodyId, BodyComponent? body = null)
    {
        foreach (var part in GetBodyChildren(bodyId, body))
        {
            foreach (var slot in part.Component.Children.Values)
                yield return slot;
        }
    }

    public IEnumerable<BodyPartSlot> GetAllBodyPartSlots(EntityUid partId, BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, false))
            yield break;

        foreach (var slot in part.Children.Values)
            yield return slot;

        foreach (var child in GetDirectPartChildren(partId, part))
        {
            foreach (var slot in GetAllBodyPartSlots(child.Id, child.Component))
                yield return slot;
        }
    }

    public bool PartHasChild(
        EntityUid parentId,
        EntityUid childId,
        BodyPartComponent? parent,
        BodyPartComponent? child)
    {
        return Resolve(parentId, ref parent, false) &&
               Resolve(childId, ref child, false) &&
               GetBodyPartChildren(parentId, parent).Any(found => found.Id == childId);
    }

    public bool BodyHasChild(
        EntityUid bodyId,
        EntityUid partId,
        BodyComponent? body = null,
        BodyPartComponent? part = null)
    {
        return Resolve(bodyId, ref body, false) &&
               Resolve(partId, ref part, false) &&
               GetBodyChildren(bodyId, body).Any(found => found.Id == partId);
    }

    public IEnumerable<EntityUid> GetBodyPartAdjacentParts(EntityUid partId, BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, false))
            yield break;

        if (GetParentPartOrNull(partId) is { } parent)
            yield return parent;

        foreach (var child in GetDirectPartChildren(partId, part))
            yield return child.Id;
    }

    public IEnumerable<(EntityUid AdjacentId, T Component)> GetBodyPartAdjacentPartsComponents<T>(
        EntityUid partId,
        BodyPartComponent? part = null)
        where T : IComponent
    {
        foreach (var adjacent in GetBodyPartAdjacentParts(partId, part))
        {
            if (TryComp<T>(adjacent, out var component))
                yield return (adjacent, component);
        }
    }

    public bool TryGetBodyPartAdjacentPartsComponents<T>(
        EntityUid partId,
        [NotNullWhen(true)] out List<(EntityUid AdjacentId, T Component)>? components,
        BodyPartComponent? part = null)
        where T : IComponent
    {
        components = GetBodyPartAdjacentPartsComponents<T>(partId, part).ToList();
        if (components.Count != 0)
            return true;

        components = null;
        return false;
    }

    public List<(T Comp, OrganComponent Organ)> GetBodyPartOrganComponents<T>(
        EntityUid uid,
        BodyPartComponent? part = null)
        where T : IComponent
    {
        var result = new List<(T Comp, OrganComponent Organ)>();
        foreach (var organ in GetPartOrgans(uid, part))
        {
            if (TryComp<T>(organ.Id, out var component))
                result.Add((component, organ.Component));
        }

        return result;
    }

    public bool TryGetBodyPartOrganComponents<T>(
        EntityUid uid,
        [NotNullWhen(true)] out List<(T Comp, OrganComponent Organ)>? components,
        BodyPartComponent? part = null)
        where T : IComponent
    {
        components = GetBodyPartOrganComponents<T>(uid, part);
        if (components.Count != 0)
            return true;

        components = null;
        return false;
    }

    private bool TryGetRelatedOccupant(
        ParentOrganComponent parent,
        string slotId,
        bool bodyPart,
        out EntityUid occupant)
    {
        foreach (var child in parent.Children)
        {
            if (HasComp<BodyPartComponent>(child) != bodyPart ||
                !TryComp<OrganComponent>(child, out var organ) ||
                GetCanonicalSlotId(organ.Category) != slotId)
            {
                continue;
            }

            occupant = child;
            return true;
        }

        occupant = default;
        return false;
    }

    private string GetCategorySortKey(EntityUid uid)
    {
        return TryComp<OrganComponent>(uid, out var organ)
            ? GetCanonicalSlotId(organ.Category) ?? organ.Category?.Id ?? string.Empty
            : string.Empty;
    }
}
