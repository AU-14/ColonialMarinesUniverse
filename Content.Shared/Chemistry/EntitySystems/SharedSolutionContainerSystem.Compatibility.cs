using System.Diagnostics.CodeAnalysis;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Robust.Shared.Containers;

namespace Content.Shared.Chemistry.EntitySystems;

/// <summary>
/// This exists so that entity prototypes and maps with <see cref="SolutionContainerManagerComponent"/> can load their solutions.
/// This system is extremely simple and will not have compatibility for new features that come as a result of future refactors.
/// This compatibility layer will degrade over time and eventually break as more solution logic relies on well-defined prototypes.
/// This is only here to give you more time to port your solutions to the new system. It is going to be deleted eventually.
/// You have been warned.
/// </summary>
public abstract partial class SharedSolutionContainerSystem
{
    /// <summary>
    /// Compatibility overload for callers that still cache the legacy solution manager component.
    /// </summary>
    public bool TryGetSolution(
        (EntityUid Owner, SolutionContainerManagerComponent? Component) entity,
        string? name,
        [NotNullWhen(true)] out Entity<SolutionComponent>? solutionEnt,
        [NotNullWhen(true)] out Solution? solution,
        bool errorOnMissing = false)
    {
        if (!TryGetSolution(entity, name, out solutionEnt, errorOnMissing))
        {
            solution = null;
            return false;
        }

        solution = solutionEnt.Value.Comp.Solution;
        return true;
    }

    /// <summary>
    /// Compatibility overload for callers that still cache the legacy solution manager component.
    /// </summary>
    public bool TryGetSolution(
        (EntityUid Owner, SolutionContainerManagerComponent? Component) entity,
        string? name,
        [NotNullWhen(true)] out Entity<SolutionComponent>? solutionEnt,
        bool errorOnMissing = false)
    {
        if (name is null)
        {
            if (SolutionQuery.TryComp(entity.Owner, out var solution))
            {
                solutionEnt = (entity.Owner, solution);
                return true;
            }

            solutionEnt = null;
            if (errorOnMissing)
                Log.Error($"{ToPrettyString(entity.Owner)} does not have a solution on itself.");

            return false;
        }

        return TryGetSolution(
            (entity.Owner, (SolutionManagerComponent?) null),
            name,
            out solutionEnt,
            errorOnMissing);
    }

    /// <summary>
    /// Compatibility overload for callers that still cache the legacy solution manager component.
    /// </summary>
    public IEnumerable<(string? Name, Entity<SolutionComponent> Solution)> EnumerateSolutions(
        (EntityUid Owner, SolutionContainerManagerComponent? Component) entity,
        bool includeSelf = true)
    {
        return EnumerateSolutions((entity.Owner, (SolutionManagerComponent?) null), includeSelf);
    }

    public void InitializeContainerManager()
    {
        SubscribeLocalEvent<SolutionContainerManagerComponent, MapInitEvent>(OnSolutionContainerInit);
    }

    private void OnSolutionContainerInit(Entity<SolutionContainerManagerComponent> container, ref MapInitEvent args)
    {
        // Create the manager, this should also create a container, so we ensure it exists.
        EnsureComp<SolutionManagerComponent>(container, out var manager);
        var solutionContainer = ContainerSystem.EnsureContainer<Container>(container, manager.Container);

        // First, if this entity was saved with an entity in a container, try to put it in the SolutionManager
        foreach (var name in container.Comp.Containers)
        {
            if (ContainerSystem.GetContainer(container, $"solution@{name}") is not ContainerSlot slot || slot.ContainedEntity is not { } solutionUid)
                continue;

            if (!SolutionQuery.TryComp(solutionUid, out var solution))
                continue;

            if (TryGetSolution(container.Owner, name, out var solutionEnt))
            {
                // Only a warning so tests don't fail. If you're using this to find maps/prototypes which need porting, change this to Log.Error so tests fail.
                Log.Warning($"Attempted to port a solution id: {name} entity: {ToPrettyString(solutionUid)} " +
                            $"from a {nameof(SolutionContainerManagerComponent)} on {ToPrettyString(container)}, {MetaData(container).EntityPrototype}, " +
                            $"but the entity already had a solution with that id.");
                solutionEnt.Value.Comp.Solution = solution.Solution;
            }
            else
            {
                ContainerSystem.Insert(solutionUid, solutionContainer, force: true);
            }

            // We don't need it anymore
            ContainerSystem.ShutdownContainer(slot);
        }

        if (container.Comp.Solutions == null)
        {
            RemCompDeferred<SolutionContainerManagerComponent>(container);
            return;
        }


        // Next, if this entity was never initialized, create its solutions.
        foreach (var (name, solution) in container.Comp.Solutions)
        {
            // Solution already exists so we ignore it.
            if (EnsureSolution(container.Owner, name, out var solutionEnt))
            {
                // Only a warning so tests don't fail. If you're using this to find maps/prototypes which need porting, change this to Log.Error so tests fail.
                Log.Warning($"Attempted to port a solution id: {name} " +
                            $"from a {nameof(SolutionContainerManagerComponent)} on {ToPrettyString(container)}, {MetaData(container).EntityPrototype}, " +
                            $"but the entity already had a solution with that id.");
            }

            // Clone the solution to the component.
            solutionEnt.Comp.Solution = solution;
        }

        // Clear its data
        container.Comp.Solutions = null;
        RemCompDeferred<SolutionContainerManagerComponent>(container);
    }
}
