using Content.Shared.CMU14.Radio;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     The set's own screen, kept to the three questions an operator glances at it for: what am
///     I talking on, is it secure, and is it reaching anyone. Everything here is a reading off the
///     server's state; nothing on the screen is clickable.
/// </summary>
public sealed class ANPRCStatusDisplay : PanelContainer
{
    private readonly Label _slot;
    private readonly Label _frequency;
    private readonly Label _mode;
    private readonly Label _net;
    private readonly Label _talk;
    private readonly Label _signal;
    private readonly Label _battery;
    private readonly Label _relay;
    private readonly Label _station;

    public ANPRCStatusDisplay()
    {
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = ANPRCUi.LcdBack,
            BorderColor = ANPRCUi.LcdEdge,
            BorderThickness = new Thickness(2),
        };

        var column = ANPRCUi.Column(2);
        column.Margin = new Thickness(8, 5);
        AddChild(column);

        var top = ANPRCUi.Row(8);
        _slot = Mono(string.Empty, 14, true);
        _frequency = Mono("---.---", 16, true);
        _mode = Mono(string.Empty, 11, true);
        _mode.HorizontalExpand = true;
        _mode.Align = Label.AlignMode.Right;

        top.AddChild(_slot);
        top.AddChild(_frequency);
        top.AddChild(_mode);
        column.AddChild(top);

        var netRow = ANPRCUi.Row(8);
        _net = Mono(string.Empty, 11, true);
        _station = Mono(string.Empty, 10);
        _station.HorizontalExpand = true;
        _station.Align = Label.AlignMode.Right;
        netRow.AddChild(_net);
        netRow.AddChild(_station);
        column.AddChild(netRow);

        _talk = Mono(string.Empty, 11, true);
        column.AddChild(_talk);

        var meters = ANPRCUi.Row(10);
        _signal = Mono(string.Empty, 10);
        _battery = Mono(string.Empty, 10);
        _relay = Mono(string.Empty, 10);
        _relay.HorizontalExpand = true;
        _relay.Align = Label.AlignMode.Right;

        meters.AddChild(_signal);
        meters.AddChild(_battery);
        meters.AddChild(_relay);
        column.AddChild(meters);
    }

    // readouts are bounded and short, so they are measured rather than clipped: a clipped label
    // in a horizontal row measures as nothing and draws nothing
    private static Label Mono(string text, int size, bool bold = false)
    {
        return new Label
        {
            Text = text,
            FontOverride = ANPRCUi.Mono(size, bold),
            FontColorOverride = ANPRCUi.LcdOff,
        };
    }

    public void Update(ANPRCPanelData data)
    {
        var state = data.State;
        var lit = data.Powered;

        // ----- what the set is tuned to ----------------------------------------------------------

        if (state.SweepEnabled && lit)
        {
            _slot.Text = Loc.GetString("anprc-fp-rd-bit-search");
            _frequency.Text = ANPRCPanelData.FormatFrequency(state.SweepPosition);
            _net.Text = Loc.GetString("anprc-op-lcd-searching");
            Colour(ANPRCUi.Warn, _slot, _frequency, _net);
        }
        else if (data.HasActiveNet)
        {
            _slot.Text = data.SlotLabel(state.ActiveSlot);
            _frequency.Text = ANPRCPanelData.FormatFrequency(data.ActiveFrequency);
            _net.Text = data.ActiveIsDirect
                ? Loc.GetString("anprc-op-lcd-direct")
                : data.ActiveChannel!.LocalizedName.ToUpperInvariant();

            Colour(lit ? ANPRCUi.LcdBright : ANPRCUi.LcdOff, _slot, _frequency);
            Colour(lit ? ANPRCUi.LcdMid : ANPRCUi.LcdOff, _net);
        }
        else
        {
            _slot.Text = "--";
            _frequency.Text = "---.---";
            _net.Text = Loc.GetString(state.SlotLabels.Count == 0
                ? "anprc-op-lcd-no-memories"
                : "anprc-op-lcd-no-net");
            Colour(ANPRCUi.LcdOff, _slot, _frequency);
            Colour(lit ? ANPRCUi.Warn : ANPRCUi.LcdOff, _net);
        }

        // ----- mode and security ----------------------------------------------------------------

        var mode = ANPRCPanelData.ModeShort(state.Mode);

        _mode.Text = !lit ? Loc.GetString("anprc-op-lcd-off")
            : state.Mode == RadioMode.PlainText ? Loc.GetString("anprc-op-lcd-clear", ("mode", mode))
            : data.Secured ? Loc.GetString("anprc-op-lcd-secure", ("mode", mode))
            : Loc.GetString("anprc-op-lcd-unsecured", ("mode", mode));

        _mode.FontColorOverride = !lit ? ANPRCUi.LcdOff
            : state.Mode == RadioMode.PlainText ? ANPRCUi.Warn
            : data.Secured ? ANPRCUi.LcdBright
            : ANPRCUi.Bad;

        // ----- how to talk ----------------------------------------------------------------------

        // the one thing nobody can find out from the panel alone, so it sits on the screen
        // rather than in a help page
        if (data.Ready && !state.MonitorEnabled)
        {
            _talk.Text = Loc.GetString("anprc-op-lcd-talk",
                ("label", data.SlotLabel(state.ActiveSlot)));
            _talk.FontColorOverride = ANPRCUi.LcdBright;
        }
        else
        {
            _talk.Text = Loc.GetString(!lit ? "anprc-op-lcd-talk-off"
                : state.MonitorEnabled ? "anprc-op-lcd-talk-monitor"
                : state.SweepEnabled ? "anprc-op-lcd-talk-searching"
                : !data.Deployed ? "anprc-op-lcd-talk-stowed"
                : "anprc-op-lcd-talk-no-net");
            _talk.FontColorOverride = lit ? ANPRCUi.LcdDim : ANPRCUi.LcdOff;
        }

        // ----- meters -----------------------------------------------------------------------------

        var link = data.Relay.LinkQuality;

        if (!data.Ready)
        {
            _signal.Text = Loc.GetString("anprc-op-lcd-signal", ("bars", ANPRCUi.Bars(0, 6)));
            _signal.FontColorOverride = ANPRCUi.LcdOff;
        }
        else if (link < 0f)
        {
            // a raw frequency or an ungated net has no anchor to measure against
            _signal.Text = Loc.GetString("anprc-op-lcd-signal-direct");
            _signal.FontColorOverride = ANPRCUi.LcdDim;
        }
        else
        {
            var bars = (int) MathF.Ceiling(Math.Clamp(link, 0f, 1f) * 6f);
            _signal.Text = Loc.GetString("anprc-op-lcd-signal", ("bars", ANPRCUi.Bars(bars, 6)));
            _signal.FontColorOverride = bars == 0 ? ANPRCUi.Bad : bars <= 2 ? ANPRCUi.Warn : ANPRCUi.LcdMid;
        }

        var cells = state.HasBattery ? (int) MathF.Round(state.BatteryFraction * 4f) : 0;

        _battery.Text = state.HasBattery
            ? Loc.GetString("anprc-op-lcd-battery", ("bars", ANPRCUi.Bars(cells, 4)))
            : Loc.GetString("anprc-op-lcd-no-battery");
        _battery.FontColorOverride = !state.HasBattery ? ANPRCUi.Bad
            : state.BatteryFraction <= 0.2f ? ANPRCUi.Warn
            : lit ? ANPRCUi.LcdMid : ANPRCUi.LcdDim;

        // the relay is the half of the job that happens for other people, and it used to be
        // invisible. it gets a reading of its own
        if (data.Relay.Relaying)
        {
            _relay.Text = Loc.GetString("anprc-op-lcd-relay",
                ("count", data.Relay.RelayedNets.Count),
                ("range", (int) data.Relay.FullRange));
            _relay.FontColorOverride = ANPRCUi.LcdBright;
        }
        else
        {
            _relay.Text = Loc.GetString("anprc-op-lcd-relay-none");
            _relay.FontColorOverride = lit ? ANPRCUi.Warn : ANPRCUi.LcdOff;
        }

        _station.Text = Loc.GetString("anprc-op-lcd-station", ("callsign", data.Callsign));
        _station.FontColorOverride = lit ? ANPRCUi.LcdDim : ANPRCUi.LcdOff;
    }

    private static void Colour(Color color, params Label[] labels)
    {
        foreach (var label in labels)
        {
            label.FontColorOverride = color;
        }
    }
}
