using System.Numerics;
using Content.Server.Decals;
using Content.Shared.Decals;
using Content.Shared.FootPrint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared._RMC14.Xenonids.Weeds;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.FootPrint;

public sealed partial class FootPrintsSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private IMapManager _map = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private SharedXenoWeedsSystem _weeds = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private EntityQuery<TransformComponent> _transformQuery;
    private EntityQuery<MobThresholdsComponent> _mobThresholdQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<DecalGridComponent> _decalGridQuery;

    // Cap how many dragging footprint decals can coexist on a single tile.
    private const int MaxFootprintsPerTile = 8;
    private static readonly Vector2 DecalCenterOffset = new(-0.5f, -0.5f);

    // Multiplier applied to a footprint's alpha when it is placed on xeno weeds;
    // keeps the weeds underneath visible.
    public const float WeedAlphaMultiplier = 0.3f;

    public override void Initialize()
    {
        base.Initialize();

        _transformQuery = GetEntityQuery<TransformComponent>();
        _mobThresholdQuery = GetEntityQuery<MobThresholdsComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _decalGridQuery = GetEntityQuery<DecalGridComponent>();

        SubscribeLocalEvent<FootPrintsComponent, ComponentStartup>(OnStartupComponent);
        SubscribeLocalEvent<FootPrintsComponent, MoveEvent>(OnMove);
    }

    private void OnStartupComponent(EntityUid uid, FootPrintsComponent component, ComponentStartup args)
    {
        component.StepSize = Math.Max(0f, component.StepSize + _random.NextFloat(-0.05f, 0.05f));
    }

    private void OnMove(EntityUid uid, FootPrintsComponent component, ref MoveEvent args)
    {
        if (component.PrintsColor.A <= 0f
            || !_transformQuery.TryComp(uid, out var transform)
            || !_mobThresholdQuery.TryComp(uid, out var mobThreshHolds)
            || !_map.TryFindGridAt(_transform.GetMapCoordinates((uid, transform)), out var gridUid, out _))
            return;

        var dragging = mobThreshHolds.CurrentThresholdState is MobState.Critical or MobState.Dead;
        var distance = (transform.LocalPosition - component.StepPos).Length();
        var stepSize = dragging ? component.DragSize : component.StepSize;

        if (!(distance > stepSize))
            return;

        if (!dragging || component.DraggingDecals.Count == 0)
        {
            component.StepPos = transform.LocalPosition;
            return;
        }

        component.RightStep = !component.RightStep;

        var spawnCoords = new EntityCoordinates(gridUid, transform.LocalPosition);

        if (_gridQuery.TryComp(gridUid, out var gridComp))
        {
            var tile = _mapSystem.CoordinatesToTile(gridUid, gridComp, spawnCoords);
            if (_decalGridQuery.TryComp(gridUid, out var decalGrid) &&
                CountDraggingDecalsInTile(gridUid, tile, component, decalGrid) >= MaxFootprintsPerTile)
                return;
        }

        var stepColor = component.PrintsColor;
        if (gridComp != null && _weeds.IsOnWeeds((gridUid, gridComp), spawnCoords))
            stepColor = stepColor.WithAlpha(stepColor.A * WeedAlphaMultiplier);

        var rotation = (transform.LocalPosition - component.StepPos).ToAngle() + Angle.FromDegrees(-90f);
        _decals.TryAddDecal(
            _random.Pick(component.DraggingDecals),
            spawnCoords.Offset(DecalCenterOffset),
            out _,
            stepColor,
            rotation,
            cleanable: true);

        component.PrintsColor = component.PrintsColor.WithAlpha(Math.Max(0f, component.PrintsColor.A - component.ColorReduceAlpha));
        component.StepPos = transform.LocalPosition;
    }

    private int CountDraggingDecalsInTile(
        EntityUid gridUid,
        Vector2i tile,
        FootPrintsComponent component,
        DecalGridComponent decalGrid)
    {
        var min = new Vector2(tile.X, tile.Y);
        var bounds = new Box2(min, min + Vector2.One);
        var decals = _decals.GetDecalsIntersecting(gridUid, bounds, decalGrid);
        var count = 0;

        foreach (var (_, decal) in decals)
        {
            if (!component.DraggingDecals.Contains(decal.Id))
                continue;

            count++;
        }

        return count;
    }
}
