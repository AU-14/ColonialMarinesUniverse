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
        int historyTotal = 0)
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

/// <summary>Player pressed the Backspace (yellow) key.</summary>
[Serializable, NetSerializable]
public sealed class ColonyAtmBackspaceBuiMsg : BoundUserInterfaceMessage { }

/// <summary>Player pressed the Confirm (green) key.</summary>
[Serializable, NetSerializable]
public sealed class ColonyAtmConfirmBuiMsg : BoundUserInterfaceMessage { }

/// <summary>Player pressed a scroll arrow on the history screen.</summary>
[Serializable, NetSerializable]
public sealed class ColonyAtmScrollHistoryBuiMsg : BoundUserInterfaceMessage
{
    /// <summary>True to page towards older entries, false towards newer ones.</summary>
    public bool Older { get; }
    public ColonyAtmScrollHistoryBuiMsg(bool older) => Older = older;
}

/// <summary>Player pressed the Cancel (red) key.</summary>
[Serializable, NetSerializable]
public sealed class ColonyAtmCancelBuiMsg : BoundUserInterfaceMessage { }
