using System.Numerics;
using Content.Server.Decals;
using Content.Shared.Decals;
using Content.Shared.FootPrint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared._RMC14.Xenonids.Weeds;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.FootPrint;

public sealed partial class FootPrintsSystem : EntitySystem
{
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedXenoWeedsSystem _weeds = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<MobThresholdsComponent> _mobThresholdQuery;
    private EntityQuery<TransformComponent> _transformQuery;

    private static readonly Vector2 DecalCenterOffset = new(-0.5f, -0.5f);
    private static readonly Angle DraggingRotationOffset = Angle.FromDegrees(-90f);
    private static readonly Angle StepRotationOffset = Angle.FromDegrees(180f);

    // Multiplier applied to a footprint's alpha when it is placed on xeno weeds;
    // keeps the weeds underneath visible.
    public const float WeedAlphaMultiplier = 0.3f;

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _mobThresholdQuery = GetEntityQuery<MobThresholdsComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();

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

        // CMU14: organ-driven incapacity can override the damage-threshold cache.
        // var dragging = mobThreshHolds.CurrentThresholdState is MobState.Critical or MobState.Dead;
        var state = TryComp<MobStateComponent>(uid, out var mobState) ? mobState.CurrentState : mobThreshHolds.CurrentThresholdState;
        var dragging = state is MobState.Critical or MobState.Dead;
        var stepDelta = transform.LocalPosition - component.StepPos;
        var stepSize = dragging ? component.DragSize : component.StepSize;

        if (stepDelta.LengthSquared() <= stepSize * stepSize)
            return;

        if (dragging && component.DraggingDecals.Count == 0)
        {
            component.StepPos = transform.LocalPosition;
            return;
        }

        component.RightStep = !component.RightStep;

        var spawnCoords = CalcCoords(gridUid, component, transform, dragging);
        MapGridComponent? gridComp = null;
        _gridQuery.TryComp(gridUid, out gridComp);

        if (!dragging)
        {
            SpawnStepFootprintDecal(component, transform, gridUid, spawnCoords, gridComp);
            component.StepPos = transform.LocalPosition;
            return;
        }

        var stepColor = GetFootprintColor(component, gridUid, spawnCoords, gridComp);

        var rotation = stepDelta.ToAngle() + DraggingRotationOffset;
        _decals.TryAddDecal(
            _random.Pick(component.DraggingDecals),
            spawnCoords.Offset(DecalCenterOffset),
            out _,
            stepColor,
            rotation,
            cleanable: true);

        FadePrintColor(component);
        component.StepPos = transform.LocalPosition;
    }

    private void SpawnStepFootprintDecal(
        FootPrintsComponent component,
        TransformComponent transform,
        EntityUid gridUid,
        EntityCoordinates spawnCoords,
        MapGridComponent? gridComp)
    {
        _decals.TryAddDecal(
            PickStepDecal(component),
            spawnCoords.Offset(DecalCenterOffset),
            out _,
            GetFootprintColor(component, gridUid, spawnCoords, gridComp),
            transform.LocalRotation + StepRotationOffset,
            cleanable: true);

        FadePrintColor(component);
    }

    private Color GetFootprintColor(
        FootPrintsComponent component,
        EntityUid gridUid,
        EntityCoordinates spawnCoords,
        MapGridComponent? gridComp)
    {
        var stepColor = component.PrintsColor;
        if (gridComp != null && _weeds.IsOnWeeds((gridUid, gridComp), spawnCoords))
            return stepColor.WithAlpha(stepColor.A * WeedAlphaMultiplier);

        return stepColor;
    }

    private static void FadePrintColor(FootPrintsComponent component)
    {
        var alpha = Math.Max(0f, component.PrintsColor.A - component.ColorReduceAlpha);
        component.PrintsColor = component.PrintsColor.WithAlpha(alpha);

        if (alpha > 0f)
            return;

        component.ColorQuantity = 0f;
        component.ReagentToTransfer = null;
    }

    private static EntityCoordinates CalcCoords(
        EntityUid gridUid,
        FootPrintsComponent component,
        TransformComponent transform,
        bool dragging)
    {
        if (dragging)
            return new EntityCoordinates(gridUid, transform.LocalPosition);

        var offset = component.RightStep
            ? new Angle(StepRotationOffset + transform.LocalRotation).RotateVec(component.OffsetPrint)
            : new Angle(transform.LocalRotation).RotateVec(component.OffsetPrint);

        return new EntityCoordinates(gridUid, transform.LocalPosition + offset);
    }

    private static ProtoId<DecalPrototype> PickStepDecal(FootPrintsComponent component)
    {
        return component.RightStep ? component.RightBareDecal : component.LeftBareDecal;
    }
}
