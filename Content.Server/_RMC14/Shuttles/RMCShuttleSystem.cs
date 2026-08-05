using System.Numerics;
using Content.Server.Shuttles.Events;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Shuttles;
using Content.Shared.Shuttles.Components;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._RMC14.Shuttles;

public sealed partial class RMCShuttleSystem : SharedRMCShuttleSystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MapSystem _mapSystem = default!;
    [Dependency] private TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlaySoundOnFTLStartComponent, FTLStartedEvent>(OnPlaySoundOnFTLStart);

        SubscribeLocalEvent<RMCSpawnEntityOnFTLStartComponent, FTLStartedEvent>(OnSpawnEntityOnFTLStart);

        SubscribeLocalEvent<FTLComponent, FTLCompletedEvent>(OnFTLCompleted);
    }

    private void OnPlaySoundOnFTLStart(Entity<PlaySoundOnFTLStartComponent> ent, ref FTLStartedEvent args)
    {
        if (Transform(ent).GridUid is not { } grid)
            return;

        _audio.PlayPvs(ent.Comp.Sound, grid);
        RemCompDeferred<PlaySoundOnFTLStartComponent>(ent);
    }

    /// <summary>
    ///     Spawn an entity on every tile that the FTLing grid occupied after it has moved to FTL space.
    /// </summary>
    private void OnSpawnEntityOnFTLStart(Entity<RMCSpawnEntityOnFTLStartComponent> ent, ref FTLStartedEvent args)
    {
        if (args.FromMapUid is not { } fromMap ||
            !TryComp(ent, out MapGridComponent? grid))
        {
            return;
        }

        ent.Comp.Coordinates.Clear();
        var mapId = _transform.GetMapId(fromMap);
        var enumerator = _mapSystem.GetAllTilesEnumerator(ent, grid);
        while (enumerator.MoveNext(out var tile))
        {
            var localPosition = _mapSystem.TileCenterToVector(ent, grid, tile.Value.GridIndices);
            var worldPosition = Vector2.Transform(localPosition, args.FTLFrom);
            ent.Comp.Coordinates.Add(new MapCoordinates(worldPosition, mapId));
        }

        foreach (var coordinate in ent.Comp.Coordinates)
            Spawn(ent.Comp.SpawnedEntity, coordinate);
    }

    private void OnFTLCompleted(Entity<FTLComponent> ent, ref FTLCompletedEvent args)
    {
        try
        {
            if (!TryComp(ent.Comp.TargetCoordinates.EntityId, out TransformComponent? targetTransform) ||
                targetTransform.GridUid is not { } mapGrid)
            {
                return;
            }

            // Create a box that has the width and height of the FTLing grid.
            var shuttleAABB = Comp<MapGridComponent>(ent).LocalAABB;
            var shuttleHeight = (float)Math.Floor(shuttleAABB.Height / 2f);
            var shuttleWidth = (float)Math.Floor(shuttleAABB.Width / 2f);
            var expansionHeight = shuttleAABB.Height % 2 == 0 ? shuttleHeight - 1 : shuttleHeight;
            var expansionWidth = shuttleAABB.Width % 2 == 0 ? shuttleWidth - 1 : shuttleWidth;

            // Center the box around the destination.
            var targetLocalAABB = Box2.CenteredAround(ent.Comp.TargetCoordinates.Position, Vector2.One);
            var extinguishArea = new Box2(targetLocalAABB.Left - expansionWidth,
                targetLocalAABB.Bottom - expansionHeight,
                targetLocalAABB.Right + expansionWidth,
                targetLocalAABB.Top + expansionHeight);
            var targetLocalAABBExpanded = _transform.GetWorldMatrix(Transform(mapGrid)).TransformBox(extinguishArea);

            // Delete all tile fires inside the box.
            var lookupEntities = new HashSet<EntityUid>();
            _lookup.GetLocalEntitiesIntersecting(mapGrid,
                targetLocalAABBExpanded,
                lookupEntities,
                LookupFlags.Uncontained);

            foreach (var entity in lookupEntities)
            {
                if (HasComp<TileFireComponent>(entity))
                    Del(entity);
            }
        }
        catch (Exception e)
        {
            Log.Error($"Error extinguishing fires under shuttle:\n{e}");
        }
    }
}
