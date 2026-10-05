using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ColonyEconomy;

// ── UI Key ────────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public enum ColonyAtmUi
{
    Key,
}

// ── BUI State (server → client) ──────────────────────────────────────────

/// <summary>
///     Full ATM display state sent to every client that has the UI open.
///     The client renders exactly what the server tells it.
/// </summary>
[Serializable, NetSerializable]
public sealed class ColonyAtmBuiState : BoundUserInterfaceState
{
    public AtmScreen Screen { get; }
    public int Balance { get; }
    public string OwnerName { get; }
    public int AccountNumber { get; }
    public float IncomeTaxPercent { get; }
    public TimeSpan? LockExpiry { get; }
    public string StatusMessage { get; }
    public string KeypadBuffer { get; }

    /// <summary>Left side button labels (indices 0-2 = L1, L2, L3). Empty string hides the button.</summary>
    public string[] LeftLabels { get; }
    /// <summary>Right side button labels (indices 0-2 = R1, R2, R3). Empty string hides the button.</summary>
    public string[] RightLabels { get; }

    /// <summary>Whether the ATM has been tampered with by a siphon rig (shows the tampered card reader art).</summary>
    public bool Tampered { get; }

    /// <summary>
    ///     One page of the account's history, newest first. Only sent while the PIN-unlocked history screen is open.
    /// </summary>
    public ColonyAccountHistoryEntry[] History { get; }

    /// <summary>How many newer entries come before this page.</summary>
    public int HistoryOffset { get; }

    /// <summary>How many entries the account's history holds in total.</summary>
    public int HistoryTotal { get; }

    /// <summary>Whether a card is sitting in the reader.</summary>
    public bool CardInserted { get; }

    /// <summary>Prototype of the card in the reader, so the screen can show that very card going in.</summary>
    public string? CardPrototype { get; }

    /// <summary>
    ///     A sapper's siphon rig has seized the machine: it shows the CLF's mark instead of the
    ///     terminal and takes no input until it repairs itself.
    /// </summary>
    public bool OutOfService { get; }

    /// <summary>
    ///     When the card in the reader went in, and when cash last came out or went in. The screen
    ///     plays each of those once if it is recent; one opened later shows the card already seated.
    /// </summary>
    public TimeSpan? CardInsertedAt { get; }

    public TimeSpan? CashDispensedAt { get; }

    public TimeSpan? CashDepositedAt { get; }

    /// <summary>How many dollars came out or went in last; the slot shows more notes for more money.</summary>
    public int CashAmount { get; }

    public ColonyAtmBuiState(
        AtmScreen screen,
        int balance,
        string ownerName,
        int accountNumber,
        float incomeTaxPercent,
        TimeSpan? lockExpiry,
        string statusMessage,
        string keypadBuffer,
        string[] leftLabels,
        string[] rightLabels,
        bool tampered = false,
        ColonyAccountHistoryEntry[]? history = null,
        int historyOffset = 0,
        int historyTotal = 0,
        bool cardInserted = false,
        TimeSpan? cardInsertedAt = null,
        TimeSpan? cashDispensedAt = null,
        TimeSpan? cashDepositedAt = null,
        string? cardPrototype = null,
        bool outOfService = false,
        int cashAmount = 0)
    {
        Screen = screen;
        Balance = balance;
        OwnerName = ownerName;
        AccountNumber = accountNumber;
        IncomeTaxPercent = incomeTaxPercent;
        LockExpiry = lockExpiry;
        StatusMessage = statusMessage;
        KeypadBuffer = keypadBuffer;
        LeftLabels = leftLabels;
        RightLabels = rightLabels;
        Tampered = tampered;
        History = history ?? Array.Empty<ColonyAccountHistoryEntry>();
        HistoryOffset = historyOffset;
        HistoryTotal = historyTotal;
        CardInserted = cardInserted;
        CardInsertedAt = cardInsertedAt;
        CashDispensedAt = cashDispensedAt;
        CashDepositedAt = cashDepositedAt;
        CardPrototype = cardPrototype;
        OutOfService = outOfService;
        CashAmount = cashAmount;
    }
}

// ── BUI Messages (client → server) ───────────────────────────────────────

/// <summary>Player pressed one of the 6 side buttons.</summary>
[Serializable, NetSerializable]
public sealed class ColonyAtmSideButtonBuiMsg : BoundUserInterfaceMessage
{
    public AtmSideButton Button { get; }
    public ColonyAtmSideButtonBuiMsg(AtmSideButton button) => Button = button;
}

/// <summary>Player pressed a digit key on the numpad (value "0"-"9").</summary>
[Serializable, NetSerializable]
public sealed class ColonyAtmDigitBuiMsg : BoundUserInterfaceMessage
{
    public string Digit { get; }
    public ColonyAtmDigitBuiMsg(string digit) => Digit = digit;
}

/// <summary>Player pressed the CLEAR (yellow) key: rubs out the last digit typed.</summary>
[Serializable, NetSerializable]
public sealed class ColonyAtmBackspaceBuiMsg : BoundUserInterfaceMessage { }

/// <summary>Player pressed the ENTER (green) key.</summary>
[Serializable, NetSerializable]
public sealed class ColonyAtmConfirmBuiMsg : BoundUserInterfaceMessage { }

/// <summary>Player clicked the empty card reader: put their ID card in, from hand or from where they wear it.</summary>
[Serializable, NetSerializable]
public sealed class ColonyAtmInsertCardBuiMsg : BoundUserInterfaceMessage { }

/// <summary>Player clicked their card in the reader while signed in: log off and hand the card back.</summary>
[Serializable, NetSerializable]
public sealed class ColonyAtmEjectCardBuiMsg : BoundUserInterfaceMessage { }

/// <summary>
///     Client → server, once the screen exists: which card is mine? Answered with
///     <see cref="ColonyAtmOwnCardMsg"/> to the asking player only.
/// </summary>
[Serializable, NetSerializable]
public sealed class ColonyAtmOwnCardRequestMsg : BoundUserInterfaceMessage { }

/// <summary>
///     Server → the one player who opened the screen: their own card's account number and PIN, for
///     the nav bar. Only ever sent to that player, and only for the card that is theirs (as the
///     character notes show it) - never the card in the reader. Zero when they have none.
/// </summary>
[Serializable, NetSerializable]
public sealed class ColonyAtmOwnCardMsg(int accountNumber, int pin) : BoundUserInterfaceMessage
{
    public int AccountNumber { get; } = accountNumber;
    public int Pin { get; } = pin;
}

/// <summary>Player pressed a scroll arrow on the history screen.</summary>
[Serializable, NetSerializable]
public sealed class ColonyAtmScrollHistoryBuiMsg : BoundUserInterfaceMessage
{
    /// <summary>True to page towards older entries, false towards newer ones.</summary>
    public bool Older { get; }
    public ColonyAtmScrollHistoryBuiMsg(bool older) => Older = older;
}

/// <summary>
///     Player pressed the CANCEL (red) key: abandons the transaction in progress, or at the menu
///     ends the session and hands the card back.
/// </summary>
[Serializable, NetSerializable]
public sealed class ColonyAtmCancelBuiMsg : BoundUserInterfaceMessage { }
