using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;

namespace Content.Shared.Body.Components;

public sealed partial class BloodstreamComponent
{
    public const string DefaultChemicalsSolutionName = "chemicals";

    /// <summary>
    /// Maximum capacity of the legacy RMC chemical stream.
    /// </summary>
    [DataField]
    public FixedPoint2 ChemicalMaxVolume = FixedPoint2.New(250);

    /// <summary>
    /// Name of the separate solution used by RMC injection and metabolism code.
    /// </summary>
    [DataField]
    public string ChemicalSolutionName = DefaultChemicalsSolutionName;

    /// <summary>
    /// Cached entity for the RMC chemical stream.
    /// </summary>
    [ViewVariables]
    public Entity<SolutionComponent>? ChemicalSolution;
}
