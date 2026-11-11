using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ColonyEconomy;

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

    // RUMC: the tax on the parcels sold through the trade platforms
    /// <summary>Current tax on the parcels sold through the trade platforms, in percent (0–50).</summary>
    public float TradeTaxPercent { get; }

    /// <summary>Current colony budget balance.</summary>
    public float ColonyBudget { get; }

    public EconomyStatusState EconomyStatus { get; }

    // RUMC: the constructor had no trade tax (kept): public AdminConsoleBuiState(float salesTaxPercent, float incomeTaxPercent, float colonyBudget, EconomyStatusState economyStatus)
    public AdminConsoleBuiState(float salesTaxPercent, float incomeTaxPercent, float colonyBudget, EconomyStatusState economyStatus, float tradeTaxPercent = 0f)
    {
        SalesTaxPercent = salesTaxPercent;
        IncomeTaxPercent = incomeTaxPercent;
        TradeTaxPercent = tradeTaxPercent; // RUMC
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

    /// <summary>How many more third parties can be called in this round.</summary>
    public int CallsLeft { get; init; }

    /// <summary>When the next one can be called in.</summary>
    public TimeSpan NextCallInAt { get; init; }

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

// RUMC message: the console sets the tax on the parcels sold through the trade platforms
[Serializable, NetSerializable]
public sealed class AdminConsoleSetTradeTaxBuiMsg : BoundUserInterfaceMessage
{
    /// <summary>New tax on the parcels sold through the trade platforms, in percent (0–50).</summary>
    public float TaxPercent { get; }
    public AdminConsoleSetTradeTaxBuiMsg(float taxPercent) { TaxPercent = taxPercent; }
}

// RUMC message: the console opens the window of the government of the colony (the reforms and the foreign policy)
[Serializable, NetSerializable]
public sealed class AdminConsoleOpenGovernmentBuiMsg : BoundUserInterfaceMessage
{
}
