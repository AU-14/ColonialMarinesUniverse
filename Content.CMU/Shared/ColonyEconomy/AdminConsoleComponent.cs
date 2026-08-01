namespace Content.Shared.AU14.ColonyEconomy;

/// <summary>
///     Admin console: manages colony sales tax, income tax, and can call third parties.
///     Third-party costs are paid from the colony budget.
/// </summary>
[RegisterComponent]
public sealed partial class AdminConsoleComponent : Component
{
    /// <summary>
    ///     Current colony-wide sales tax in percent (0–50).
    ///     Applied to cash vendors and corporate ASRS orders. Revenue goes to colony budget.
    /// </summary>
    [DataField("salesTaxPercent")]
    public float SalesTaxPercent = 0f;

    /// <summary>
    ///     Current colony-wide income tax in percent (0–50).
    ///     Applied to salary payouts and corporate console cash withdrawals. Revenue goes to colony budget.
    /// </summary>
    [DataField("incomeTaxPercent")]
    public float IncomeTaxPercent = 0f;

    /// <summary>
    ///     Map of third-party prototype ID → cost deducted from colony budget.
    /// </summary>
    [DataField("callableParties")]
    public Dictionary<string, float> CallableParties = new();

    public HashSet<string> CalledParties = new();
}
