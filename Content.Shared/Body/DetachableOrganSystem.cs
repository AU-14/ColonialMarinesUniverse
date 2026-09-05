using Content.Shared.Body;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared.Body;

public sealed partial class DetachableOrganSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private EntityQuery<DetachableOrganComponent> _detachableOrgan;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private EntityQuery<OrganComponent> _organ;
    [Dependency] private OrganRelationSystem _organRelation = default!;

    /// <summary>
    /// Detaches an organ from its containing body.
    /// </summary>
    /// <param name="organ">The organ to detach</param>
    /// <returns>The body that spawned when this organ was detached</returns>
    [PublicAPI]
    public EntityUid? Detach(Entity<DetachableOrganComponent?> organ)
    {
        if (!_detachableOrgan.Resolve(organ, ref organ.Comp) || !_organ.TryComp(organ, out var organComp) || organComp.Body is not { } oldBody)
            return null;

        var body = PredictedSpawnNextToOrDrop(organ.Comp.DetachedBody, oldBody);
        _metaData.SetEntityName(body, Name(organ));

        if (!_container.TryGetContainer(body, BodyComponent.ContainerID, out var container))
        {
            Log.Error($"Entity {ToPrettyString(body)} relied on by {nameof(DetachableOrganComponent)} on {ToPrettyString(organ)} is missing a container ({BodyComponent.ContainerID}).");
            Del(body);
            return null;
        }

        var parts = new List<EntityUid> { organ.Owner };
        foreach (var child in _organRelation.AllChildren(organ.Owner))
            parts.Add(child.Owner);

        foreach (var part in parts)
        {
            if (_container.CanInsert(part, container))
                continue;

            Del(body);
            return null;
        }

        if (!_container.TryGetContainer(oldBody, BodyComponent.ContainerID, out var previousContainer))
        {
            Del(body);
            return null;
        }

        var previousParent = TryComp<ChildOrganComponent>(organ.Owner, out var relation) ? relation.Parent : null;
        _organRelation.Orphan(organ.Owner);
        foreach (var part in parts)
        {
            if (!_container.Insert(part, container, force: true))
            {
                // Never report a complete detachment after a partial transfer.
                foreach (var restore in parts)
                    _container.Insert(restore, previousContainer, force: true);
                if (previousParent is { } parent)
                    _organRelation.Relate(parent, organ.Owner);
                Del(body);
                return null;
            }
        }

        return body;
    }
}
