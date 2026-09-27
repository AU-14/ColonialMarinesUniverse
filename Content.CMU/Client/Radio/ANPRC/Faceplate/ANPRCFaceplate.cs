using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Radio;
using Content.Shared.Radio;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     The expert view: the panel of an AN/PRC-117G, laid out the way the set is: connector plate down the left,
///     display and brand plate in the middle, function switch on the right, soft keys under the
///     glass and the keypad across the bottom.
///
///     Everything the operator does happens on the glass. There is no deck of buttons below it,
///     because the real set has none: a screen is put on a page, the page is a list of lines, and
///     the lines are worked with the preset rocker and ENT. The window owns the hardware
///     behaviours - which screen is up, what the keypad is typing into, where the switch is
///     sitting, how bright the backlight is - and forwards everything that touches the radio to
///     the bound user interface. Nothing here keeps a second copy of radio state: every line is
///     rebuilt from the state the server pushed, so the panel cannot drift away from the set.
/// </summary>
public sealed class ANPRCFaceplate : PanelContainer
{
    /// <summary>The smallest the set can be drawn without the keypad and switch overlapping.</summary>
    public static readonly Vector2 MinimumSize = new(620f, 600f);

    private static readonly TimeSpan AcknowledgeHold = TimeSpan.FromSeconds(3.5);

    /// <summary>How long a letter key stays open to being pressed again to step its group.</summary>
    private static readonly TimeSpan MultiTapHold = TimeSpan.FromSeconds(1.1);

    private const int MaxEntryDigits = 6;


    private readonly IGameTiming _timing;
    private readonly ANPRCPanelContext _context;

    private readonly ANPRCRadioActions _radio;

    private readonly ANPRCLcdScreen _screen;
    private readonly ANPRCReadout _readout;
    private readonly ANPRCScreenView _view;
    private readonly ANPRCModeKnob _knob;
    private readonly ANPRCKeypad _keypad;
    private readonly ANPRCPortStrip _ports;

    private readonly ANPRCComsecScreen _comsecScreen;

    /// <summary>The handle the screens reach the radio through. One object, shared by all of them.</summary>
    private readonly ANPRCScreenActions _actions;
    private readonly IANPRCScreenHost _host;

    private readonly Dictionary<ANPRCPageId, ANPRCScreen> _screens = new();
    private readonly Dictionary<ANPRCPageId, ANPRCKeyButton> _tabs = new();

    /// <summary>Screens opened over a page. CLR walks back down it.</summary>
    private readonly List<ANPRCScreen> _stack = new();

    private readonly Label _statusLabel;
    private readonly Label _acknowledgeLabel;

    private ANPRCRadioState? _state;
    private ANPRCPageId _page = ANPRCPageId.Net;
    private ANPRCBacklight _backlight = ANPRCBacklight.Night;

    private ANPRCEntry? _entry;
    private string _entryText = string.Empty;

    // where a run of presses on one letter key has got to in its group
    private ANPRCKey _tapKey;
    private int _tapIndex = -1;
    private TimeSpan _tapUntil;

    private TimeSpan _acknowledgeUntil;

    // the detent the panel last rendered. a sprung pull begins and ends on the knob's own clock
    // rather than on a state push, so the screen has to follow it from the frame loop or the
    // held-pull prompt would hang around until the next refresh tick happened to land
    private ANPRCKnobPosition _shownSwitch = ANPRCKnobPosition.Off;

    // whether the last Z pull actually primed a wipe. the prompt must not tell an operator the
    // fill is one press from gone when the screen refused the arm
    private bool _zeroizeArmed;

    // Build can back out of a screen that has gone away under it, which invalidates the rows it
    // was part-way through writing. Guards the one retry that needs
    private bool _rebuilding;

    private enum ANPRCPageId : byte
    {
        Net,
        Comsec,
        Search,
        Log,
        Options,
        Help,
    }

    /// <summary>The screen the glass is on, including anything opened over the current page.</summary>
    private ANPRCScreen Current => _stack.Count > 0 ? _stack[^1] : _screens[_page];

    public ANPRCFaceplate(ANPRCRadioActions radio)
    {
        _radio = radio;
        _timing = IoCManager.Resolve<IGameTiming>();
        _context = new ANPRCPanelContext(IoCManager.Resolve<IPrototypeManager>());

        PanelOverride = new StyleBoxFlat { BackgroundColor = ANPRCPanelStyle.Chassis };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 6,
        };

        AddChild(root);

        root.AddChild(new ANPRCCarryHandle());

        // ----- head: ports, display, function switch ---------------------------------------------

        var head = ANPRCPanelStyle.Row(8);
        head.VerticalExpand = true;

        _ports = new ANPRCPortStrip { MinWidth = 132f };
        head.AddChild(ANPRCPanelStyle.Framed(
            new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Margin = new Thickness(6, 6),
                Children = { _ports },
                VerticalAlignment = VAlignment.Top,
            },
            ANPRCPanelStyle.ChassisDeep,
            ANPRCPanelStyle.ChassisEdge));

        var centre = ANPRCPanelStyle.Column(5);
        centre.HorizontalExpand = true;
        centre.VerticalExpand = true;

        _readout = new ANPRCReadout(_timing);
        _view = _readout.Screen;

        _screen = new ANPRCLcdScreen
        {
            // a floor, not a size: the glass expands into whatever the window has spare, and a
            // lower floor is what lets the whole set fit a 720p screen with room for the toolbar
            MinHeight = 280f,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        _screen.Contents.AddChild(_readout);
        centre.AddChild(_screen);

        centre.AddChild(BrandPlate());
        head.AddChild(centre);

        var knobColumn = ANPRCPanelStyle.Column(3);
        knobColumn.VerticalAlignment = VAlignment.Top;

        _knob = new ANPRCModeKnob();
        _knob.OnPosition += OnKnobPosition;
        _knob.OnSpringBack += OnKnobSprungBack;

        knobColumn.AddChild(_knob);
        knobColumn.AddChild(new Label
        {
            Text = Loc.GetString("anprc-fp-eng-function"),
            FontOverride = ANPRCPanelStyle.Mono(8, true),
            FontColorOverride = ANPRCPanelStyle.EngravedDim,
            HorizontalAlignment = HAlignment.Center,
        });

        // engraved on the plate, the way the marking is on the set. the sprung detents are not
        // a fault and the panel should not need explaining to know that
        knobColumn.AddChild(new Label
        {
            Text = Loc.GetString("anprc-fp-eng-sprung"),
            FontOverride = ANPRCPanelStyle.Mono(7),
            FontColorOverride = ANPRCPanelStyle.EngravedDim,
            HorizontalAlignment = HAlignment.Center,
        });

        head.AddChild(knobColumn);
        root.AddChild(head);

        // ----- soft keys -------------------------------------------------------------------------

        // directly under the glass, where the set carries them, because that is what they label
        var tabs = ANPRCPanelStyle.Row(3);

        AddTab(tabs, ANPRCPageId.Net, Loc.GetString("anprc-fp-tab-pgm"), Loc.GetString("anprc-fp-tab-pgm-key"));
        AddTab(tabs, ANPRCPageId.Comsec, Loc.GetString("anprc-fp-tab-sec"), Loc.GetString("anprc-fp-tab-sec-key"));
        AddTab(tabs, ANPRCPageId.Search, Loc.GetString("anprc-fp-tab-srch"), Loc.GetString("anprc-fp-tab-srch-key"));
        AddTab(tabs, ANPRCPageId.Log, Loc.GetString("anprc-fp-tab-log"), Loc.GetString("anprc-fp-tab-log-key"));
        AddTab(tabs, ANPRCPageId.Options, Loc.GetString("anprc-fp-tab-opt"), Loc.GetString("anprc-fp-tab-opt-key"));
        AddTab(tabs, ANPRCPageId.Help, "?", Loc.GetString("anprc-fp-tab-help-key"));

        root.AddChild(tabs);

        // ----- keypad ----------------------------------------------------------------------------

        _keypad = new ANPRCKeypad();
        _keypad.OnKey += OnKeypadKey;

        root.AddChild(ANPRCPanelStyle.Framed(
            new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Margin = new Thickness(6, 6),
                Children = { _keypad },
            },
            ANPRCPanelStyle.ChassisDeep,
            ANPRCPanelStyle.ChassisEdge));

        // ----- status strip ----------------------------------------------------------------------

        var status = ANPRCPanelStyle.Row(8);
        status.Margin = new Thickness(6, 4);

        _statusLabel = new Label
        {
            Text = string.Empty,
            FontOverride = ANPRCPanelStyle.Mono(9),
            FontColorOverride = ANPRCPanelStyle.Engraved,
            HorizontalExpand = true,
            ClipText = true,
        };

        _acknowledgeLabel = new Label
        {
            Text = string.Empty,
            FontOverride = ANPRCPanelStyle.Mono(9, true),
            FontColorOverride = ANPRCPanelStyle.Good,
            ClipText = true,
            // a clipped label measures as nothing wide, and a row hands a non-expanding child
            // exactly its measured width - so without this every acknowledgement the panel has
            // ever printed was drawn into a strip no pixels across. It shares the strip with the
            // status line and reads from the right, the way the second field on a panel does
            HorizontalExpand = true,
            Align = Label.AlignMode.Right,
        };

        status.AddChild(_statusLabel);
        status.AddChild(_acknowledgeLabel);

        root.AddChild(ANPRCPanelStyle.Framed(status, ANPRCPanelStyle.ChassisDeep, ANPRCPanelStyle.ChassisEdge));

        // the panel's own clock. it has to hang off the chassis rather than off the window,
        // because popping the set out closes the window and stops its frame updates while the
        // chassis carries on living inside an OS window of its own
        root.AddChild(new ANPRCFrameTicker(Tick));

        // ----- the screens -----------------------------------------------------------------------

        _actions = BuildActions();
        _host = new ScreenHost(this);

        _comsecScreen = new ANPRCComsecScreen(_timing);

        _screens[ANPRCPageId.Net] = new ANPRCNetScreen();
        _screens[ANPRCPageId.Comsec] = _comsecScreen;
        _screens[ANPRCPageId.Search] = new ANPRCSearchScreen();
        _screens[ANPRCPageId.Log] = new ANPRCLogScreen();
        _screens[ANPRCPageId.Options] = new ANPRCOptionsScreen();
        _screens[ANPRCPageId.Help] = new ANPRCHelpScreen();

        foreach (var screen in _screens.Values)
        {
            screen.Bind(_host, _actions);
        }

        _view.OnRowClicked += OnRowClicked;

        ShowPage(ANPRCPageId.Net);

        // a set is dead until the server says otherwise. every control on the panel is driven
        // from UpdateState, so before the first one arrives they would all sit at a Control's
        // default of enabled and the panel would read as a powered set
        _keypad.SetDisabled(true);
        _screen.Lit = false;
        _view.Lit = false;
        _view.Unavailable = Loc.GetString("anprc-fp-no-data");
    }

    private ANPRCScreenActions BuildActions()
    {
        return new ANPRCScreenActions
        {
            SelectSlot = slot => _radio.SelectSlot(slot),
            AddSlot = label => _radio.AddSlot(label),
            DeleteSlot = slot => _radio.DeleteSlot(slot),
            ClearSlot = slot => _radio.ClearSlot(slot),
            SetSlotChannel = (slot, channel) => _radio.SetSlotChannel(slot, channel),
            ManualFrequency = (slot, text) => _radio.ManualFrequency(slot, text),
            RenameSlot = (slot, label) => _radio.RenameSlot(slot, label),
            QuickSetup = () => _radio.QuickSetup(),

            TogglePower = () => _radio.TogglePower(),
            ToggleMonitor = () => _radio.ToggleMonitor(),
            SetMode = mode => _radio.SetMode(mode),
            SetScan = enabled => _radio.SetScan(enabled),
            SetTxPower = power => _radio.SetTxPower(power),
            SetSquelch = level => _radio.SetSquelch(level),
            SetCallsign = callsign => _radio.SetCallsign(callsign),

            SetBurst = enabled => _radio.SetBurst(enabled),
            SetPowerSave = enabled => _radio.SetPowerSave(enabled),
            SetPriorityWatch = slot => _radio.SetPriorityWatch(slot),
            SetEmcon = enabled => _radio.SetEmcon(enabled),
            SetRetrans = (a, b) => _radio.SetRetrans(a, b),
            PeakAntenna = () => _radio.PeakAntenna(),
            Otar = () => _radio.Otar(),
            SetDwell = kilohertz => _radio.SetDwell(kilohertz),
            JammerBearing = () => _radio.JammerBearing(),

            CryptoZeroize = () => _radio.CryptoZeroize(),
            CryptoDestroy = () => _radio.CryptoDestroy(),
            CryptoRecrypto = () => _radio.CryptoRecrypto(),

            RadioCheck = () => _radio.RadioCheck(),
            OpenDirectory = () => _radio.OpenDirectory(),
            OpenPhone = () => _radio.OpenPhone(),
            SetSweep = enabled => _radio.SetSweep(enabled),
            TuneContact = (slot, frequency) => _radio.TuneContact(slot, frequency),
            PrintLog = intercepts => _radio.PrintLog(intercepts),

            CycleBacklight = CycleBacklight,
        };
    }

    /// <summary>The panel's side of <see cref="IANPRCScreenHost"/>, kept off the public surface.</summary>
    private sealed class ScreenHost(ANPRCFaceplate window) : IANPRCScreenHost
    {
        public void Push(ANPRCScreen screen)
        {
            screen.Bind(this, window._actions);
            screen.Cursor = 0;

            window._stack.Add(screen);
            window.RebuildScreen();
        }

        public void Pop() => window.PopScreen();

        public void Acknowledge(string text) => window.Acknowledge(text);

        public void BeginEntry(ANPRCEntry entry) => window.BeginEntry(entry);

        public void Refresh() => window.RebuildScreen();
    }

    private static Control BrandPlate()
    {
        var plate = ANPRCPanelStyle.Row(10);
        plate.Margin = new Thickness(6, 3);

        plate.AddChild(new Label
        {
            Text = "HARRIS",
            FontOverride = ANPRCPanelStyle.Mono(12, true),
            FontColorOverride = ANPRCPanelStyle.Engraved,
        });

        plate.AddChild(new Label
        {
            Text = "FALCON III",
            FontOverride = ANPRCPanelStyle.Mono(10),
            FontColorOverride = ANPRCPanelStyle.EngravedDim,
            HorizontalExpand = true,
        });

        plate.AddChild(new Label
        {
            Text = "AN/PRC-117G",
            FontOverride = ANPRCPanelStyle.Mono(10, true),
            FontColorOverride = ANPRCPanelStyle.Engraved,
        });

        return ANPRCPanelStyle.Framed(plate, ANPRCPanelStyle.ChassisRaised, ANPRCPanelStyle.ChassisEdge);
    }

    private void AddTab(BoxContainer row, ANPRCPageId id, string legend, string secondary)
    {
        var button = new ANPRCKeyButton(legend, secondary, minHeight: 28f);
        button.HorizontalExpand = true;
        button.OnPressed += _ => ShowPage(id);

        _tabs[id] = button;
        row.AddChild(button);
    }

    // ----- screen routing ------------------------------------------------------------------------

    private void ShowPage(ANPRCPageId id)
    {
        _page = id;

        // paging away abandons whatever was opened over the old page, and anything being typed
        // into it - the pad must never be left filling in a field nobody can see
        _stack.Clear();
        CancelEntry();

        foreach (var (tabId, tab) in _tabs)
        {
            tab.Lit = tabId == id;
        }

        RebuildScreen();
    }

    private void PopScreen()
    {
        if (_stack.Count == 0)
            return;

        _stack.RemoveAt(_stack.Count - 1);
        RebuildScreen();
    }

    /// <summary>
    ///     Rebuild the lines on the glass from the set's state. Runs on every state push and after
    ///     anything the operator does, so a line can never describe a radio that has moved on.
    /// </summary>
    private void RebuildScreen()
    {
        if (_state == null)
            return;

        var screen = Current;
        var depth = _stack.Count;

        _view.Title = screen.Title;
        _view.Status = screen.Status(_context);
        _view.Unavailable = screen.Unavailable(_context);

        _view.Rows.Clear();

        if (_view.Unavailable == null)
        {
            screen.Build(_context, _view.Rows);

            // a screen can back out of itself while building - a memory deleted from under the
            // cursor does. Its rows are then half-written and describe something gone, so the
            // screen underneath gets one chance to build properly instead
            if (_stack.Count != depth && !_rebuilding)
            {
                _rebuilding = true;

                try
                {
                    RebuildScreen();
                }
                finally
                {
                    _rebuilding = false;
                }

                return;
            }
        }

        ClampCursor(screen);
    }

    /// <summary>
    ///     Put the cursor on a line that can actually be worked. Rows come and go as the radio
    ///     changes, so wherever it was may now be a heading or nothing at all.
    /// </summary>
    private void ClampCursor(ANPRCScreen screen)
    {
        var rows = _view.Rows;

        if (rows.Count == 0)
        {
            screen.Cursor = 0;
            _view.Cursor = 0;

            return;
        }

        var cursor = Math.Clamp(screen.Cursor, 0, rows.Count - 1);

        if (!rows[cursor].Selectable)
        {
            // nearest selectable line, looking down first because a screen reads downward
            var found = -1;

            for (var offset = 0; offset < rows.Count; offset++)
            {
                if (cursor + offset < rows.Count && rows[cursor + offset].Selectable)
                {
                    found = cursor + offset;
                    break;
                }

                if (cursor - offset >= 0 && rows[cursor - offset].Selectable)
                {
                    found = cursor - offset;
                    break;
                }
            }

            cursor = found < 0 ? 0 : found;
        }

        var moved = screen.Cursor != cursor || _view.Cursor != cursor;

        screen.Cursor = cursor;
        _view.Cursor = cursor;

        // only chase the cursor when it has actually moved. Rebuilding happens on every state
        // push, and forcing it back into view each time would undo a scroll the operator had
        // made with the wheel while reading down the log
        if (moved)
            _view.ScrollToCursor();
    }

    /// <summary>
    ///     Walk the cursor down the selectable lines. When there is no further line to sit on in
    ///     that direction the view scrolls instead, which is how a screen that is mostly text -
    ///     the net log, the key card - is read without touching the mouse.
    /// </summary>
    private void MoveCursor(int delta)
    {
        var rows = _view.Rows;

        if (_view.Unavailable != null || rows.Count == 0)
            return;

        var screen = Current;
        var cursor = screen.Cursor + delta;

        while (cursor >= 0 && cursor < rows.Count && !rows[cursor].Selectable)
        {
            cursor += delta;
        }

        if (cursor < 0 || cursor >= rows.Count || !rows[cursor].Selectable)
        {
            // nothing further to land on. scroll the page, and only wrap once the end of the
            // list is actually on screen - otherwise the rocker would jump back to the top
            // while there is still text below waiting to be read
            if (_view.Scroll(delta))
                return;

            for (var step = 0; step < rows.Count; step++)
            {
                cursor = delta > 0 ? step : rows.Count - 1 - step;

                if (rows[cursor].Selectable)
                    break;
            }

            if (cursor < 0 || cursor >= rows.Count || !rows[cursor].Selectable)
                return;
        }

        screen.Cursor = cursor;
        _view.Cursor = cursor;
        _view.ScrollToCursor();
    }

    private void Activate()
    {
        var rows = _view.Rows;

        if (_view.Unavailable != null || rows.Count == 0)
            return;

        var cursor = Current.Cursor;

        if (cursor < 0 || cursor >= rows.Count)
            return;

        rows[cursor].Activate?.Invoke();

        // whatever it did, the lines describing it are now stale
        RebuildScreen();
        UpdateReadout();
    }

    private void OnRowClicked(int index)
    {
        var rows = _view.Rows;

        if (index < 0 || index >= rows.Count || !rows[index].Selectable)
            return;

        // clicking a line the cursor is not on moves to it; clicking the line it is on works it.
        // A single click that both moves and fires would make a mis-click irreversible on a
        // screen where half the lines wipe COMSEC
        if (Current.Cursor != index)
        {
            Current.Cursor = index;
            _view.Cursor = index;
            _view.ScrollToCursor();

            return;
        }

        Activate();
    }

    private void CycleBacklight()
    {
        _backlight = ANPRCPanelStyle.NextBacklight(_backlight);
        Acknowledge(Loc.GetString("anprc-fp-ack-backlight", ("level", ANPRCPanelStyle.BacklightLabel(_backlight))));
        UpdateReadout();
    }

    // ----- keypad entry --------------------------------------------------------------------------

    private void BeginEntry(ANPRCEntry entry)
    {
        _entry = entry;
        _entryText = entry.Initial;
        _tapIndex = -1;

        _keypad.SetEntryMode(entry.Mode);
        UpdateReadout();
    }

    private void CancelEntry()
    {
        if (_entry == null)
            return;

        _entry = null;
        _entryText = string.Empty;
        _tapIndex = -1;

        _keypad.SetEntryMode(null);
        UpdateReadout();
    }

    private void OnKeypadKey(ANPRCKey key)
    {
        // the rockers are their own switches and work whatever the pad is doing
        switch (key)
        {
            case ANPRCKey.PageUp:
                StepPage(-1);
                return;

            case ANPRCKey.PageDown:
                StepPage(1);
                return;

            case ANPRCKey.PresetUp:
                MoveCursor(-1);
                return;

            case ANPRCKey.PresetDown:
                MoveCursor(1);
                return;
        }

        if (_entry != null)
        {
            HandleEntryKey(key);
            return;
        }

        HandleShortcutKey(key);
    }

    private void HandleEntryKey(ANPRCKey key)
    {
        var entry = _entry!;

        switch (key)
        {
            case ANPRCKey.Clear:
                if (_entryText.Length == 0)
                {
                    CancelEntry();
                    Acknowledge(Loc.GetString("anprc-fp-ack-entry-abandoned"));

                    return;
                }

                _entryText = _entryText[..^1];
                _tapIndex = -1;
                UpdateReadout();

                return;

            case ANPRCKey.Enter:
                if (entry.Mode == ANPRCEntryMode.Frequency)
                {
                    // three digits is a whole megahertz, which is the least the set can act on.
                    // anything shorter is not a frequency yet
                    if (_entryText.Length < 3)
                    {
                        Acknowledge(Loc.GetString("anprc-fp-ack-three-digits"));
                        return;
                    }

                    var commit = entry.Commit;
                    var text = SubmitEntry(_entryText);

                    CancelEntry();
                    commit(text);
                }
                else
                {
                    var commit = entry.Commit;
                    var text = _entryText.Trim();

                    CancelEntry();
                    commit(text);
                }

                RebuildScreen();

                return;
        }

        if (!ANPRCKeypad.IsDigit(key))
            return;

        if (entry.Mode == ANPRCEntryMode.Frequency)
        {
            if (_entryText.Length >= Math.Min(entry.MaxLength, MaxEntryDigits))
                return;

            _entryText += ANPRCKeypad.DigitOf(key).ToString();
            UpdateReadout();

            return;
        }

        TypeLetter(key, entry.MaxLength);
    }

    /// <summary>
    ///     Letters are keyed off the groups printed above the numbers, the way the set is
    ///     lettered: press once for the first letter of the group, again to step through it.
    ///     Moving to another key, or pausing, settles the character being worked on.
    /// </summary>
    private void TypeLetter(ANPRCKey key, int maxLength)
    {
        var group = ANPRCKeypad.LetterCycle(key);

        if (group.Length == 0)
            return;

        var stepping = _tapIndex >= 0 && _tapKey == key && _timing.CurTime <= _tapUntil && _entryText.Length > 0;

        // Substring rather than indexing, and deliberately so. Concatenating a char onto a
        // string is lowered to String.Concat over ReadOnlySpan<char> using the span constructor
        // that takes a byref, which the sandbox forbids - and the whole assembly then fails its
        // type check on load rather than erroring at the call site. Writing .ToString() on the
        // char does not help: the compiler sees through it and lowers it exactly the same way.
        // Taking a one-character string means no char is ever materialised to take a span of.
        if (stepping)
        {
            _tapIndex = (_tapIndex + 1) % group.Length;
            _entryText = _entryText[..^1] + group.Substring(_tapIndex, 1);
        }
        else
        {
            if (_entryText.Length >= maxLength)
                return;

            _tapIndex = 0;
            _entryText += group.Substring(0, 1);
        }

        _tapKey = key;
        _tapUntil = _timing.CurTime + MultiTapHold;

        UpdateReadout();
    }

    private void HandleShortcutKey(ANPRCKey key)
    {
        switch (key)
        {
            case ANPRCKey.Digit1:
                // keying up is the one keypad function that needs an antenna in the air, so it
                // is the one that names the gate it tripped instead of just refusing
                if (!_context.Ready)
                {
                    Acknowledge(
                        !_context.Deployed ? Loc.GetString("anprc-fp-ack-check-stowed")
                        : _state is { SweepEnabled: true } ? Loc.GetString("anprc-fp-ack-check-searching")
                        : Loc.GetString("anprc-fp-ack-check-no-net"));

                    return;
                }

                _radio.RadioCheck();
                Acknowledge(Loc.GetString("anprc-fp-ack-radio-check"));

                return;

            case ANPRCKey.Digit2:
                CycleBacklight();
                return;

            case ANPRCKey.Digit3:
                if (_state == null || !_context.Powered)
                    return;

                var mode = ANPRCOptionsScreen.NextMode(_state.Mode);
                _radio.SetMode(mode);
                Acknowledge(Loc.GetString("anprc-fp-ack-mode", ("mode", ANPRCPanelContext.ModeName(mode))));

                return;

            case ANPRCKey.Digit4:
                if (_state == null || !_context.Powered)
                    return;

                var squelch = _state.SquelchLevel >= ANPRCRadioComponent.MaxSquelchLevel
                    ? 0
                    : _state.SquelchLevel + 1;

                _radio.SetSquelch(squelch);
                Acknowledge(Loc.GetString("anprc-fp-ack-squelch", ("level", squelch)));

                return;

            case ANPRCKey.Digit5:
                ShowPage(ANPRCPageId.Comsec);

                Acknowledge(_comsecScreen.ArmZeroize(_context)
                    ? Loc.GetString("anprc-fp-ack-zeroize-armed")
                    : Loc.GetString("anprc-fp-ack-nothing-to-wipe"));

                RebuildScreen();

                return;

            case ANPRCKey.Digit6:
                // the set refuses to come up on a dead or missing cell, and the panel used to
                // report SET ON anyway because it never checked
                if (_state is { Enabled: false, HasBattery: false })
                {
                    Acknowledge(Loc.GetString("anprc-fp-ack-no-cell"));
                    return;
                }

                _radio.TogglePower();
                Acknowledge(Loc.GetString(_state is { Enabled: true } ? "anprc-fp-ack-set-off" : "anprc-fp-ack-set-on"));

                return;

            case ANPRCKey.Digit7:
                ShowPage(ANPRCPageId.Options);
                return;

            case ANPRCKey.Digit8:
                ShowPage(ANPRCPageId.Net);
                return;

            case ANPRCKey.Digit9:
                ShowPage(ANPRCPageId.Comsec);
                return;

            case ANPRCKey.Digit0:
                ShowPage(ANPRCPageId.Log);
                return;

            case ANPRCKey.Clear:
                // CLR is the back key: out of an opened screen first, and off a page back to the
                // memories, which is where an operator wants to be by default
                if (_stack.Count > 0)
                    PopScreen();
                else if (_page != ANPRCPageId.Net)
                    ShowPage(ANPRCPageId.Net);

                return;

            case ANPRCKey.Enter:
                Activate();
                return;
        }
    }

    // the left rocker walks the soft-key pages in the order they sit under the glass
    private static readonly ANPRCPageId[] PageOrder =
    {
        ANPRCPageId.Net,
        ANPRCPageId.Comsec,
        ANPRCPageId.Search,
        ANPRCPageId.Log,
        ANPRCPageId.Options,
        ANPRCPageId.Help,
    };

    private void StepPage(int delta)
    {
        var index = Array.IndexOf(PageOrder, _page);
        var next = PageOrder[(index + delta + PageOrder.Length) % PageOrder.Length];

        ShowPage(next);
    }

    // ----- function switch -----------------------------------------------------------------------

    private void OnKnobPosition(ANPRCKnobPosition position)
    {
        if (_state == null)
            return;

        switch (position)
        {
            case ANPRCKnobPosition.Off:
                if (_state.Enabled)
                {
                    _radio.TogglePower();
                    Acknowledge("SET OFF");
                }

                return;

            case ANPRCKnobPosition.CipherText:
                var wasOff = !_state.Enabled;

                if (wasOff)
                {
                    _radio.TogglePower();
                    Acknowledge("SET ON");
                }

                // coming off clear, or off a dead set, lands on the set's default waveform.
                // throwing the switch onto a detent it is already sitting on steps to the next
                // secure waveform, which is how FH, SC and CT are all reachable from the switch
                if (_state.Mode == RadioMode.PlainText)
                {
                    _radio.SetMode(RadioMode.FrequencyHopping);
                    Acknowledge(Loc.GetString("anprc-fp-ack-mode", ("mode", ANPRCPanelContext.ModeName(RadioMode.FrequencyHopping))));
                }
                else if (!wasOff)
                {
                    var stepped = NextSecureMode(_state.Mode);

                    _radio.SetMode(stepped);
                    Acknowledge(Loc.GetString("anprc-fp-ack-mode", ("mode", ANPRCPanelContext.ModeName(stepped))));
                }

                return;

            case ANPRCKnobPosition.PlainText:
                if (!_state.Enabled)
                {
                    _radio.TogglePower();
                    Acknowledge("SET ON");
                }

                if (_state.Mode != RadioMode.PlainText)
                {
                    _radio.SetMode(RadioMode.PlainText);
                    Acknowledge(Loc.GetString("anprc-fp-ack-mode-plain"));
                }

                return;

            // LD and Z are sprung on the real set: they are pulls that do a job and let go,
            // not modes the switch sits in. Both say what they did and that the switch is
            // coming back, because a pointer that walks home on its own reads as a fault
            case ANPRCKnobPosition.Load:
                if (!_state.Enabled)
                {
                    _radio.TogglePower();
                    Acknowledge(Loc.GetString("anprc-fp-ack-set-on-comsec"));
                }
                else
                {
                    Acknowledge(Loc.GetString("anprc-fp-ack-ld-comsec"));
                }

                ShowPage(ANPRCPageId.Comsec);

                return;

            case ANPRCKnobPosition.Zeroize:
                ShowPage(ANPRCPageId.Comsec);

                _zeroizeArmed = _comsecScreen.ArmZeroize(_context);

                Acknowledge(_zeroizeArmed
                    ? Loc.GetString("anprc-fp-ack-z-armed")
                    : Loc.GetString("anprc-fp-ack-z-nothing"));

                RebuildScreen();

                return;
        }
    }

    /// <summary>
    ///     A sprung detent has let go. The mode the set is in never changed, so the only thing
    ///     worth saying is where the switch went and that the pull still counted - which is
    ///     exactly what an operator watching the pointer move by itself needs told.
    /// </summary>
    private void OnKnobSprungBack(ANPRCKnobPosition detent)
    {
        if (_state == null)
            return;

        var landed = detent == ANPRCKnobPosition.Off
            ? Loc.GetString("anprc-fp-off")
            : ANPRCModeKnob.Legend(detent) +
              (detent == ANPRCKnobPosition.CipherText
                  ? " " + ANPRCPanelContext.ModeShort(_state.Mode)
                  : string.Empty);

        Acknowledge(Loc.GetString("anprc-fp-ack-sprung-back", ("position", landed)));
        UpdateReadout();
    }

    // the secure waveforms, in the order the switch steps through them. plain text is not one
    // of them - that is its own detent
    private static RadioMode NextSecureMode(RadioMode mode) => mode switch
    {
        RadioMode.FrequencyHopping => RadioMode.SingleChannel,
        RadioMode.SingleChannel => RadioMode.CipherText,
        _ => RadioMode.FrequencyHopping,
    };

    private static ANPRCKnobPosition KnobPositionFor(ANPRCRadioState state)
    {
        if (!state.Enabled)
            return ANPRCKnobPosition.Off;

        return state.Mode == RadioMode.PlainText
            ? ANPRCKnobPosition.PlainText
            : ANPRCKnobPosition.CipherText;
    }

    // ----- entry formatting ----------------------------------------------------------------------

    /// <summary>
    ///     The pad keys a fixed-width MHz.kHz frequency, the way the set's own display fills in
    ///     left to right. Untouched positions show as the template so the dot never moves under
    ///     the operator mid-entry.
    /// </summary>
    private static string FormatEntry(string entry)
    {
        if (entry.Length == 0)
            return string.Empty;

        var megahertz = entry.Length <= 3 ? entry : entry[..3];
        var kilohertz = entry.Length <= 3 ? string.Empty : entry[3..];

        var left = megahertz.PadRight(3, '_');
        var right = kilohertz.PadRight(3, '_');

        return left + "." + right;
    }

    /// <summary>The same number with the untyped positions zeroed, which is what the set acts on.</summary>
    private static string SubmitEntry(string entry)
    {
        var megahertz = entry.Length <= 3 ? entry.PadLeft(3, '0') : entry[..3];
        var kilohertz = entry.Length <= 3 ? "000" : entry[3..].PadRight(3, '0');

        return megahertz + "." + kilohertz;
    }

    // ----- state ---------------------------------------------------------------------------------

    public void UpdateState(ANPRCRadioState state)
    {
        _state = state;
        _context.SetState(state);

        // the panel is alive whenever the set is switched on. wearing it or staking it down is
        // what puts an antenna in the air, not what wakes the front panel up - a 117G on a
        // bench programmes exactly like one on a man's back, and the server agrees
        var powered = _context.Powered;

        _screen.Lit = powered;
        _screen.Backlight = _backlight;

        _knob.Detent = KnobPositionFor(state);
        // the detent is already lettered CT, so only a waveform that is not plain cipher needs
        // spelling out beside it
        _knob.Waveform = powered && state.Mode is RadioMode.FrequencyHopping or RadioMode.SingleChannel
            ? ANPRCPanelContext.ModeShort(state.Mode)
            : string.Empty;

        _keypad.SetDisabled(!powered);

        // the pad cannot be left typing into a set that has gone down, which is the one way the
        // panel could end up out of step with the radio
        if (!powered)
            CancelEntry();

        _ports.SetAntenna(state.AntennaLabel, !string.IsNullOrEmpty(state.AntennaLabel));
        _ports.SetAudio(state.Info.HandsetOut);
        _ports.SetFill(
            state.CryptoDesignation,
            !string.IsNullOrEmpty(state.CryptoFaction) && !state.CryptoStale,
            state.CryptoStale);
        _ports.SetPower(state.HasBattery, state.BatteryFraction, state.Enabled);

        UpdateReadout();
        RebuildScreen();

        var deployment = Loc.GetString(state.Planted ? "anprc-fp-role-retrans" : state.IsEquipped ? "anprc-fp-worn" : "anprc-fp-stowed");
        var net = _context.HasActiveNet
            ? _context.ActiveIsDirect
                ? Loc.GetString("anprc-fp-status-direct", ("freq", ANPRCPanelContext.FormatFrequency(_context.ActiveFrequency)))
                : _context.ActiveChannel!.LocalizedName
            : Loc.GetString("anprc-fp-status-no-net");

        _statusLabel.Text = Loc.GetString("anprc-fp-status", ("deployment", deployment), ("power", Loc.GetString(state.Enabled ? "anprc-fp-on" : "anprc-fp-off")), ("net", net));
        _statusLabel.FontColorOverride = powered ? ANPRCPanelStyle.Engraved : ANPRCPanelStyle.Muted;
    }

    private void UpdateReadout()
    {
        if (_state == null)
            return;

        var prompt = string.Empty;
        var active = false;

        if (_entry is { } entry)
        {
            prompt = entry.Prompt + " " + (entry.Mode == ANPRCEntryMode.Frequency
                ? _entryText.Length == 0 ? "___.___" : FormatEntry(_entryText)
                : _entryText.Length == 0 ? "_" : _entryText + "_");

            active = true;
        }
        else if (_knob.Held)
        {
            // a sprung detent is being held. the prompt is the one line an operator is already
            // looking at, so it is where the spring gets explained
            prompt = _knob.Shown != ANPRCKnobPosition.Zeroize
                ? Loc.GetString("anprc-fp-prompt-ld-held")
                : _zeroizeArmed
                    ? Loc.GetString("anprc-fp-prompt-z-armed")
                    : Loc.GetString("anprc-fp-prompt-z-nothing");

            active = true;
        }
        else if (!_state.HasBattery)
        {
            prompt = Loc.GetString("anprc-fp-prompt-no-cell");
        }
        else if (!_state.Enabled)
        {
            prompt = Loc.GetString("anprc-fp-prompt-off");
        }
        else if (!_context.Deployed)
        {
            // the set is on and fully programmable here. only the air side is missing, and the
            // prompt says which half is which instead of reading as a dead panel
            prompt = Loc.GetString("anprc-fp-prompt-stowed");
        }
        else if (!_context.HasActiveNet && !_state.SweepEnabled)
        {
            prompt = Loc.GetString("anprc-fp-prompt-no-net");
        }

        _readout.Update(_context, _backlight, prompt, active);
    }

    private void Acknowledge(string text)
    {
        _acknowledgeLabel.Text = text;
        _acknowledgeUntil = _timing.CurTime + AcknowledgeHold;
    }

    private void Tick(FrameEventArgs args)
    {
        if (_acknowledgeLabel.Text?.Length > 0 && _timing.CurTime > _acknowledgeUntil)
            _acknowledgeLabel.Text = string.Empty;

        // a letter run settling is worth redrawing for: the cursor under the character being
        // worked goes away when the group closes
        if (_tapIndex >= 0 && _timing.CurTime > _tapUntil)
        {
            _tapIndex = -1;
            UpdateReadout();
        }

        if (_shownSwitch == _knob.Shown)
            return;

        _shownSwitch = _knob.Shown;
        _comsecScreen.Switch = _shownSwitch;

        RebuildScreen();
        UpdateReadout();
    }

    /// <summary>
    ///     Nothing to look at: a zero-sized control whose only job is to hand the panel a frame
    ///     tick from inside the chassis, so the panel keeps running once the set has been popped
    ///     out of its window.
    /// </summary>
    private sealed class ANPRCFrameTicker : Control
    {
        private readonly Action<FrameEventArgs> _tick;

        public ANPRCFrameTicker(Action<FrameEventArgs> tick)
        {
            _tick = tick;
            MouseFilter = MouseFilterMode.Ignore;
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize) => Vector2.Zero;

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            _tick(args);
        }
    }

    /// <summary>
    ///     The pair of tube handles across the top of the case. Drawn rather than left out because
    ///     the top of the panel would otherwise start at a screen, and the set would stop reading
    ///     as something with a back to it.
    /// </summary>
    private sealed class ANPRCCarryHandle : Control
    {
        public ANPRCCarryHandle()
        {
            MinHeight = 12f;
            MouseFilter = MouseFilterMode.Ignore;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var width = (float) PixelWidth;
            var height = (float) PixelHeight;

            if (width <= 0f || height <= 0f)
                return;

            var bar = new UIBox2(width * 0.08f, height * 0.3f, width * 0.92f, height * 0.7f);

            handle.DrawRect(bar, ANPRCPanelStyle.ChassisRaised);
            handle.DrawRect(new UIBox2(bar.Left, bar.Top, bar.Right, bar.Top + 1f), ANPRCPanelStyle.EngravedDim);

            // the two grab points, so the strip reads as handles rather than as a divider
            foreach (var fraction in new[] { 0.2f, 0.8f })
            {
                var x = width * fraction;
                handle.DrawRect(new UIBox2(x - 12f, 0f, x + 12f, height), ANPRCPanelStyle.ChassisRaised);
                handle.DrawRect(new UIBox2(x - 12f, 0f, x + 12f, 2f), ANPRCPanelStyle.ChassisEdge);
            }
        }
    }
}
