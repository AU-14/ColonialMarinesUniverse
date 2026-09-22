using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Item;
using Content.Shared.Throwing;
using Robust.Shared.Map;

namespace Content.Shared.CMU14.ZLevels.Core.EntitySystems;

public abstract partial class CMUSharedZLevelsSystem
{
    private const float ThrowUpZVelocity = 6.5f;

    /// <summary>
    /// Projects an airborne item into a lower map through an uninterrupted column of openings.
    /// Falling state outlives the horizontal throw animation, so descending items remain visible.
    /// </summary>
    public bool TryGetOverheadItemProjection(EntityUid uid, EntityUid viewingMap,
        out MapCoordinates coordinates, out float height)
    {
        coordinates = default;
        height = default;
        if (!HasComp<CMUZFallingComponent>(uid) ||
            !HasComp<ItemComponent>(uid) ||
            !TryComp<CMUZPhysicsComponent>(uid, out var physics) ||
            !TryComp(uid, out TransformComponent? xform) ||
            xform.Anchored || !IsZPhysicsParent(xform) ||
            xform.MapUid is not { } sourceMap ||
            !TryGetZNetwork(sourceMap, out var network) ||
            !IsMapInNetwork(network.Value, viewingMap) ||
            !_zMapQuery.TryComp(sourceMap, out var sourceZ) ||
            !_zMapQuery.TryComp(viewingMap, out var targetZ) ||
            !_mapQuery.TryComp(viewingMap, out var targetMap))
        {
            return false;
        }

        var depth = (long) sourceZ.Depth - targetZ.Depth;
        if (depth <= 0 || depth > MaxZLevelsBelowRendering)
            return false;

        var position = _transform.GetWorldPosition(xform);
        var current = sourceMap;
        for (var i = 0; i < depth; i++)
        {
            // The source floor and every intervening floor can hide the item from below.
            if (_map.TryFindGridAt(current, position, out var gridUid, out var grid) &&
                !CMUZLevelOpeningCache.IsOpeningTile(gridUid, grid, position, _map, TilDefMan))
            {
                return false;
            }

            if (!TryMapDown(current, out var below))
                return false;

            current = below.Value.Owner;
        }

        if (current != viewingMap)
            return false;

        coordinates = new MapCoordinates(position, targetMap.MapId);
        height = (float) depth + physics.LocalPosition;
        return height > 0f;
    }

    private void InitThrowing()
    {
        SubscribeLocalEvent<CMUZPhysicsComponent, ThrownEvent>(OnThrown);
    }

    private void OnThrown(Entity<CMUZPhysicsComponent> ent, ref ThrownEvent args)
    {
        if (args.User is not { } user ||
            !TryComp<CMUZLevelViewerComponent>(user, out var viewer) ||
            !viewer.LookUp)
        {
            return;
        }

        if (ent.Comp.Velocity >= ThrowUpZVelocity)
            return;

        Entity<CMUZPhysicsComponent?> nullableEnt = (ent.Owner, ent.Comp);
        SetZVelocity(nullableEnt, ThrowUpZVelocity);
    }
}
