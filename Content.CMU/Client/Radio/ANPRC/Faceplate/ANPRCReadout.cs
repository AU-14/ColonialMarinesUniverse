using System.Numerics;
using Content.Shared.CMU14.Radio;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     What is actually printed on the set's glass. Four fixed ranks, laid out the way the real
///     display is: a status strip across the top, the tuned net large under it, the working
///     settings below that, and a prompt line the keypad types into.
///
///     Nothing here is interactive - a display is a display. Every value comes off the radio's own
///     state, so the glass cannot drift out of step with the set.
/// </summary>
public sealed class ANPRCReadout : Control
{
    // how long the lamps stay lit after the set keys up or takes traffic
    private static readonly TimeSpan TxLampHold = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan RxLampHold = TimeSpan.FromSeconds(3.5);

    private readonly IGameTiming _timing;

    private readonly Label _rxLamp;
    private readonly Label _txLamp;
    private readonly Label _secLabel;
    private readonly Label _signalLabel;
    private readonly Label _batteryLabel;

    private readonly Label _slotLabel;
    private readonly Label _frequencyLabel;
    private readonly Label _netLabel;
    private readonly Label _bandLabel;

    private readonly Label _settingsLabel;
    private readonly Label _stationLabel;
    private readonly Label _bitLabel;

    private readonly Label _promptLabel;

    /// <summary>
    ///     The screen's working area, between the tuned-net ranks above it and the settings and
    ///     prompt below. The panel puts its pages here.
    /// </summary>
    public readonly ANPRCScreenView Screen;

    private ANPRCLcdPalette _palette = ANPRCPanelStyle.Palette(ANPRCBacklight.Night);
    private TimeSpan _lastTransmit;
    private TimeSpan _lastReceive;
    private bool _lit;

    // whether each lamp is currently drawn lit, so a frame that changes nothing writes nothing
    private bool? _txLampLit;
    private bool? _rxLampLit;

    public ANPRCReadout(IGameTiming timing)
    {
        _timing = timing;

        VerticalExpand = true;
        HorizontalExpand = true;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
            VerticalExpand = true,
        };

        AddChild(root);

        // ----- rank 1: status strip ------------------------------------------------------------

        var status = ANPRCPanelStyle.Row(6);

        _rxLamp = Line("[" + Loc.GetString("anprc-fp-lamp-rx") + "]", 10, true);
        _txLamp = Line("[" + Loc.GetString("anprc-fp-lamp-tx") + "]", 10, true);
        _secLabel = Line(Loc.GetString("anprc-fp-rd-unsec"), 10, true);
        _signalLabel = Line(Loc.GetString("anprc-fp-rd-sig", ("bars", ANPRCPanelContext.Bars(0, 8))), 10);
        _batteryLabel = Line(Loc.GetString("anprc-fp-rd-bat", ("bars", ANPRCPanelContext.Bars(0, 4))), 10);

        status.AddChild(_rxLamp);
        status.AddChild(_txLamp);
        status.AddChild(_secLabel);
        status.AddChild(new Control { HorizontalExpand = true });
        status.AddChild(_signalLabel);
        status.AddChild(_batteryLabel);

        root.AddChild(status);
        root.AddChild(new ANPRCRule(() => _palette.Grid));

        // ----- rank 2: the tuned net -----------------------------------------------------------

        var tuned = ANPRCPanelStyle.Row(8);

        _slotLabel = Line("--", 13, true);
        _frequencyLabel = Line("---.---", 17, true);
        _bandLabel = Line("---", 10);

        tuned.AddChild(_slotLabel);
        tuned.AddChild(_frequencyLabel);
        tuned.AddChild(new Control { HorizontalExpand = true });
        tuned.AddChild(_bandLabel);

        root.AddChild(tuned);

        _netLabel = Line(Loc.GetString("anprc-fp-rd-no-net-loaded"), 11, true, expand: true);
        root.AddChild(_netLabel);

        root.AddChild(new ANPRCRule(() => _palette.Grid));

        // ----- rank 3: the screen ---------------------------------------------------------------

        // the working area of the display. Everything the operator does happens here, on the
        // glass, the way it does on the set - the panel has no second deck of controls below
        Screen = new ANPRCScreenView();
        root.AddChild(Screen);

        root.AddChild(new ANPRCRule(() => _palette.Grid));

        // ----- rank 4: working settings --------------------------------------------------------

        _settingsLabel = Line(string.Empty, 10, clip: true);
        root.AddChild(_settingsLabel);

        var footer = ANPRCPanelStyle.Row(8);

        _stationLabel = Line(Loc.GetString("anprc-fp-rd-stn", ("station", "---")), 10, expand: true);
        _bitLabel = Line(Loc.GetString("anprc-fp-rd-bit", ("result", "---")), 10, true);

        footer.AddChild(_stationLabel);
        footer.AddChild(_bitLabel);
        root.AddChild(footer);

        // ----- rank 5: the prompt line ---------------------------------------------------------

        _promptLabel = Line(string.Empty, 10, true, expand: true);
        root.AddChild(_promptLabel);
    }

    /// <summary>
    ///     One rank of characters on the glass.
    ///
    ///     A clipped label measures as zero width, and a horizontal row hands a non-expanding
    ///     child exactly its measured width - so a clipped field sitting in a row draws nothing
    ///     at all. Two kinds of field come out of that:
    ///
    ///     Fixed fields - the lamps, the mode, the meters, the tuned frequency - are short and
    ///     bounded, so they do not clip and are simply measured. Guessing a width for them
    ///     instead is what cut 177.400 down to 177.4 and UHF SAT down to UHF SA: a floor on a
    ///     clipped label is also its ceiling. The glass clips as a whole, so nothing can spill
    ///     past the bezel even if a reading does come out longer than expected.
    ///
    ///     Free-text fields - the net name, the station, the prompt - do clip, and expand into
    ///     whatever slack the row has, because their content has no length limit worth trusting.
    /// </summary>
    private static Label Line(string text, int size, bool bold = false, bool expand = false, bool clip = false)
    {
        return new Label
        {
            Text = text,
            FontOverride = ANPRCPanelStyle.Mono(size, bold),
            // a field in a vertical rank already spans the glass, so it can clip without also
            // having to expand - which is what the composed settings line wants, since its
            // length varies with how many flags are up
            ClipText = expand || clip,
            HorizontalExpand = expand,
        };
    }

    public void Update(ANPRCPanelContext context, ANPRCBacklight backlight, string prompt, bool promptActive)
    {
        var state = context.State;

        _palette = ANPRCPanelStyle.Palette(backlight);
        _lit = context.Powered;

        Screen.Lit = context.Powered;
        Screen.Backlight = backlight;
        _lastTransmit = state.Info.LastTransmit;
        _lastReceive = state.Info.LastReceive;

        // the glass is lit by the set being switched on. whether the pack is worn is the air
        // side of the question, and BIT below is where that gets reported
        var live = context.Powered;

        // ----- status strip --------------------------------------------------------------------

        var secured = !string.IsNullOrEmpty(state.CryptoFaction) && !state.CryptoStale;

        _secLabel.Text = !live ? "----"
            : Loc.GetString(state.Mode == RadioMode.PlainText ? "anprc-fp-rd-clear"
                : secured ? "anprc-fp-rd-sec"
                : "anprc-fp-rd-unsec");

        _secLabel.FontColorOverride = !live ? _palette.Unlit
            : state.Mode == RadioMode.PlainText ? _palette.Warn
            : secured ? _palette.Bright
            : _palette.Bad;

        // the bar is the set's link to whatever anchor is carrying the net, the same number the
        // range system gates traffic on. a raw frequency has no anchor behind it, so it reads
        // as not applicable rather than as a full-scale lie
        if (!context.Ready)
        {
            _signalLabel.Text = Loc.GetString("anprc-fp-rd-sig", ("bars", ANPRCPanelContext.Bars(0, 8)));
            _signalLabel.FontColorOverride = _palette.Unlit;
        }
        else if (state.Info.LinkQuality < 0f)
        {
            _signalLabel.Text = Loc.GetString("anprc-fp-rd-sig", ("bars", "--DIR--"));
            _signalLabel.FontColorOverride = _palette.Dim;
        }
        else
        {
            var bars = (int) MathF.Ceiling(Math.Clamp(state.Info.LinkQuality, 0f, 1f) * 8f);

            _signalLabel.Text = Loc.GetString("anprc-fp-rd-sig", ("bars", ANPRCPanelContext.Bars(bars, 8)));
            _signalLabel.FontColorOverride = bars == 0
                ? _palette.Bad
                : bars <= 3
                    ? _palette.Warn
                    : _palette.Mid;
        }

        var cells = state.HasBattery ? (int) MathF.Round(state.BatteryFraction * 4f) : 0;

        _batteryLabel.Text = Loc.GetString("anprc-fp-rd-bat", ("bars", state.HasBattery ? ANPRCPanelContext.Bars(cells, 4) : "----"));
        _batteryLabel.FontColorOverride = !state.HasBattery
            ? _palette.Bad
            : state.BatteryFraction <= 0.2f
                ? _palette.Warn
                : _palette.Mid;

        // ----- tuned net -----------------------------------------------------------------------

        if (state.SweepEnabled)
        {
            _slotLabel.Text = Loc.GetString("anprc-fp-rd-search-slot");
            _frequencyLabel.Text = ANPRCPanelContext.FormatFrequency(state.SweepPosition);
            _netLabel.Text = Loc.GetString(state.Expert.SweepDwellKilohertz >= 0 ? "anprc-fp-rd-dwell" : "anprc-fp-rd-search");
            _bandLabel.Text = Loc.GetString("anprc-fp-rd-search-band");

            _slotLabel.FontColorOverride = _palette.Warn;
            _frequencyLabel.FontColorOverride = _palette.Warn;
            _netLabel.FontColorOverride = _palette.Warn;
            _bandLabel.FontColorOverride = _palette.Warn;
        }
        else if (context.HasActiveNet)
        {
            _slotLabel.Text = context.SlotLabel(state.ActiveSlot);
            _frequencyLabel.Text = ANPRCPanelContext.FormatFrequency(context.ActiveFrequency);
            _netLabel.Text = context.ActiveIsDirect
                ? Loc.GetString("anprc-fp-rd-direct")
                : context.ActiveChannel!.LocalizedName.ToUpperInvariant();

            var band = ANPRCPanelContext.BandName(context.ActiveFrequency);

            _bandLabel.Text = Loc.GetString(context.ActiveChannel is { LongRange: true } ? "anprc-fp-rd-band-sat" : "anprc-fp-rd-band-los", ("band", band));

            _slotLabel.FontColorOverride = live ? _palette.Bright : _palette.Unlit;
            _frequencyLabel.FontColorOverride = live ? _palette.Bright : _palette.Unlit;
            _netLabel.FontColorOverride = live ? _palette.Mid : _palette.Unlit;
            _bandLabel.FontColorOverride = live ? _palette.Dim : _palette.Unlit;
        }
        else
        {
            _slotLabel.Text = "--";
            _frequencyLabel.Text = "---.---";
            _netLabel.Text = Loc.GetString(state.ActiveSlot < 0 ? "anprc-fp-rd-no-net-memory" : "anprc-fp-rd-slot-empty");
            _bandLabel.Text = "---";

            _slotLabel.FontColorOverride = _palette.Unlit;
            _frequencyLabel.FontColorOverride = _palette.Unlit;
            _netLabel.FontColorOverride = live ? _palette.Warn : _palette.Unlit;
            _bandLabel.FontColorOverride = _palette.Unlit;
        }

        // ----- working settings ----------------------------------------------------------------

        var extras = string.Empty;

        void Flag(bool on, string key)
        {
            if (on)
                extras += "  " + Loc.GetString(key);
        }

        Flag(state.MonitorEnabled, "anprc-fp-rd-flag-mon");
        Flag(state.ScanEnabled, "anprc-fp-rd-flag-scan");
        Flag(state.Expert.Burst, "anprc-fp-rd-flag-burst");
        Flag(state.Expert.PowerSave, "anprc-fp-rd-flag-ps");
        Flag(state.Expert.PriorityWatchSlot >= 0, "anprc-fp-rd-flag-pri");
        Flag(state.Expert.Emcon, "anprc-fp-rd-flag-emcon");
        Flag(state.Expert.RetransSlotA >= 0, "anprc-fp-rd-flag-rxmt");

        _settingsLabel.Text = Loc.GetString("anprc-fp-rd-settings",
            ("mode", ANPRCPanelContext.ModeShort(state.Mode)),
            ("squelch", state.SquelchLevel),
            ("power", ANPRCPanelContext.PowerShort(state.TxPower))) + extras;

        _settingsLabel.FontColorOverride = live ? _palette.Dim : _palette.Unlit;

        var station = !string.IsNullOrEmpty(state.Callsign)
            ? state.Callsign
            : !string.IsNullOrEmpty(state.WearerCallsign)
                ? Loc.GetString("anprc-fp-callsign-auto", ("callsign", state.WearerCallsign))
                : Loc.GetString("anprc-fp-unknown");

        _stationLabel.Text = Loc.GetString("anprc-fp-rd-stn", ("station", station));
        _stationLabel.FontColorOverride = live ? _palette.Dim : _palette.Unlit;

        // the built-in test, which is the set's own word on whether it can work. read in the
        // order the faults actually stack: no cell, then switched off, then not deployed, and
        // only then the net itself. a stowed set that was also off used to report STOWED and
        // send an operator hunting for a strap when the switch was the problem
        if (!state.HasBattery)
        {
            _bitLabel.Text = Loc.GetString("anprc-fp-rd-bit", ("result", Loc.GetString("anprc-fp-rd-bit-no-cell")));
            _bitLabel.FontColorOverride = _palette.Bad;
        }
        else if (!state.Enabled)
        {
            _bitLabel.Text = Loc.GetString("anprc-fp-rd-bit", ("result", Loc.GetString("anprc-fp-off")));
            _bitLabel.FontColorOverride = _palette.Unlit;
        }
        else if (!context.Deployed)
        {
            _bitLabel.Text = Loc.GetString("anprc-fp-rd-bit", ("result", Loc.GetString("anprc-fp-stowed")));
            _bitLabel.FontColorOverride = _palette.Warn;
        }
        else if (state.SweepEnabled)
        {
            _bitLabel.Text = Loc.GetString("anprc-fp-rd-bit", ("result", Loc.GetString("anprc-fp-rd-bit-search")));
            _bitLabel.FontColorOverride = _palette.Warn;
        }
        else if (!context.HasActiveNet)
        {
            _bitLabel.Text = Loc.GetString("anprc-fp-rd-bit", ("result", Loc.GetString("anprc-fp-no-net")));
            _bitLabel.FontColorOverride = _palette.Warn;
        }
        else
        {
            _bitLabel.Text = Loc.GetString("anprc-fp-rd-bit", ("result", Loc.GetString("anprc-fp-rd-bit-pass")));
            _bitLabel.FontColorOverride = _palette.Bright;
        }

        // ----- prompt --------------------------------------------------------------------------

        _promptLabel.Text = prompt;
        _promptLabel.FontColorOverride = promptActive ? _palette.Bright : _palette.Dim;
        _promptLabel.Visible = !string.IsNullOrEmpty(prompt);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // the lamps run off the clock rather than off a state push, so they fade out on
        // their own instead of staying lit until the next update happens to land
        var now = _timing.CurTime;

        _txLampLit = SetLamp(_txLamp, Loc.GetString("anprc-fp-lamp-tx"), _lastTransmit, TxLampHold, now, _palette.Bad, _txLampLit);
        _rxLampLit = SetLamp(_rxLamp, Loc.GetString("anprc-fp-lamp-rx"), _lastReceive, RxLampHold, now, _palette.Bright, _rxLampLit);
    }

    private bool SetLamp(
        Label lamp,
        string legend,
        TimeSpan at,
        TimeSpan hold,
        TimeSpan now,
        Color lit,
        bool? drawn)
    {
        var active = _lit && at > TimeSpan.Zero && now - at <= hold;

        // the colour is a plain field, but the text invalidates the label's measure, so it is
        // only rewritten when the lamp actually goes on or off
        lamp.FontColorOverride = active ? lit : _palette.Unlit;

        // a lit lamp closes its brackets. the bullet glyphs this used are not in RobotoMono and
        // came out of the font as missing-glyph boxes, and the width never changes this way
        if (drawn != active)
            lamp.Text = active ? "[" + legend + "]" : " " + legend + " ";

        return active;
    }

    /// <summary>A hairline across the glass, drawn in whatever the backlight is currently doing.</summary>
    private sealed class ANPRCRule : Control
    {
        private readonly Func<Color> _color;

        public ANPRCRule(Func<Color> color)
        {
            _color = color;
            MouseFilter = MouseFilterMode.Ignore;
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize) => new(0f, 1f);

        protected override void Draw(DrawingHandleScreen handle) => handle.DrawRect(PixelSizeBox, _color());
    }
}
