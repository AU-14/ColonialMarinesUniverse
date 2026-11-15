using Content.Shared.CMU14.Fluids;

namespace Content.Client.CMU14.Fluids;

/// <summary>Client drawing data for pipes with machine connection markers.</summary>
[RegisterComponent]
public sealed partial class CMUFluidPipeFlowComponent : Component
{
    public List<CMUFluidPipeArrow> Arrows = new();
}
