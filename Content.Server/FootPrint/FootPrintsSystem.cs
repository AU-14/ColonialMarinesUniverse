using Content.Server.Atmos.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.FootPrint;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.FootPrint;

public sealed class FootPrintsSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedXenoWeedsSystem _weeds = default!;

    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<TransformComponent> _transformQuery;
    private EntityQuery<MobThresholdsComponent> _mobThresholdQuery;
    private EntityQuery<AppearanceComponent> _appearanceQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    // Cap how many Footstep entities can coexist on a single tile. Heavy traffic
    // areas (e.g. blood-soaked corridors) used to spawn unbounded entities and tank server perf.
    private const int MaxFootprintsPerTile = 8;

    // Multiplier applied to a footprint's alpha when it is placed on, or covered by, xeno weeds —
    // keeps the weeds underneath visible.
    public const float WeedAlphaMultiplier = 0.3f;

    private readonly HashSet<Entity<FootPrintComponent>> _footprintsInTile = new();

    public override void Initialize()
    {
        base.Initialize();

        _transformQuery = GetEntityQuery<TransformComponent>();
        _mobThresholdQuery = GetEntityQuery<MobThresholdsComponent>();
        _appearanceQuery = GetEntityQuery<AppearanceComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();

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

        component.RightStep = !component.RightStep;

        var spawnCoords = CalcCoords(gridUid, component, transform, dragging);

        // Bail if this tile has already hit the per-tile footprint cap.
        if (_gridQuery.TryComp(gridUid, out var gridComp))
        {
            var tile = _mapSystem.CoordinatesToTile(gridUid, gridComp, spawnCoords);
            _footprintsInTile.Clear();
            _lookup.GetLocalEntitiesIntersecting(gridUid, tile, _footprintsInTile, gridComp: gridComp);
            if (_footprintsInTile.Count >= MaxFootprintsPerTile)
                return;
        }

        var entity = Spawn(component.StepProtoId, spawnCoords);
        var footPrintComponent = EnsureComp<FootPrintComponent>(entity);

        footPrintComponent.PrintOwner = uid;

        // Dim the footprint if the tile already has weeds, so the weeds remain visible.
        var stepColor = component.PrintsColor;
        if (gridComp != null && _weeds.IsOnWeeds((gridUid, gridComp), spawnCoords))
        {
            stepColor = stepColor.WithAlpha(stepColor.A * WeedAlphaMultiplier);
            footPrintComponent.DimmedByWeeds = true;
        }

        Dirty(entity, footPrintComponent);

        if (_appearanceQuery.TryComp(entity, out var appearance))
        {
            _appearance.SetData(entity, FootPrintVisualState.State, PickState(uid, dragging), appearance);
            _appearance.SetData(entity, FootPrintVisualState.Color, stepColor, appearance);
        }

        if (!_transformQuery.TryComp(entity, out var stepTransform))
            return;

        stepTransform.LocalRotation = dragging
            ? (transform.LocalPosition - component.StepPos).ToAngle() + Angle.FromDegrees(-90f)
            : transform.LocalRotation + Angle.FromDegrees(180f);

        component.PrintsColor = component.PrintsColor.WithAlpha(Math.Max(0f, component.PrintsColor.A - component.ColorReduceAlpha));
        component.StepPos = transform.LocalPosition;

        if (!TryComp<SolutionContainerManagerComponent>(entity, out var solutionContainer)
            || !_solution.ResolveSolution((entity, solutionContainer), footPrintComponent.SolutionName, ref footPrintComponent.Solution, out var solution)
            || string.IsNullOrWhiteSpace(component.ReagentToTransfer) || solution.Volume >= 1)
            return;

        _solution.TryAddReagent(footPrintComponent.Solution.Value, component.ReagentToTransfer, 1, out _);
    }

    private EntityCoordinates CalcCoords(EntityUid uid, FootPrintsComponent component, TransformComponent transform, bool state)
    {
        if (state)
            return new EntityCoordinates(uid, transform.LocalPosition);

        var offset = component.RightStep
            ? new Angle(Angle.FromDegrees(180f) + transform.LocalRotation).RotateVec(component.OffsetPrint)
            : new Angle(transform.LocalRotation).RotateVec(component.OffsetPrint);

        return new EntityCoordinates(uid, transform.LocalPosition + offset);
    }

    private FootPrintVisuals PickState(EntityUid uid, bool dragging)
    {
        var state = FootPrintVisuals.BareFootPrint;

        if (_inventory.TryGetSlotEntity(uid, "shoes", out _))
            state = FootPrintVisuals.ShoesPrint;

        if (_inventory.TryGetSlotEntity(uid, "outerClothing", out var suit) && TryComp<PressureProtectionComponent>(suit, out _))
            state = FootPrintVisuals.SuitPrint;

        if (dragging)
            state = FootPrintVisuals.Dragging;

        return state;
    }
}
