using Robust.Shared.Serialization;

namespace Content.Shared.AU14.Ambassador;

[Serializable, NetSerializable]
public enum AmbassadorConsoleUi
{
    Key,
}

[Serializable, NetSerializable]
public enum AmbassadorThirdPartyUi
{
    Key,
}

// ---- BUI State ----

[Serializable, NetSerializable]
public sealed class AmbassadorConsoleBuiState : BoundUserInterfaceState
{
    public float Budget { get; }
    public bool EmbargoActive { get; }
    public bool TradePactActive { get; }
    public bool CommsJamActive { get; }
    public bool SignalBoostActive { get; }
    public bool SignalJamActive { get; }
    public List<string> RadarList { get; }
    public Content.Shared.AU14.ColonyEconomy.EconomyStatusState EconomyStatus { get; }
    public string FactionName { get; }

    // Prices
    public float EmbargoCostPerMinute { get; }
    public float TradePactCostPerMinute { get; }
    public float CommsJamCostPerMinute { get; }
    public float SignalBoostCostPerMinute { get; }
    public float SignalJamCostPerMinute { get; }
    public float BroadcastCost { get; }
    public float RadarScanCost { get; }

    public AmbassadorConsoleBuiState(
        float budget,
        bool embargoActive,
        bool tradePactActive,
        bool commsJamActive,
        bool signalBoostActive,
        bool signalJamActive,
        List<string> radarList,
        Content.Shared.AU14.ColonyEconomy.EconomyStatusState economyStatus,
        string factionName,
        float embargoCostPerMinute,
        float tradePactCostPerMinute,
        float commsJamCostPerMinute,
        float signalBoostCostPerMinute,
        float signalJamCostPerMinute,
        float broadcastCost,
        float radarScanCost)
    {
        Budget = budget;
        EmbargoActive = embargoActive;
        TradePactActive = tradePactActive;
        CommsJamActive = commsJamActive;
        SignalBoostActive = signalBoostActive;
        SignalJamActive = signalJamActive;
        RadarList = radarList;
        EconomyStatus = economyStatus;
        FactionName = factionName;
        EmbargoCostPerMinute = embargoCostPerMinute;
        TradePactCostPerMinute = tradePactCostPerMinute;
        CommsJamCostPerMinute = commsJamCostPerMinute;
        SignalBoostCostPerMinute = signalBoostCostPerMinute;
        SignalJamCostPerMinute = signalJamCostPerMinute;
        BroadcastCost = broadcastCost;
        RadarScanCost = radarScanCost;
    }
}

/// <summary>
/// Separate UI state for third party calling window.
/// Contains display names and costs.
/// </summary>
[Serializable, NetSerializable]
public sealed class AmbassadorThirdPartyBuiState : BoundUserInterfaceState
{
    public float Budget { get; }
    public Dictionary<string, (string DisplayName, float Cost)> CallableParties { get; }
    public HashSet<string> CalledParties { get; }

    public AmbassadorThirdPartyBuiState(float budget, Dictionary<string, (string DisplayName, float Cost)> callableParties, HashSet<string> calledParties)
    {
        Budget = budget;
        CallableParties = callableParties;
        CalledParties = calledParties;
    }
}

// ---- BUI Messages ----

[Serializable, NetSerializable]
public sealed class AmbassadorOpenThirdPartyBuiMsg : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class AmbassadorDepositBuiMsg : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class AmbassadorWithdrawBuiMsg : BoundUserInterfaceMessage
{
    public float Amount { get; }
    public AmbassadorWithdrawBuiMsg(float amount) { Amount = amount; }
}

[Serializable, NetSerializable]
public sealed class AmbassadorCallThirdPartyBuiMsg : BoundUserInterfaceMessage
{
    public string ThirdPartyId { get; }
    public AmbassadorCallThirdPartyBuiMsg(string thirdPartyId) { ThirdPartyId = thirdPartyId; }
}

[Serializable, NetSerializable]
public sealed class AmbassadorToggleEmbargoBuiMsg : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class AmbassadorToggleSignalBoostBuiMsg : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class AmbassadorToggleSignalJamBuiMsg : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class AmbassadorBroadcastBuiMsg : BoundUserInterfaceMessage
{
    public string Message { get; }
    public AmbassadorBroadcastBuiMsg(string message) { Message = message; }
}

[Serializable, NetSerializable]
public sealed class AmbassadorToggleTradePactBuiMsg : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class AmbassadorToggleCommsJamBuiMsg : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class AmbassadorScanRadarBuiMsg : BoundUserInterfaceMessage
{
}

