using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Ordnance;

/// <summary>
///     Data-driven assembly recipe that combines a fueled shell/tube with an armed chemical payload casing.
/// </summary>
[RegisterComponent]
public sealed partial class RMCOrdnancePayloadAssemblyComponent : Component
{
    /// <summary>
    ///     Fuel solution on the shell or rocket body that must be filled before assembly.
    /// </summary>
    [DataField]
    public string FuelSolution = "fuel";

    /// <summary>
    ///     Chemical solution on the final assembled ordnance entity.
    /// </summary>
    [DataField]
    public string ChemicalSolution = "chemicals";

    /// <summary>
    ///     Minimum amount of fuel required to allow payload assembly.
    /// </summary>
    [DataField]
    public FixedPoint2 RequiredFuel = 60;

    /// <summary>
    ///     Optional specific fuel reagent that must satisfy <see cref="RequiredFuel"/>.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype>? RequiredFuelReagent;

    /// <summary>
    ///     Payload-to-result prototype mapping for this shell or rocket body.
    /// </summary>
    [DataField(required: true)]
    public List<RMCOrdnancePayloadAssemblyResult> Results = new();
}

/// <summary>
///     Single payload recipe entry for <see cref="RMCOrdnancePayloadAssemblyComponent"/>.
/// </summary>
[DataDefinition]
public sealed partial class RMCOrdnancePayloadAssemblyResult
{
    /// <summary>
    ///     Prototype ID of the armed payload casing that may be inserted.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Payload = default!;

    /// <summary>
    ///     Prototype ID of the assembled shell or rocket produced by the recipe.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Result = default!;
}
