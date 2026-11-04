using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Chemistry;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Chemistry.Spill;

/// <summary>
/// A beaker put into storage without its lid on spills all of its contents.
/// </summary>
public sealed class CMUSpillWhenStoredOpenSystem : EntitySystem
{
    [Dependency] private OpenableSystem _openable = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSpillWhenStoredOpenComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
    }

    private void OnInserted(Entity<CMUSpillWhenStoredOpenComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        // Kits filled as they spawn don't spill.
        if (ent.Comp.CreationTick == _timing.CurTick)
            return;

        if (args.Container.ID != StorageComponent.ContainerId || !HasComp<StorageComponent>(args.Container.Owner))
            return;

        if (!TryComp<SpillableComponent>(ent, out var spillable) || _openable.IsClosed(ent))
            return;

        if (!_solution.TryGetSolution(ent.Owner, spillable.SolutionName, out var solutionEnt, out var solution) ||
            solution.Volume <= FixedPoint2.Zero)
            return;

        var spilled = _solution.SplitSolution(solutionEnt.Value, solution.Volume);
        var coordinates = _transform.GetMoverCoordinates(ent);
        _puddle.TrySpillAt(coordinates, spilled, out _);
    }
}
