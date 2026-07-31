namespace Content.Shared.AU14.ColonyEconomy;

/// <summary>
///     Corporate console: manages transit tariffs and can call third parties.
///     Third-party costs are paid from the corporate budget.
///     TransitTariffPercent and CorporateBudget are stored on the component;
///     CorporateConsoleSystem keeps all consoles in sync.
/// </summary>
[RegisterComponent]
public sealed partial class CorporateConsoleComponent : Component
{
    /// <summary>
    ///     Current transit tariff in percent (0–50).
    ///     This portion of every submission storage payout goes to the corporate budget.
    /// </summary>
    [DataField("transitTariffPercent")]
    public float TransitTariffPercent = 0f;

    /// <summary>
    ///     The corporate department's cash balance.
    ///     Fed by transit tariffs; spent on third-party calls or cash withdrawals.
    /// </summary>
    public float CorporateBudget = 0f;

    /// <summary>
    ///     Map of third-party prototype ID → cost deducted from corporate budget.
    /// </summary>
    [DataField("callableParties")]
    public Dictionary<string, float> CallableParties = new();

    public HashSet<string> CalledParties = new();
}
