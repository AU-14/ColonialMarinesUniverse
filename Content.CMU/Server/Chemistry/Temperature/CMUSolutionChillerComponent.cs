namespace Content.Server.CMU14.Chemistry.Temperature;

/// <summary>
/// The cold counterpart of the hotplate's <c>SolutionHeaterComponent</c>: while powered, removes thermal energy
/// from every solution on this entity's <c>ItemPlacer</c>, never taking one below <see cref="MinTemperature"/>.
/// Run by <see cref="CMUSolutionChillerSystem"/>.
/// </summary>
[RegisterComponent, Access(typeof(CMUSolutionChillerSystem))]
public sealed partial class CMUSolutionChillerComponent : Component
{
    /// <summary>
    /// Thermal energy removed from each placed solution per second.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CoolPerSecond = 160f;

    /// <summary>
    /// The chiller never cools a solution below this temperature, and leaves anything already colder alone.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MinTemperature = 250f;
}
