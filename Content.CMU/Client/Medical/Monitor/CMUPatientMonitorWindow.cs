using System.Numerics;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Client.CMU14.Temperature;
using Content.Shared.CMU14.Medical.Monitor;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.CMU14.Medical.Monitor;

/// <summary>
/// The monitor/defibrillator's screen: ECG, pleth and capnography traces with their numerics, the last NIBP,
/// the selected energy and a code summary, above a row of hardware keys.
/// </summary>
public sealed class CMUPatientMonitorWindow : DefaultWindow
{
    private static readonly Color Casing = Color.FromHex("#35383B");
    private static readonly Color CasingDark = Color.FromHex("#232527");
    private static readonly Color Screen = Color.FromHex("#050807");
    private static readonly Color EcgColor = Color.FromHex("#3CFF6B");
    private static readonly Color SpO2Color = Color.FromHex("#3CD8FF");
    private static readonly Color Co2Color = Color.FromHex("#FFE14A");
    private static readonly Color NibpColor = Color.FromHex("#F2F2F2");
    private static readonly Color TempColor = Color.FromHex("#FF9FE5");
    private static readonly Color AlarmRed = Color.FromHex("#FF3B30");
    private static readonly Color AlarmYellow = Color.FromHex("#FFC300");
    private static readonly Color Dim = Color.FromHex("#8A9A90");
    private static readonly Color ShockOrange = Color.FromHex("#FF8A3D");

    public event Action<bool>? OnEnergy;
    public event Action? OnNibp;
    public event Action? OnSilence;
    public event Action? OnDisconnect;
    public event Action? OnAnalyze;
    public event Action? OnCharge;
    public event Action? OnShock;

    private readonly Label _patient;
    private readonly Label _elapsed;
    private readonly Label _clock;
    private readonly Label _battery;
    private readonly PanelContainer _banner;
    private readonly StyleBoxFlat _bannerStyle;
    private readonly Label _bannerText;

    private readonly CMUMonitorWaveform _ecg;
    private readonly CMUMonitorWaveform _pleth;
    private readonly CMUMonitorWaveform _co2;

    private readonly CMUMonitorNumeric _hr;
    private readonly CMUMonitorNumeric _heart;
    private readonly CMUMonitorNumeric _spo2;
    private readonly CMUMonitorNumeric _nibp;
    private readonly CMUMonitorNumeric _etco2;
    private readonly CMUMonitorNumeric _energy;
    private readonly CMUMonitorNumeric _temp;

    private readonly BoxContainer _log;
    private readonly ScrollContainer _logScroll;
    private readonly Button _silence;
    private readonly Button _disconnect;
    private readonly Button _nibpButton;
    private readonly Button _analyzeButton;
    private readonly Button _chargeButton;
    private readonly Button _shockButton;
    private int _logCount = -1;
    private IReadOnlyList<TimeSpan>? _beats;
    private CMUMonitorLogEntry? _lastEntry;

    public CMUPatientMonitorWindow()
    {
        Title = Loc.GetString("cmu-monitor-title");
        MinSize = new Vector2(800, 700);
        SetSize = new Vector2(820, 720);

        var casing = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Casing },
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        var column = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(10), SeparationOverride = 8 };
        casing.AddChild(column);
        Contents.AddChild(casing);

        // Screen bezel
        var bezel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = CasingDark, ContentMarginLeftOverride = 6, ContentMarginRightOverride = 6, ContentMarginTopOverride = 6, ContentMarginBottomOverride = 6 },
            VerticalExpand = true,
        };
        var screen = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Screen },
            VerticalExpand = true,
        };
        bezel.AddChild(screen);
        column.AddChild(bezel);

        var screenBox = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(6), SeparationOverride = 4, VerticalExpand = true };
        screen.AddChild(screenBox);

        // Status bar
        var status = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 14 };
        _patient = new Label { FontColorOverride = Color.White, HorizontalExpand = true, ClipText = true };
        _elapsed = new Label { FontColorOverride = Dim };
        _clock = new Label { FontColorOverride = Dim };
        _battery = new Label { FontColorOverride = Dim };
        status.AddChild(_patient);
        status.AddChild(_elapsed);
        status.AddChild(_clock);
        status.AddChild(_battery);
        screenBox.AddChild(status);

        // Alarm / rhythm banner
        _bannerStyle = new StyleBoxFlat { BackgroundColor = Color.Transparent };
        _banner = new PanelContainer { PanelOverride = _bannerStyle };
        _bannerText = new Label { Align = Label.AlignMode.Center, Margin = new Thickness(0, 1), StyleClasses = { "LabelHeading" } };
        _banner.AddChild(_bannerText);
        screenBox.AddChild(_banner);

        // Traces and numerics
        var main = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 6, VerticalExpand = true };
        var traces = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 2, HorizontalExpand = true, VerticalExpand = true };
        _ecg = new CMUMonitorWaveform { Kind = CMUMonitorWaveKind.Ecg, TraceColor = EcgColor, Label = Loc.GetString("cmu-monitor-trace-ecg"), VerticalExpand = true, SizeFlagsStretchRatio = 1.6f };
        _pleth = new CMUMonitorWaveform { Kind = CMUMonitorWaveKind.Pleth, TraceColor = SpO2Color, Label = Loc.GetString("cmu-monitor-trace-pleth"), VerticalExpand = true };
        _co2 = new CMUMonitorWaveform { Kind = CMUMonitorWaveKind.Capnography, TraceColor = Co2Color, Label = Loc.GetString("cmu-monitor-trace-co2"), VerticalExpand = true };
        traces.AddChild(_ecg);
        traces.AddChild(_pleth);
        traces.AddChild(_co2);
        main.AddChild(traces);

        var numerics = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 4, MinWidth = 160 };
        _hr = Numeric("cmu-monitor-hr", "cmu-monitor-unit-bpm", EcgColor);
        _spo2 = Numeric("cmu-monitor-spo2", "cmu-monitor-unit-percent", SpO2Color);
        _nibp = Numeric("cmu-monitor-nibp", "cmu-monitor-unit-mmhg", NibpColor);
        _etco2 = Numeric("cmu-monitor-etco2", "cmu-monitor-unit-mmhg", Co2Color);
        _heart = Numeric("cmu-monitor-heart", "cmu-monitor-unit-none", EcgColor);
        numerics.AddChild(_hr);
        numerics.AddChild(_heart);
        numerics.AddChild(_spo2);
        numerics.AddChild(_nibp);
        numerics.AddChild(_etco2);
        main.AddChild(numerics);
        screenBox.AddChild(main);

        // Energy, temperature and the code summary
        var bottom = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 6, MinHeight = 110 };
        var bottomLeft = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 4, MinWidth = 150 };
        _energy = Numeric("cmu-monitor-energy", "cmu-monitor-unit-joules", AlarmYellow);
        _temp = Numeric("cmu-monitor-temp", "cmu-monitor-unit-celsius", TempColor);
        bottomLeft.AddChild(_energy);
        bottomLeft.AddChild(_temp);
        bottom.AddChild(bottomLeft);

        var summary = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
        summary.AddChild(new Label { Text = Loc.GetString("cmu-monitor-code-summary"), FontColorOverride = Dim });
        _logScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _log = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
        _logScroll.AddChild(_log);
        summary.AddChild(_logScroll);
        bottom.AddChild(summary);
        screenBox.AddChild(bottom);

        // Hardware keys
        var keys = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 6, HorizontalAlignment = HAlignment.Center };
        _analyzeButton = Key("cmu-monitor-key-analyze", () => OnAnalyze?.Invoke());
        keys.AddChild(_analyzeButton);
        _chargeButton = Key("cmu-monitor-key-charge", () => OnCharge?.Invoke());
        _chargeButton.ModulateSelfOverride = AlarmYellow;
        keys.AddChild(_chargeButton);
        _shockButton = Key("cmu-monitor-key-shock", () => OnShock?.Invoke());
        _shockButton.ModulateSelfOverride = ShockOrange;
        keys.AddChild(_shockButton);
        keys.AddChild(Key("cmu-monitor-key-energy-down", () => OnEnergy?.Invoke(false)));
        keys.AddChild(Key("cmu-monitor-key-energy-up", () => OnEnergy?.Invoke(true)));
        _nibpButton = Key("cmu-monitor-key-nibp", () => OnNibp?.Invoke());
        keys.AddChild(_nibpButton);
        _silence = Key("cmu-monitor-key-alarms", () => OnSilence?.Invoke());
        keys.AddChild(_silence);
        _disconnect = Key("cmu-monitor-key-disconnect", () => OnDisconnect?.Invoke());
        keys.AddChild(_disconnect);
        column.AddChild(keys);
    }

    private static CMUMonitorNumeric Numeric(string label, string unit, Color color)
    {
        return new CMUMonitorNumeric
        {
            Label = Loc.GetString(label),
            Unit = Loc.GetString(unit),
            Color = color,
            VerticalExpand = true,
        };
    }

    private static Button Key(string text, Action act)
    {
        var button = new Button { Text = Loc.GetString(text), MinWidth = 90, MinHeight = 30 };
        button.OnPressed += _ => act();
        return button;
    }

    public void UpdateState(CMUPatientMonitorBuiState state)
    {
        var connected = state.PatientName != null;

        _patient.Text = !state.PoweredOn
            ? string.Empty
            : connected
                ? Loc.GetString("cmu-monitor-patient", ("name", state.PatientName!), ("status", Loc.GetString(StatusLoc(state.Status))))
                : Loc.GetString("cmu-monitor-no-patient");
        _elapsed.Text = connected ? Loc.GetString("cmu-monitor-elapsed", ("time", state.Elapsed)) : string.Empty;
        _clock.Text = state.Clock;
        _battery.Text = state.Battery is { } battery
            ? Loc.GetString("cmu-monitor-battery", ("percent", battery))
            : Loc.GetString("cmu-monitor-battery-none");
        _battery.FontColorOverride = state.Battery is < 20 ? AlarmRed : Dim;

        UpdateBanner(state, connected);
        UpdateTraces(state, connected);
        UpdateNumerics(state, connected);
        UpdateLog(state);

        _nibpButton.Disabled = !connected;
        _analyzeButton.Disabled = !connected || state.Analyzing;
        _chargeButton.Disabled = !connected || state.Charging || state.Charged;
        _shockButton.Disabled = !connected || !state.Charged;
        _disconnect.Disabled = !state.LeadsAttached;
        _silence.Text = Loc.GetString(state.AlarmSilenced ? "cmu-monitor-key-alarms-silenced" : "cmu-monitor-key-alarms");
    }

    private void UpdateBanner(CMUPatientMonitorBuiState state, bool connected)
    {
        string text;
        Color color;
        var background = Color.Transparent;

        if (!state.PoweredOn)
        {
            text = Loc.GetString("cmu-monitor-banner-off");
            color = Dim;
        }
        else if (connected && state.Charged)
        {
            text = Loc.GetString("cmu-monitor-banner-charged", ("joules", state.Joules));
            color = Color.White;
            background = ShockOrange;
        }
        else if (connected && state.Charging)
        {
            text = Loc.GetString("cmu-monitor-banner-charging", ("joules", state.Joules));
            color = Color.Black;
            background = AlarmYellow;
        }
        else if (connected && state.Analyzing)
        {
            text = Loc.GetString("cmu-monitor-banner-analyzing");
            color = Color.Black;
            background = Color.White;
        }
        else if (!connected)
        {
            text = Loc.GetString("cmu-monitor-banner-leads-off");
            color = Color.Black;
            background = AlarmYellow;
        }
        else if (state.Rhythm == CMUMonitorRhythm.VentricularFibrillation)
        {
            text = Loc.GetString("cmu-monitor-banner-vfib");
            color = Color.White;
            background = AlarmRed;
        }
        else if (state.Rhythm == CMUMonitorRhythm.Asystole)
        {
            text = Loc.GetString("cmu-monitor-banner-asystole");
            color = Color.White;
            background = AlarmRed;
        }
        else if (state.SpO2 is < 90)
        {
            text = Loc.GetString("cmu-monitor-banner-spo2-low");
            color = Color.Black;
            background = AlarmYellow;
        }
        else if (state.NibpSystolic is < 90)
        {
            text = Loc.GetString("cmu-monitor-banner-hypotension");
            color = Color.Black;
            background = AlarmYellow;
        }
        else
        {
            text = Loc.GetString(CMUMonitorRhythms.LocId(state.Rhythm));
            color = EcgColor;
        }

        if (state.AlarmSilenced)
            text += " " + Loc.GetString("cmu-monitor-banner-silenced");

        _bannerText.Text = text;
        _bannerText.FontColorOverride = color;
        _bannerStyle.BackgroundColor = background;
    }

    /// <summary>
    /// Points the traces at the monitor's beats, so each QRS drawn is one the beep played.
    /// </summary>
    public void SetMonitor(EntityUid monitor)
    {
        if (!IoCManager.Resolve<IEntityManager>().TryGetComponent(monitor, out CMUPatientMonitorComponent? comp))
            return;

        _beats = comp.BeatTimes;
        _ecg.Beats = _beats;
    }

    private void UpdateTraces(CMUPatientMonitorBuiState state, bool connected)
    {
        _ecg.Rhythm = connected ? state.Rhythm : CMUMonitorRhythm.None;
        _ecg.HeartRate = state.HeartRate;
        _ecg.Message = connected ? null : Loc.GetString("cmu-monitor-trace-leads-off");

        _pleth.HeartRate = state.HeartRate;
        _pleth.Beats = state.SpO2 != null ? _beats : null;
        _pleth.Amplitude = state.SpO2 is { } spo2 ? Math.Clamp((spo2 - 40) / 58f, 0.25f, 1f) : 1f;
        _pleth.Message = connected ? null : Loc.GetString("cmu-monitor-trace-no-sensor");

        _co2.RespiratoryRate = state.RespiratoryRate;
        _co2.Amplitude = Math.Clamp(state.EtCO2 / 40f, 0.2f, 1.2f);
        _co2.Message = connected ? null : Loc.GetString("cmu-monitor-trace-no-sensor");

        // Switched off, the screen is blank.
        if (!state.PoweredOn)
        {
            _ecg.Message = string.Empty;
            _pleth.Message = string.Empty;
            _co2.Message = string.Empty;
        }
    }

    private void UpdateNumerics(CMUPatientMonitorBuiState state, bool connected)
    {
        _hr.Value = connected ? state.HeartRate.ToString() : "---";
        _hr.Detail = connected ? Loc.GetString(CMUMonitorRhythms.LocId(state.Rhythm)) : string.Empty;
        _hr.Alarm = connected && (state.HeartRate < 50 || state.HeartRate > 130);

        UpdateHeart(state, connected);

        _spo2.Value = connected && state.SpO2 is { } spo2 ? spo2.ToString() : "---";
        _spo2.Detail = connected && state.SpO2 == null ? Loc.GetString("cmu-monitor-no-pulse") : string.Empty;
        _spo2.Alarm = connected && (state.SpO2 == null || state.SpO2 < 90);

        if (connected && state.NibpSystolic is { } sys && state.NibpDiastolic is { } dia)
        {
            var map = (sys + 2 * dia) / 3;
            _nibp.Value = $"{sys}/{dia}";
            _nibp.Detail = Loc.GetString("cmu-monitor-nibp-detail", ("map", map), ("time", state.NibpTime ?? string.Empty));
            _nibp.Alarm = sys < 90;
        }
        else
        {
            _nibp.Value = "---/---";
            _nibp.Detail = connected && state.NibpTime != null
                ? Loc.GetString("cmu-monitor-nibp-failed", ("time", state.NibpTime))
                : string.Empty;
            _nibp.Alarm = connected && state.NibpTime != null;
        }

        _etco2.Value = connected ? state.EtCO2.ToString() : "---";
        _etco2.Detail = connected ? Loc.GetString("cmu-monitor-rr", ("rate", state.RespiratoryRate)) : string.Empty;
        _etco2.Alarm = connected && (state.RespiratoryRate == 0 || state.EtCO2 < 25);

        _energy.Value = state.PoweredOn ? state.Joules.ToString() : "---";
        _energy.Detail = Loc.GetString(state.Armed ? "cmu-monitor-energy-armed" : "cmu-monitor-energy-safe");

        // Follows the Fahrenheit option in the CMU settings.
        _temp.Unit = "°" + TemperatureDisplay.Unit;
        _temp.Value = connected && state.TemperatureC is { } temp
            ? TemperatureDisplay.FromKelvin(temp + 273.15f).ToString("0.0")
            : "---";
        _temp.Alarm = connected && state.TemperatureC is < 35f or > 39f;
    }

    private void UpdateHeart(CMUPatientMonitorBuiState state, bool connected)
    {
        if (!connected)
        {
            _heart.Value = "---";
            _heart.Detail = string.Empty;
            _heart.Color = EcgColor;
            _heart.Alarm = false;
            return;
        }

        if (state.HeartStage is not { } stage)
        {
            _heart.Value = Loc.GetString("cmu-monitor-heart-none");
            _heart.Detail = string.Empty;
            _heart.Color = AlarmRed;
            _heart.Alarm = true;
            return;
        }

        var percent = state.HeartMax > 0 ? state.HeartCurrent * 100 / state.HeartMax : 0;
        _heart.Value = Loc.GetString(HeartStageLoc(stage));
        _heart.Detail = Loc.GetString("cmu-monitor-heart-detail",
            ("current", state.HeartCurrent), ("max", state.HeartMax), ("percent", percent));
        _heart.Color = stage switch
        {
            OrganDamageStage.Healthy => EcgColor,
            OrganDamageStage.Bruised => AlarmYellow,
            OrganDamageStage.Damaged => ShockOrange,
            _ => AlarmRed,
        };
        _heart.Alarm = stage >= OrganDamageStage.Failing;
    }

    private static string HeartStageLoc(OrganDamageStage stage)
    {
        return stage switch
        {
            OrganDamageStage.Healthy => "cmu-monitor-heart-healthy",
            OrganDamageStage.Bruised => "cmu-monitor-heart-bruised",
            OrganDamageStage.Damaged => "cmu-monitor-heart-damaged",
            OrganDamageStage.Failing => "cmu-monitor-heart-failing",
            _ => "cmu-monitor-heart-dead",
        };
    }

    private void UpdateLog(CMUPatientMonitorBuiState state)
    {
        // Only rebuild (and jump to the newest entry) when something was logged.
        CMUMonitorLogEntry? last = state.Log.Count > 0 ? state.Log[^1] : null;
        if (state.Log.Count == _logCount && last == _lastEntry && _log.ChildCount > 0)
            return;

        _logCount = state.Log.Count;
        _lastEntry = last;
        _log.RemoveAllChildren();
        if (state.Log.Count == 0)
        {
            _log.AddChild(new Label { Text = Loc.GetString("cmu-monitor-log-empty"), FontColorOverride = Dim });
            return;
        }

        foreach (var entry in state.Log)
        {
            _log.AddChild(new Label
            {
                Text = $"{entry.Time}  {entry.Text}",
                FontColorOverride = entry.Critical ? AlarmRed : NibpColor,
                ClipText = true,
            });
        }

        _logScroll.SetScrollValue(new Vector2(0, float.MaxValue));
    }

    private static string StatusLoc(CMUMonitorPatientStatus status)
    {
        return status switch
        {
            CMUMonitorPatientStatus.Conscious => "cmu-monitor-status-conscious",
            CMUMonitorPatientStatus.Unresponsive => "cmu-monitor-status-unresponsive",
            CMUMonitorPatientStatus.Pulseless => "cmu-monitor-status-pulseless",
            CMUMonitorPatientStatus.Dead => "cmu-monitor-status-dead",
            _ => "cmu-monitor-status-none",
        };
    }
}
