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
