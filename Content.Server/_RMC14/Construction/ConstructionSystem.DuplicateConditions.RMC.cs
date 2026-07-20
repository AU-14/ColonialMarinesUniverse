using Content.Server.Construction.Components;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.DoAfter;

namespace Content.Server.Construction;

public sealed partial class ConstructionSystem
{
    private DuplicateConditions GetRmcDuplicateConditions(
        ToolConstructionGraphStep step,
        ConstructionComponent? construction)
    {
        if (step.DuplicateConditions is { } configured)
            return configured;

        if (construction != null &&
            ProtoMan.TryIndex(construction.Graph, out ConstructionGraphPrototype? graph) &&
            graph.IsCM)
        {
            return DuplicateConditions.None;
        }

        return DuplicateConditions.All;
    }
}
