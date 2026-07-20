using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Shared.Body.Systems;

public sealed partial class StomachSystem : EntitySystem
{
    public sealed partial class StomachSystem : EntitySystem
    {
        [Dependency] private IGameTiming _gameTiming = default!;
        [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;

    public bool TryTransferSolution(Entity<StomachComponent?, SolutionManagerComponent?> entity, Solution solution)
    {
        if (!Resolve(entity, ref entity.Comp1, logMissing: false)
            || !_solutionContainerSystem.ResolveSolution((entity, entity.Comp2), DefaultSolutionName, ref entity.Comp1.Solution)
            || !CanTransferSolution(entity, solution))
            return false;

        _solutionContainerSystem.TryAddSolution(entity.Comp1.Solution.Value, solution);
        return true;
    }
}
