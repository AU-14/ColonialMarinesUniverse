using Content.Shared.Atmos;
using Robust.Shared.Analyzers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Atmos;

/// <summary>
///     Consumes gases from its pipe inlet and mints solid stack outputs when a recipe's
///     temperature window and gas amounts are met. Ratio-sensitive refinement lives in
///     the gas stage (gas reactions).
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class GasCrystallizerComponent : Component
{
    /// <summary>Pipe node the machine 'drinks' from</summary>
    [DataField]
    public string Inlet = "pipe";

    /// <summary>Seconds between successful batches</summary>
    [DataField]
    public float BatchDelay = 5f;

    [DataField, AutoPausedField]
    public TimeSpan NextBatch;

    [DataField(required: true)]
    public List<GasCrystallizerRecipe> Recipes = new();
}

[DataDefinition]
public sealed partial class GasCrystallizerRecipe
{
    /// <summary>Gas moles consumed per batch</summary>
    [DataField(required: true)]
    public Dictionary<Gas, float> Gases = new();

    [DataField]
    public float MinTemperature = Atmospherics.TCMB;

    [DataField]
    public float MaxTemperature = float.PositiveInfinity;

    [DataField(required: true)]
    public EntProtoId Output;

    [DataField]
    public int OutputAmount = 1;
}
