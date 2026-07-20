using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Content.Shared.Body;

public sealed partial class BodySystem
{
    /// <summary>
    /// Returns every organ contained by a body.
    /// This compatibility helper treats the new flat organ container as the old body-part collection.
    /// </summary>
    [Obsolete("Use an event-relay based approach instead")]
    [PublicAPI]
    public IEnumerable<Entity<OrganComponent>> GetBodyChildren(Entity<BodyComponent?> ent)
    {
        if (!_bodyQuery.Resolve(ent, ref ent.Comp))
            yield break;

        foreach (var organ in ent.Comp.Organs?.ContainedEntities ?? [])
        {
            if (_organQuery.TryComp(organ, out var organComp))
                yield return (organ, organComp);
        }
    }

    /// <summary>
    /// Returns a list of organs with a given component in the body.
    /// This is only provided to ease migration from the older BodySystem and should not be used in new code.
    /// </summary>
    /// <param name="ent">The body to query.</param>
    /// <param name="organs">The set of organs with the given component.</param>
    /// <typeparam name="TComp">The component to test for.</typeparam>
    /// <returns>Whether any organs were returned.</returns>
    [Obsolete("Use an event-relay based approach instead")]
    [PublicAPI]
    public bool TryGetOrgansWithComponent<TComp>(Entity<BodyComponent?> ent, out List<Entity<TComp>> organs) where TComp : Component
    {
        organs = new();
        if (!_bodyQuery.Resolve(ent, ref ent.Comp))
            return false;

        foreach (var organ in ent.Comp.Organs?.ContainedEntities ?? [])
        {
            if (TryComp<TComp>(organ, out var comp))
                organs.Add((organ, comp));
        }

        return organs.Count != 0;
    }

    /// <summary>
    /// Returns organs carrying a given component in the legacy two-component entity shape.
    /// </summary>
    [Obsolete("Use TryGetOrgansWithComponent instead")]
    [PublicAPI]
    public List<Entity<TComp, OrganComponent>> GetBodyOrganEntityComps<TComp>(Entity<BodyComponent?> ent)
        where TComp : Component
    {
        var organs = new List<Entity<TComp, OrganComponent>>();
        if (!_bodyQuery.Resolve(ent, ref ent.Comp))
            return organs;

        foreach (var organ in ent.Comp.Organs?.ContainedEntities ?? [])
        {
            if (TryComp<TComp>(organ, out var comp) && _organQuery.TryComp(organ, out var organComp))
                organs.Add((organ, comp, organComp));
        }

        return organs;
    }

    /// <summary>
    /// Tries to return organs carrying a given component in the legacy two-component entity shape.
    /// </summary>
    [Obsolete("Use TryGetOrgansWithComponent instead")]
    [PublicAPI]
    public bool TryGetBodyOrganEntityComps<TComp>(
        Entity<BodyComponent?> ent,
        [NotNullWhen(true)] out List<Entity<TComp, OrganComponent>>? organs)
        where TComp : Component
    {
        organs = GetBodyOrganEntityComps<TComp>(ent);
        if (organs.Count != 0)
            return true;

        organs = null;
        return false;
    }

    /// <summary>
    /// Inserts an organ into the flat organ container used by the current body model.
    /// The legacy slot selection no longer applies because organs are categorized by prototype.
    /// </summary>
    [Obsolete("Insert the organ into BodyComponent.ContainerID instead")]
    [PublicAPI]
    public bool AddOrganToFirstValidSlot(EntityUid bodyId, EntityUid organId)
    {
        if (!_bodyQuery.TryComp(bodyId, out var body) ||
            !_organQuery.TryComp(organId, out _) ||
            body.Organs is not { } organs)
        {
            return false;
        }

        return _container.Insert(organId, organs);
    }
}
