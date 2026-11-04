// ReSharper disable CheckNamespace

namespace Content.Shared.Chemistry.Reagent;

public partial class ReagentPrototype
{
    /// <summary>
    /// Fires fed by this reagent ignore the target's damage modifier set, including synthetic and Yautja heat
    /// resistance. Emitted by the reagent generator for the Firepenetrating property and read by
    /// <c>CMUFuelProfileSystem</c> when a flamer tank blend is evaluated.
    /// </summary>
    [DataField]
    public bool FirePenetrating;
}
