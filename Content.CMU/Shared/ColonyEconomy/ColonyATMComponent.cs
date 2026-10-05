// Content.Shared/CMU14/ColonyEconomy/SubmissionStorageComponent.cs
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ColonyEconomy;

[RegisterComponent, NetworkedComponent]
public sealed partial class ColonyAtmComponent : Component
{
    /// <summary>
    ///     Container the ATM keeps an inserted ID card in for the whole session. The card stays in the
    ///     machine until it is ejected, including after its owner walks away from the screen.
    /// </summary>
    public const string CardSlotId = "colony_atm_card";

    /// <summary>Digits in a card PIN.</summary>
    public const int PinLength = 4;

    /// <summary>Longest amount or account number the keypad accepts.</summary>
    public const int MaxAmountDigits = 9;

    /// <summary>
    ///     Who pushed the card currently in the slot into it. They get it back instantly from the
    ///     screen; anyone else - or them, once they have walked away - has to pull it out.
    /// </summary>
    public EntityUid? CardInsertedBy;

    /// <summary>When the current card went in, so the operator's screen can play the insertion once.</summary>
    public TimeSpan? CardInsertedAt;

    /// <summary>When cash last came out of the dispenser.</summary>
    public TimeSpan? CashDispensedAt;

    /// <summary>When cash was last fed into the machine.</summary>
    public TimeSpan? CashDepositedAt;

    /// <summary>How many dollars the last of those two moved, so the slot shows a wad that thick.</summary>
    public int CashAmount;

    /// <summary>How long pulling out a card takes for anyone but its inserter at the screen.</summary>
    [DataField]
    public TimeSpan TakeCardDelay = TimeSpan.FromSeconds(2);

    // The machine's own sounds: the card drawn in and pushed out, and the note counter, whose run the
    // cash slot's animation is timed to (atm_pixel_art.py reads atm_cash.wav for that).
    [DataField]
    public SoundSpecifier InsertSound = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_card_in.wav");

    [DataField]
    public SoundSpecifier EjectSound = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_card_out.wav");

    [DataField]
    public SoundSpecifier DispenseSound = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_cash.wav");

    [DataField]
    public SoundSpecifier DepositSound = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_cash.wav");

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

/// <summary>Someone other than the card's inserter finished pulling it out of the ATM.</summary>
[Serializable, NetSerializable]
public sealed partial class ColonyAtmTakeCardDoAfterEvent : SimpleDoAfterEvent;
