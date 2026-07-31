using Robust.Shared.Serialization;

namespace Content.Shared.AU14.ColonyEconomy;

/// <summary>
///     Shared economy status sent to admin, corporate, and ambassador consoles
///     so all three can display the full picture of colony economics.
/// </summary>
[Serializable, NetSerializable]
public sealed class EconomyStatusState
{
    public float SalesTaxPercent { get; }
    public float IncomeTaxPercent { get; }
    public float TransitTariffPercent { get; }
    public List<string> ActiveEmbargoes { get; }
    public List<string> ActiveTradePacts { get; }

    public EconomyStatusState(
        float salesTaxPercent,
        float incomeTaxPercent,
        float transitTariffPercent,
        List<string> activeEmbargoes,
        List<string> activeTradePacts)
    {
        SalesTaxPercent = salesTaxPercent;
        IncomeTaxPercent = incomeTaxPercent;
        TransitTariffPercent = transitTariffPercent;
        ActiveEmbargoes = activeEmbargoes;
        ActiveTradePacts = activeTradePacts;
    }
}

