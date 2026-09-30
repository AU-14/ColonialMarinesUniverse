using Content.Shared.CMU14.Radio;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>How a row reads on the glass.</summary>
public enum ANPRCRowStyle : byte
{
    /// <summary>A working line: a setting, a memory, a contact.</summary>
    Normal,

    /// <summary>A section marker inside a screen. Never selectable.</summary>
    Heading,

    /// <summary>Explanatory text. Never selectable.</summary>
    Note,

    Good,
    Warn,
    Bad,
}

/// <summary>
///     One line on the set's display. A screen is a list of these and nothing else - the glass is a
///     character grid, so a row is a label, a value, and what happens when ENT is pressed on it.
/// </summary>
public sealed class ANPRCScreenRow
{
    public string Label = string.Empty;

    /// <summary>Printed hard against the right edge. Empty for rows that are only a label.</summary>
    public string Value = string.Empty;

    public ANPRCRowStyle Style = ANPRCRowStyle.Normal;

    /// <summary>The cursor can rest here. A row with no action is never selectable.</summary>
    public bool Selectable => Activate != null;

    /// <summary>The row is holding a setting on - MON up, SCAN running, the working memory.</summary>
    public bool Lit;

    /// <summary>One press from something that cannot be taken back.</summary>
    public bool Armed;

    /// <summary>What ENT does here.</summary>
    public Action? Activate;

    public static ANPRCScreenRow Heading(string label) => new()
    {
        Label = label,
        Style = ANPRCRowStyle.Heading,
    };

    public static ANPRCScreenRow Note(string label) => new()
    {
        Label = label,
        Style = ANPRCRowStyle.Note,
    };

    public static ANPRCScreenRow Info(string label, string value, ANPRCRowStyle style = ANPRCRowStyle.Normal) => new()
    {
        Label = label,
        Value = value,
        Style = style,
    };
}

/// <summary>What the keypad is currently typing into.</summary>
public enum ANPRCEntryMode : byte
{
    /// <summary>Digits against the set's MHz.kHz template.</summary>
    Frequency,

    /// <summary>The letter groups printed above the number keys, keyed the way they are lettered.</summary>
    Letters,
}

/// <summary>
///     A field the keypad is filling in. The screen that asked for it gets the result and decides
///     what to do with it, so entry is one mechanism rather than one per screen.
/// </summary>
public sealed class ANPRCEntry
{
    public required string Prompt;
    public required ANPRCEntryMode Mode;
    public required int MaxLength;
    public required Action<string> Commit;

    /// <summary>Prefilled, so editing a station callsign does not start from nothing.</summary>
    public string Initial = string.Empty;
}

/// <summary>
///     What a screen can ask the panel to do. Screens never touch the radio or the window directly:
///     they build rows, and the things those rows do go through here.
/// </summary>
public interface IANPRCScreenHost
{
    /// <summary>Open a screen over this one. CLR comes back.</summary>
    void Push(ANPRCScreen screen);

    /// <summary>Back out of the current screen.</summary>
    void Pop();

    /// <summary>Print a line on the glass for a few seconds.</summary>
    void Acknowledge(string text);

    /// <summary>Hand the keypad to a field.</summary>
    void BeginEntry(ANPRCEntry entry);

    /// <summary>Rebuild the rows now rather than waiting for the next state push.</summary>
    void Refresh();
}

/// <summary>
///     Everything the screens are allowed to ask the radio for. The window owns the events that
///     reach the server; this is the handle it lends out.
/// </summary>
public sealed class ANPRCScreenActions
{
    public required Action<int> SelectSlot;
    public required Action<string> AddSlot;
    public required Action<int> DeleteSlot;
    public required Action<int> ClearSlot;
    public required Action<int, ProtoId<RadioChannelPrototype>> SetSlotChannel;
    public required Action<int, string> ManualFrequency;
    public required Action<int, string> RenameSlot;
    public required Action QuickSetup;

    public required Action TogglePower;
    public required Action ToggleMonitor;
    public required Action<RadioMode> SetMode;
    public required Action<bool> SetScan;
    public required Action<RadioTxPower> SetTxPower;
    public required Action<int> SetSquelch;
    public required Action<string> SetCallsign;

    // expert techniques, only ever reached from the faceplate
    public required Action<bool> SetBurst;
    public required Action<bool> SetPowerSave;
    public required Action<int> SetPriorityWatch;
    public required Action<bool> SetEmcon;
    public required Action<int, int> SetRetrans;
    public required Action PeakAntenna;
    public required Action Otar;
    public required Action<int> SetDwell;
    public required Action JammerBearing;
    public required Action<string, string> KeyTrial;

    public required Action CryptoZeroize;
    public required Action CryptoDestroy;
    public required Action CryptoRecrypto;

    public required Action RadioCheck;
    public required Action OpenDirectory;
    public required Action OpenPhone;
    public required Action<bool> SetSweep;
    public required Action<int, RadioFrequency> TuneContact;
    public required Action<bool> PrintLog;

    /// <summary>Step the display backlight. Panel-local, so it never leaves the client.</summary>
    public required Action CycleBacklight;
}

/// <summary>
///     One screen the display can be put on. A screen holds no radio state: it is handed the set's
///     state and builds the lines that describe it, which is why the glass can never disagree with
///     the radio.
/// </summary>
public abstract class ANPRCScreen
{
    protected IANPRCScreenHost Host = default!;
    protected ANPRCScreenActions Radio = default!;

    /// <summary>Printed across the screen's title bar.</summary>
    public abstract string Title { get; }

    /// <summary>Where the cursor is sitting, kept per screen so paging away and back holds place.</summary>
    public int Cursor;

    public void Bind(IANPRCScreenHost host, ANPRCScreenActions radio)
    {
        Host = host;
        Radio = radio;
    }

    public abstract void Build(ANPRCPanelContext context, List<ANPRCScreenRow> rows);

    /// <summary>Printed at the right of the title bar - a count, a state, whatever the screen is about.</summary>
    public virtual string Status(ANPRCPanelContext context) => string.Empty;

    /// <summary>
    ///     A screen that only makes sense on a live set says so here, and the panel prints the
    ///     reason rather than showing rows that cannot be worked.
    /// </summary>
    public virtual string? Unavailable(ANPRCPanelContext context)
        => context.Powered ? null : Loc.GetString("anprc-fp-unavailable-off");
}
