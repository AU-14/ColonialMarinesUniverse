// Content.Shared/CMU14/ColonyEconomy/SubmissionStorageComponent.cs
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.ColonyEconomy;

[RegisterComponent, NetworkedComponent]
public sealed partial class ColonyAtmComponent : Component
{
    /// <summary>
    ///     The ID card entity that was swiped on this ATM.
    ///     Set when a player uses an ID card on the machine, cleared when the UI closes.
    /// </summary>
    public EntityUid? SwipedCard;

    /// <summary>
    ///     Whether the correct PIN has been entered for the current session.
    /// </summary>
    public bool PinAuthenticated;

    /// <summary>
    ///     Current ATM screen/state.
    /// </summary>
    public AtmScreen Screen = AtmScreen.Welcome;

    /// <summary>
    ///     Text the player has typed on the keypad so far.
    /// </summary>
    public string KeypadBuffer = string.Empty;

    /// <summary>
    ///     Status message shown on the current screen.
    /// </summary>
    public string StatusMessage = string.Empty;

    /// <summary>
    ///     For Transfer flow: target account number entered by user.
    /// </summary>
    public int PendingTransferTarget;

    /// <summary>
    ///     For Transfer flow: amount staged for confirmation.
    /// </summary>
    public int PendingAmount;

    /// <summary>
    ///     For Remote Deposit flow: target account number.
    /// </summary>
    public int RemoteDepositTarget;

    /// <summary>
    ///     History screen: how many of the newest entries are scrolled past.
    /// </summary>
    public int HistoryOffset;

    /// <summary>
    ///     Who is operating the ATM right now (for forensics / deposits).
    /// </summary>
    public EntityUid? CurrentUser;

    /// <summary>
    ///     The most recent card logins at this ATM, with the PIN that was typed (newest last).
    ///     Server-only; a sapper's siphon rig dumps and wipes this list when it hacks the machine.
    /// </summary>
    public List<SkimmedAccount> RecentLogins = new();
}
