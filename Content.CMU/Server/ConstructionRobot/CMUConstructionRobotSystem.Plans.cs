using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Construction.Completions;
using Content.Server.Construction.Components;
using Content.Shared._RMC14.Prototypes;
using Content.Shared.Construction;
using Content.Shared.Construction.NodeEntities;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ConstructionRobot;

public sealed partial class CMUConstructionRobotSystem
{
    private readonly Dictionary<string, CachedRecipe> _recipeCache = new();

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs ev)
    {
        _recipeCache.Clear();
    }

    private bool TryGetBuildPlan(string recipeId, [NotNullWhen(true)] out ConstructionRobotBuildPlan? plan)
    {
        plan = null;
        if (!_prototypes.TryCM<ConstructionPrototype>(recipeId, out var recipe) ||
            recipe.Hide || recipe.Type != ConstructionType.Structure || recipe.RMCPrototype != null ||
            !_prototypes.TryIndex(recipe.Graph, out ConstructionGraphPrototype? graph) ||
            !graph.Nodes.TryGetValue(recipe.TargetNode, out var target) ||
            target.Entity is not StaticNodeEntity { Id: { } targetId } ||
            !_prototypes.TryIndex(targetId, out EntityPrototype? output))
            return false;

        // The in-game construction editor can replace prototypes directly between reload events.
        if (!_recipeCache.TryGetValue(recipeId, out var cached) ||
            cached.Recipe != recipe || cached.Graph != graph || cached.Output != output)
        {
            plan = CompileBuildPlan(recipe, graph, output);
            _recipeCache[recipeId] = new CachedRecipe(recipe, graph, output, plan);
        }
        else
        {
            plan = cached.Plan;
        }

        return plan != null;
    }

    private ConstructionRobotBuildPlan? CompileBuildPlan(ConstructionPrototype recipe, ConstructionGraphPrototype graph,
        EntityPrototype output)
    {
        // Construction startup initializes this node and performs its actions once.
        if (!output.TryComp<ConstructionComponent>(out var construction, EntityManager.ComponentFactory) ||
            construction.Graph != recipe.Graph || construction.Node != recipe.TargetNode ||
            !graph.Nodes.TryGetValue(recipe.StartNode, out var current) ||
            !graph.TryPath(recipe.StartNode, recipe.TargetNode, out var path) || path.Length == 0)
            return null;

        var materials = new Dictionary<string, int>();
        var delay = 0f;
        var fuel = 0f;
        var prefix = new HashSet<string> { current.Name };
        IReadOnlyList<IGraphAction> completions = Array.Empty<IGraphAction>();
        foreach (var next in path)
        {
            var final = next.Name == recipe.TargetNode;
            var edge = current.GetEdge(next.Name);
            if (edge == null || edge.Conditions.Count != 0 || next.TransformLogic.Length != 0 ||
                (!final && (next.Actions.Count != 0 || edge.Completed.Count != 0 ||
                            next.Entity is not (NullNodeEntity or StaticNodeEntity))) ||
                HasAlternateRoute(graph, current, edge, recipe.TargetNode, prefix))
                return null;

            // Barricade recipes snap the finished structure to the grid after spawning it.
            if (final)
            {
                if (edge.Completed.Any(action => action is not SnapToGrid))
                    return null;

                completions = edge.Completed;
            }

            foreach (var step in edge.Steps)
            {
                if (step.Completed.Count != 0)
                    return null;

                delay += step.DoAfter;
                switch (step)
                {
                    case MaterialConstructionGraphStep { Store.Length: 0 } material:
                        var type = material.MaterialPrototypeId.Id;
                        materials[type] = materials.GetValueOrDefault(type) + material.Amount;
                        break;
                    case ToolConstructionGraphStep tool when tool.Tool.Id == "Welding":
                        fuel += tool.Fuel;
                        break;
                    case ToolConstructionGraphStep tool when tool.Tool.Id is "Anchoring" or "Prying" or "Screwing" or "Cutting":
                        break;
                    default:
                        return null;
                }
            }

            prefix.Add(next.Name);
            current = next;
        }

        return new ConstructionRobotBuildPlan(recipe, output.ID, materials, fuel,
            TimeSpan.FromSeconds(Math.Max(1f, delay)), completions);
    }

    private static bool HasAlternateRoute(ConstructionGraphPrototype graph, ConstructionGraphNode current,
        ConstructionGraphEdge selected, string target, HashSet<string> prefix)
    {
        if (current.Edges.Count == 1)
            return false;

        // An alternate simple route must first diverge somewhere on the selected path.
        // Search each branch point once without walking back through the common prefix.
        var visited = new HashSet<string>(prefix);
        var pending = new Stack<string>();
        foreach (var edge in current.Edges)
        {
            if (edge != selected && visited.Add(edge.Target))
                pending.Push(edge.Target);
        }

        while (pending.TryPop(out var id))
        {
            if (id == target)
                return true;

            foreach (var edge in graph.Nodes[id].Edges)
            {
                if (visited.Add(edge.Target))
                    pending.Push(edge.Target);
            }
        }

        return false;
    }

    private sealed record CachedRecipe(ConstructionPrototype Recipe, ConstructionGraphPrototype Graph,
        EntityPrototype Output, ConstructionRobotBuildPlan? Plan);

    private sealed record ConstructionRobotBuildPlan(ConstructionPrototype Prototype, string TargetPrototype,
        Dictionary<string, int> Materials, float WeldingFuel, TimeSpan BuildDelay, IReadOnlyList<IGraphAction> Completions);
}
