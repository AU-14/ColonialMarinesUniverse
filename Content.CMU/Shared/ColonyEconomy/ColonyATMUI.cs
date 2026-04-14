using Robust.Shared.Serialization;

namespace Content.Shared.AU14.ColonyEconomy;

[Serializable, NetSerializable]
public enum ColonyAtmUi
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ColonyAtmWithdrawBuiMsg : BoundUserInterfaceMessage
{
    public int Amount { get; }

    public ColonyAtmWithdrawBuiMsg(int amount)
    {
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public sealed class ColonyAtmBuiState : BoundUserInterfaceState
{
    public int Balance { get; }
    public string OwnerName { get; }

    public ColonyAtmBuiState(int balance, string ownerName)
    {
        Balance = balance;
        OwnerName = ownerName;
    }
}

