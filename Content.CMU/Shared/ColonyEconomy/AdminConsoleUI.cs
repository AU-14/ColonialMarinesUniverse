using Robust.Shared.Serialization;

namespace Content.Shared.AU14.ColonyEconomy;

[Serializable, NetSerializable]
public enum AdminConsoleUi
{
    Key,
}

[Serializable, NetSerializable]
public enum AdminConsoleThirdPartyUi
{
    Key,
}

// ── BUI States ──────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class AdminConsoleBuiState : BoundUserInterfaceState
{
    /// <summary>Current sales tax in percent (0–50).</summary>
    public float SalesTaxPercent { get; }

    /// <summary>Current income tax in percent (0–50).</summary>
    public float IncomeTaxPercent { get; }

    /// <summary>Current colony budget balance.</summary>
    public float ColonyBudget { get; }

    public EconomyStatusState EconomyStatus { get; }

    public AdminConsoleBuiState(float salesTaxPercent, float incomeTaxPercent, float colonyBudget, EconomyStatusState economyStatus)
    {
        SalesTaxPercent = salesTaxPercent;
        IncomeTaxPercent = incomeTaxPercent;
        ColonyBudget = colonyBudget;
        EconomyStatus = economyStatus;
    }
}

[Serializable, NetSerializable]
public sealed class AdminConsoleThirdPartyBuiState : BoundUserInterfaceState
{
    public float Budget { get; }
    public Dictionary<string, (string DisplayName, float Cost)> CallableParties { get; }
    public HashSet<string> CalledParties { get; }

    public AdminConsoleThirdPartyBuiState(float budget, Dictionary<string, (string DisplayName, float Cost)> callableParties, HashSet<string> calledParties)
    {
        Budget = budget;
        CallableParties = callableParties;
        CalledParties = calledParties;
    }
}

// ── BUI Messages ─────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class AdminConsoleSetTaxBuiMsg : BoundUserInterfaceMessage
{
    /// <summary>New sales tax in percent (0–50).</summary>
    public float TaxPercent { get; }
    public AdminConsoleSetTaxBuiMsg(float taxPercent) { TaxPercent = taxPercent; }
}

[Serializable, NetSerializable]
public sealed class AdminConsoleSetIncomeTaxBuiMsg : BoundUserInterfaceMessage
{
    /// <summary>New income tax in percent (0–50).</summary>
    public float TaxPercent { get; }
    public AdminConsoleSetIncomeTaxBuiMsg(float taxPercent) { TaxPercent = taxPercent; }
}

[Serializable, NetSerializable]
public sealed class AdminConsoleOpenThirdPartyBuiMsg : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class AdminConsoleCallThirdPartyBuiMsg : BoundUserInterfaceMessage
{
    public string ThirdPartyId { get; }
    public AdminConsoleCallThirdPartyBuiMsg(string thirdPartyId) { ThirdPartyId = thirdPartyId; }
}
