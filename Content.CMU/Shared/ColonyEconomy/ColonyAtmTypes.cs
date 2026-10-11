using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ColonyEconomy;

/// <summary>
///     Screens/states the ATM can be in. Drives the UI layout sent to the client.
/// </summary>
[Serializable, NetSerializable]
public enum AtmScreen : byte
{
    Welcome,
    PinEntry,
    PinLocked,
    MainMenu,
    Withdraw,
    WithdrawConfirm,
    Deposit,
    DepositAmount,
    RemoteDeposit,
    RemoteDepositAccountNum,
    RemoteDepositAmount,
    RemoteDepositConfirm,
    Transfer,
    TransferAccountNum,
    TransferAmount,
    TransferConfirm,
    Result,
    History,
}

/// <summary>
///     One of the 6 side buttons on the ATM UI (3 left, 3 right).
/// </summary>
[Serializable, NetSerializable]
public enum AtmSideButton : byte
{
    L1, L2, L3,
    R1, R2, R3,
}

/// <summary>
///     An account login cached by an ATM, which a siphon rig can leak.
/// </summary>
[Serializable, NetSerializable]
public sealed class SkimmedAccount
{
    public int AccountNumber;
    public string Name = string.Empty;
    public int Pin;
}

/// <summary>
///     What moved money in or out of an account, as listed on the ATM's history screen.
/// </summary>
[Serializable, NetSerializable]
public enum AtmHistoryKind : byte
{
    Withdrawal,
    Deposit,
    /// <summary>Cash paid in to this account by someone at an ATM, with or without a card.</summary>
    CashDeposit,
    TransferOut,
    TransferIn,
    /// <summary>Cash paid out but never taken, drawn back into the machine and paid back in.</summary>
    Retracted,
    /// <summary>Paid at a vending machine with the card, for whatever inserted cash didn't cover.</summary>
    Purchase,
}

/// <summary>
///     One line of an account's history. <see cref="Amount"/> is always positive; the kind says
///     which way it went. <see cref="OtherAccount"/> is the other side of a transfer, otherwise 0.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct ColonyAccountHistoryEntry(TimeSpan Time, AtmHistoryKind Kind, int Amount, int OtherAccount);
