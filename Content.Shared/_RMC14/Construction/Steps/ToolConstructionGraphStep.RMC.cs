using Content.Shared.DoAfter;

namespace Content.Shared.Construction.Steps;

public sealed partial class ToolConstructionGraphStep
{
    [DataField]
    public DuplicateConditions? DuplicateConditions { get; private set; }
}
