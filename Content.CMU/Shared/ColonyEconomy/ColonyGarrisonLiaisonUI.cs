using Robust.Shared.Serialization;

namespace Content.Shared.CMU14;

/// <summary>
///     The Colony Garrison Liaison Office's screen. The messages it sends are the colony comms console's own
///     (<see cref="ColonyCommsConsoleSendMessageBuiMsg"/>), so the announcement itself is handled by the same system.
/// </summary>
[Serializable, NetSerializable]
public enum ColonyGarrisonLiaisonUi
{
    Key,
}

/// <summary>What the screen needs: how long the announcement may be, and when the office can send again.</summary>
[Serializable, NetSerializable]
public sealed class ColonyGarrisonLiaisonBuiState(int maxLength, TimeSpan nextSendAt, TimeSpan cooldown) : BoundUserInterfaceState
{
    public readonly int MaxLength = maxLength;
    public readonly TimeSpan NextSendAt = nextSendAt;
    public readonly TimeSpan Cooldown = cooldown;
}

/// <summary>
///     Pays the colony's budget out of the garrison's own requisitions account. It only goes one way: getting it back
///     is the colony's decision, made at the Budget Console.
/// </summary>
[Serializable, NetSerializable]
public sealed class ColonyGarrisonLiaisonTransferBuiMsg(int amount) : BoundUserInterfaceMessage
{
    public readonly int Amount = amount;
}

/// <summary>
///     Both balances, sent to each person using the office on their own: the garrison account is the one of their
///     faction, and someone with no garrison faction has none.
/// </summary>
[Serializable, NetSerializable]
public sealed class ColonyGarrisonLiaisonFundsBuiMsg(bool hasGarrison, int garrisonFunds, int colonyFunds) : BoundUserInterfaceMessage
{
    public readonly bool HasGarrison = hasGarrison;
    public readonly int GarrisonFunds = garrisonFunds;
    public readonly int ColonyFunds = colonyFunds;
}
