using Robust.Shared.Serialization;

namespace Content.Shared.AU14.ColonyEconomy;

[Serializable, NetSerializable]
public enum CorporateConsoleUi
{
    Key,
}

[Serializable, NetSerializable]
public enum CorporateConsoleThirdPartyUi
{
    Key,
}

// ── BUI States ──────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class CorporateConsoleBuiState : BoundUserInterfaceState
{
    /// <summary>Current transit tariff in percent (0–50).</summary>
    public float TransitTariffPercent { get; }

    /// <summary>Current corporate budget balance.</summary>
    public float CorporateBudget { get; }

    public EconomyStatusState EconomyStatus { get; }

    public CorporateConsoleBuiState(float transitTariffPercent, float corporateBudget, EconomyStatusState economyStatus)
    {
        TransitTariffPercent = transitTariffPercent;
        CorporateBudget = corporateBudget;
        EconomyStatus = economyStatus;
    }
}

[Serializable, NetSerializable]
public sealed class CorporateConsoleThirdPartyBuiState : BoundUserInterfaceState
{
    public float Budget { get; }
    public Dictionary<string, (string DisplayName, float Cost)> CallableParties { get; }
    public HashSet<string> CalledParties { get; }

    public CorporateConsoleThirdPartyBuiState(float budget, Dictionary<string, (string DisplayName, float Cost)> callableParties, HashSet<string> calledParties)
    {
        Budget = budget;
        CallableParties = callableParties;
        CalledParties = calledParties;
    }
}

// ── BUI Messages ─────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class CorporateConsoleSetTariffBuiMsg : BoundUserInterfaceMessage
{
    /// <summary>New transit tariff in percent (0–50).</summary>
    public float TariffPercent { get; }
    public CorporateConsoleSetTariffBuiMsg(float tariffPercent) { TariffPercent = tariffPercent; }
}

[Serializable, NetSerializable]
public sealed class CorporateConsoleOpenThirdPartyBuiMsg : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class CorporateConsoleCallThirdPartyBuiMsg : BoundUserInterfaceMessage
{
    public string ThirdPartyId { get; }
    public CorporateConsoleCallThirdPartyBuiMsg(string thirdPartyId) { ThirdPartyId = thirdPartyId; }
}

[Serializable, NetSerializable]
public sealed class CorporateConsoleWithdrawBuiMsg : BoundUserInterfaceMessage
{
    public float Amount { get; }
    public CorporateConsoleWithdrawBuiMsg(float amount) { Amount = amount; }
}

