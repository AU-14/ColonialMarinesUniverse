using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Client.Resources;
using Content.Shared.CMU14.Doors;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Doors;

public sealed class CMUStationAlertBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUStationAlertWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUStationAlertWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is CMUStationAlertBuiState alerts)
            _window?.UpdateState(alerts);
    }
}

/// <summary>
///     The atmospherics alert computer's screen, in the CRT theme: one card per area with something wrong, worst first.
/// </summary>
public sealed class CMUStationAlertWindow : DefaultWindow
{
    private const string MonoRegular = "/Fonts/RobotoMono/RobotoMono-Regular.ttf";
    private const string MonoBold = "/Fonts/RobotoMono/RobotoMono-Bold.ttf";

    private static readonly CMUStationAlertHazard[] Order =
    [
        CMUStationAlertHazard.Breach,
        CMUStationAlertHazard.Fire,
        CMUStationAlertHazard.Toxic,
        CMUStationAlertHazard.LowOxygen,
        CMUStationAlertHazard.LowPressure,
        CMUStationAlertHazard.HighPressure,
        CMUStationAlertHazard.Freezing,
        CMUStationAlertHazard.FirelockSealed,
    ];

    private readonly Font _small;
    private readonly Font _bold;
    private readonly Font _title;

    private readonly Label _statusTag;
    private readonly PanelContainer _statusPanel;
    private readonly BoxContainer _list;
    private readonly Label _footer;

    public CMUStationAlertWindow()
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        _small = cache.GetFont(MonoRegular, 10);
        _bold = cache.GetFont(MonoBold, 12);
        _title = cache.GetFont(MonoBold, 15);

        Title = Loc.GetString("cmu-station-alert-title");
        MinSize = new Vector2(520, 420);
        SetSize = new Vector2(560, 520);

        var screen = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface0 },
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(10),
        };
        screen.AddChild(root);

        var header = Strip(CrtTerminalPalette.Surface3);
        var headerRow = Row();
        var titles = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        titles.AddChild(Text(Loc.GetString("cmu-station-alert-heading"), _title, CrtTerminalPalette.TextBright));
        var subheading = Text(Loc.GetString("cmu-station-alert-subheading"), _small, CrtTerminalPalette.TextDim);
        subheading.ClipText = true;
        titles.AddChild(subheading);
        headerRow.AddChild(titles);
        _statusPanel = new PanelContainer { VerticalAlignment = VAlignment.Center };
        _statusTag = Text(Loc.GetString("cmu-station-alert-status-scanning"), _bold, CrtTerminalPalette.Void);
        _statusTag.Margin = new Thickness(8, 3);
        _statusPanel.AddChild(_statusTag);
        headerRow.AddChild(_statusPanel);
        header.AddChild(headerRow);
        root.AddChild(header);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        scroll.AddChild(_list);
        root.AddChild(scroll);

        var footer = Strip(CrtTerminalPalette.Surface1);
        _footer = Text(Loc.GetString("cmu-station-alert-footer"), _small, CrtTerminalPalette.TextDim);
        _footer.ClipText = true;
        footer.AddChild(_footer);
        root.AddChild(footer);

        // Reading surface, so no roll bar or grain.
        var tube = new Control { HorizontalExpand = true, VerticalExpand = true };
        tube.AddChild(screen);
        tube.AddChild(new CrtScreenControl
        {
            Source = screen,
            Phosphor = CrtTerminalPalette.Accent,
            Roll = false,
            Grain = false,
        });
        Contents.AddChild(tube);
    }

    public void UpdateState(CMUStationAlertBuiState state)
    {
        _list.RemoveAllChildren();

        var clear = state.Alerts.Count == 0;
        _statusTag.Text = clear
            ? Loc.GetString("cmu-station-alert-status-clear")
            : Loc.GetString("cmu-station-alert-status-alerts", ("count", state.Alerts.Count));
        _statusPanel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = clear ? CrtTerminalPalette.Accent : CrtTerminalPalette.Alert,
        };

        if (clear)
        {
            var none = Text(Loc.GetString("cmu-station-alert-none"), _bold, CrtTerminalPalette.Accent);
            none.HorizontalAlignment = HAlignment.Center;
            none.Margin = new Thickness(0, 24, 0, 0);
            _list.AddChild(none);
        }

        foreach (var alert in state.Alerts)
            _list.AddChild(Card(alert));

        var scanned = TimeSpan.FromSeconds(Math.Floor(state.ScannedAt.TotalSeconds));
        _footer.Text = Loc.GetString("cmu-station-alert-footer-scanned", ("time", scanned.ToString(@"hh\:mm\:ss")));
    }

    private Control Card(CMUStationAlertEntry alert)
    {
        var worst = CMUStationAlertHazard.None;
        foreach (var hazard in Order)
        {
            if ((alert.Hazards & hazard) != 0)
            {
                worst = hazard;
                break;
            }
        }

        var color = HazardColor(worst);
        var card = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 },
        };

        var row = Row();
        row.AddChild(new PanelContainer { MinWidth = 4, PanelOverride = new StyleBoxFlat { BackgroundColor = color } });

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
            Margin = new Thickness(0, 6, 8, 6),
        };

        // Area names can be long, so the name gets the whole line and the deck sits underneath.
        var name = Text(alert.Area.ToUpperInvariant(), _bold, CrtTerminalPalette.TextBright);
        name.ClipText = true;
        column.AddChild(name);

        // Two rows of hazard tags at most: four fit across at the minimum width.
        var tags = new GridContainer { Columns = 4, HSeparationOverride = 4, VSeparationOverride = 4 };
        foreach (var hazard in Order)
        {
            if ((alert.Hazards & hazard) != 0)
                tags.AddChild(Tag(hazard));
        }

        column.AddChild(tags);

        var detail = Text(Detail(alert), _small, CrtTerminalPalette.TextDim);
        detail.ClipText = true;
        column.AddChild(detail);

        row.AddChild(column);
        card.AddChild(row);
        return card;
    }

    private string Detail(CMUStationAlertEntry alert)
    {
        var parts = new List<string> { Loc.GetString("cmu-station-alert-detail-deck", ("deck", alert.Deck)) };
        if (alert.Tiles > 0)
        {
            parts.Add(Loc.GetString("cmu-station-alert-detail-tiles", ("tiles", alert.Tiles)));
            parts.Add(Loc.GetString("cmu-station-alert-detail-pressure", ("pressure", Number(alert.LowestPressure))));
            parts.Add(Loc.GetString("cmu-station-alert-detail-temperature",
                ("temperature", Number(alert.HighestTemperature - 273.15f))));
        }

        if (alert.SealedFirelocks > 0)
            parts.Add(Loc.GetString("cmu-station-alert-detail-firelocks", ("count", alert.SealedFirelocks)));

        return string.Join(" / ", parts);
    }

    /// <summary>
    ///     A readout with at most four decimals. Passed to the locale as text, since a float handed over as a number
    ///     picks up a long tail of digits on the way.
    /// </summary>
    private static string Number(float value)
    {
        return Math.Round((double) value, 4).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
    }

    private Control Tag(CMUStationAlertHazard hazard)
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = HazardColor(hazard) },
        };
        var label = Text(Loc.GetString($"cmu-station-alert-hazard-{hazard.ToString().ToLowerInvariant()}"), _small,
            CrtTerminalPalette.Void);
        label.Margin = new Thickness(6, 1);
        panel.AddChild(label);
        return panel;
    }

    private static Color HazardColor(CMUStationAlertHazard hazard)
    {
        return hazard switch
        {
            CMUStationAlertHazard.Breach or CMUStationAlertHazard.Fire or CMUStationAlertHazard.Toxic
                => CrtTerminalPalette.Alert,
            CMUStationAlertHazard.FirelockSealed => CrtTerminalPalette.TextDim,
            _ => CrtTerminalPalette.Caution,
        };
    }

    private static PanelContainer Strip(Color color)
    {
        return new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = color,
                ContentMarginLeftOverride = 10,
                ContentMarginRightOverride = 10,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 6,
            },
        };
    }

    private static BoxContainer Row()
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
    }

    private static Label Text(string text, Font font, Color color)
    {
        return new Label { Text = text, FontOverride = font, FontColorOverride = color };
    }
}
