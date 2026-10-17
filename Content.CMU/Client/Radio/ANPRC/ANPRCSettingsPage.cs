using System.Linq;
using System.Text;
using Content.Shared.CMU14.Radio;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     How the set is being worked. The guided panel runs the set on AUTO: the factory waveform,
///     output and squelch, and the station answering to the wearer's own callsign. Nothing an operator
///     needs to get on the air is missing - monitor and scan are still here - but the tactical levers
///     (power, waveform, techniques, a station callsign) live on the faceplate, and that is the reward
///     for learning it. When somebody has worked the set from the faceplate the page says what is set
///     and offers one button to put it all back.
/// </summary>
public sealed class ANPRCSettingsPage : BoxContainer
{
    private readonly ANPRCRadioActions _radio;

    private readonly RichTextLabel _auto;
    private readonly Button _returnToAuto;

    private readonly Button _monitor;
    private readonly Button _scan;

    private readonly Label _callsign;
    private readonly Button _directory;

    private bool _scanOn;

    public ANPRCSettingsPage(ANPRCRadioActions radio)
    {
        _radio = radio;

        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 5;

        // ----- how the set is running ------------------------------------------------------------

        AddChild(ANPRCUi.Heading(Loc.GetString("anprc-op-set-auto-heading")));

        _auto = ANPRCUi.Wrapped(string.Empty, ANPRCUi.Text);
        AddChild(_auto);

        _returnToAuto = ANPRCUi.Button(Loc.GetString("anprc-op-set-return-to-auto"),
            Loc.GetString("anprc-op-set-return-to-auto-tooltip"), () => _radio.ReturnToAuto());
        AddChild(_returnToAuto);

        AddChild(ANPRCUi.Wrapped(Loc.GetString("anprc-op-set-expert-hint"), ANPRCUi.TextDim));

        // ----- receive behaviour -----------------------------------------------------------------

        AddChild(ANPRCUi.Heading(Loc.GetString("anprc-op-set-receive")));

        _monitor = ANPRCUi.Button(string.Empty, Loc.GetString("anprc-op-set-monitor-help"), () => _radio.ToggleMonitor());
        AddChild(_monitor);
        AddChild(ANPRCUi.Wrapped(Loc.GetString("anprc-op-set-monitor-help"), ANPRCUi.TextDim));

        _scan = ANPRCUi.Button(string.Empty, Loc.GetString("anprc-op-set-scan-help"), () => _radio.SetScan(!_scanOn));
        AddChild(_scan);
        AddChild(ANPRCUi.Wrapped(Loc.GetString("anprc-op-set-scan-help"), ANPRCUi.TextDim));

        // ----- station ---------------------------------------------------------------------------

        AddChild(ANPRCUi.Heading(Loc.GetString("anprc-op-set-station")));

        _callsign = ANPRCUi.Label(string.Empty, ANPRCUi.Good, ANPRCUi.Mono(12, true));
        AddChild(_callsign);

        AddChild(ANPRCUi.Wrapped(Loc.GetString("anprc-op-set-callsign-guided-help"), ANPRCUi.TextDim));

        _directory = ANPRCUi.Button(Loc.GetString("anprc-op-set-directory"), Loc.GetString("anprc-op-set-directory-tooltip"),
            () => _radio.OpenDirectory());
        AddChild(_directory);

        AddChild(ANPRCUi.Button(Loc.GetString("anprc-op-set-phone"), Loc.GetString("anprc-op-set-phone-tooltip"),
            () => _radio.OpenPhone()));
    }

    public void Update(ANPRCPanelData data)
    {
        var state = data.State;
        var expert = state.Expert;
        var powered = data.Powered;

        ANPRCUi.SetWrapped(_auto, DescribeSettings(data), expert.OffAuto ? ANPRCUi.Warn : ANPRCUi.Good);
        _returnToAuto.Visible = expert.OffAuto;

        _scanOn = state.ScanEnabled;

        _monitor.Text = Loc.GetString(state.MonitorEnabled ? "anprc-op-set-monitor-on" : "anprc-op-set-monitor-off");
        _monitor.Pressed = state.MonitorEnabled;
        _monitor.Disabled = !powered;

        _scan.Text = Loc.GetString(_scanOn ? "anprc-op-set-scan-on" : "anprc-op-set-scan-off");
        _scan.Pressed = _scanOn;
        _scan.Disabled = !powered;

        _callsign.Text = !string.IsNullOrEmpty(state.Callsign)
            ? Loc.GetString("anprc-op-set-callsign-manual", ("callsign", state.Callsign))
            : !string.IsNullOrEmpty(state.WearerCallsign)
                ? Loc.GetString("anprc-op-set-callsign-from-wearer", ("callsign", state.WearerCallsign))
                : Loc.GetString("anprc-op-set-callsign-unknown");
        _callsign.FontColorOverride = string.IsNullOrEmpty(state.Callsign) && string.IsNullOrEmpty(state.WearerCallsign)
            ? ANPRCUi.Warn
            : ANPRCUi.Good;

        _directory.Visible = data.Relay.HasDirectory;
    }

    /// <summary>
    ///     AUTO in one line when nothing is changed; otherwise each thing somebody set on the
    ///     faceplate, in words, so the operator knows what the one button would undo.
    /// </summary>
    private static string DescribeSettings(ANPRCPanelData data)
    {
        var state = data.State;
        var expert = state.Expert;

        if (!expert.OffAuto)
            return Loc.GetString("anprc-op-set-auto-on");

        var changes = new List<string>();

        if (state.Mode != RadioMode.FrequencyHopping)
            changes.Add(Loc.GetString("anprc-op-set-changed-mode", ("mode", ANPRCPanelContext.ModeName(state.Mode))));

        if (state.TxPower != RadioTxPower.Medium)
            changes.Add(Loc.GetString("anprc-op-set-changed-power", ("power", ANPRCPanelContext.PowerShort(state.TxPower))));

        if (state.SquelchLevel != 3)
            changes.Add(Loc.GetString("anprc-op-set-changed-squelch", ("level", state.SquelchLevel)));

        if (!string.IsNullOrEmpty(state.Callsign))
            changes.Add(Loc.GetString("anprc-op-set-changed-callsign", ("callsign", state.Callsign)));

        if (expert.Burst)
            changes.Add(Loc.GetString("anprc-op-set-changed-burst"));

        if (expert.PowerSave)
            changes.Add(Loc.GetString("anprc-op-set-changed-power-save"));

        if (expert.PriorityWatchSlot >= 0)
            changes.Add(Loc.GetString("anprc-op-set-changed-priority", ("slot", data.SlotLabel(expert.PriorityWatchSlot))));

        if (expert.Emcon)
            changes.Add(Loc.GetString("anprc-op-set-changed-emcon"));

        if (expert.RetransSlotA >= 0)
            changes.Add(Loc.GetString("anprc-op-set-changed-retrans"));

        if (state.SweepEnabled)
            changes.Add(Loc.GetString("anprc-op-set-changed-search"));

        var text = new StringBuilder(Loc.GetString("anprc-op-set-auto-off"));

        foreach (var change in changes.Where(change => !string.IsNullOrEmpty(change)))
        {
            text.Append('\n').Append("- ").Append(change);
        }

        return text.ToString();
    }
}
