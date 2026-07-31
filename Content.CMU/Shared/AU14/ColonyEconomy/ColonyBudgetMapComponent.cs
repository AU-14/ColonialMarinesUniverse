using Robust.Shared.GameStates;

namespace Content.Shared.AU14.ColonyEconomy;

/// <summary>
///     Place this component on a map entity (e.g. the planet map) to set the starting colony budget.
///     The ColonyBudgetSystem reads it on MapInit to initialize the colony treasury.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ColonyBudgetMapComponent : Component
{
    /// <summary>
    ///     The starting colony budget for this map.
    /// </summary>
    [DataField("budget")]
    public float Budget;
}

