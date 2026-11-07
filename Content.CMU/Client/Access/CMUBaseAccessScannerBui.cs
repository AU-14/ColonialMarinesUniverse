using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Client.Resources;
using Content.Client.UserInterface.Controls;
using Content.Shared.CMU14.Access;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Access;

public sealed class CMUBaseAccessScannerBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUBaseAccessScannerWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUBaseAccessScannerWindow>();
        _window.OnOperatorCard += () => SendPredictedMessage(new CMUBaseAccessInsertOperatorCardBuiMsg());
        _window.OnEjectOperatorCard += () => SendPredictedMessage(new CMUBaseAccessEjectOperatorCardBuiMsg());
        _window.OnDirection += direction => SendPredictedMessage(new CMUBaseAccessSetDirectionBuiMsg(direction));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window is { IsOpen: true } && state is CMUBaseAccessScannerBuiState scanner)
            _window.Update(scanner);
    }
}

/// <summary>
/// Joint Base Schaefer access logging presented with the identification computer's CRT layout.
/// </summary>
public sealed class CMUBaseAccessScannerWindow : DefaultWindow
{
    private const string MonoRegular = "/Fonts/RobotoMono/RobotoMono-Regular.ttf";
    private const string MonoBold = "/Fonts/RobotoMono/RobotoMono-Bold.ttf";

    public event Action? OnOperatorCard;
    public event Action? OnEjectOperatorCard;
    public event Action<CMUBaseAccessDirection>? OnDirection;

    private readonly Font _small;
    private readonly Font _body;
    private readonly Font _bold;
    private readonly Font _title;

    private readonly Label _statusTag;
    private readonly PanelContainer _statusPanel;
    private Label _operatorName = default!;
    private Label _operatorRank = default!;
    private Button _operatorButton = default!;
    private Button _enteringButton = default!;
    private Button _leavingButton = default!;
    private readonly Label _entriesCaption;
    private readonly BoxContainer _entries;
    private readonly Label _placeholder;
    private readonly Label _footer;
    private bool _hasOperatorCard;

    public CMUBaseAccessScannerWindow()
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        _small = cache.GetFont(MonoRegular, 10);
        _body = cache.GetFont(MonoRegular, 12);
        _bold = cache.GetFont(MonoBold, 12);
        _title = cache.GetFont(MonoBold, 15);

        Title = Loc.GetString("cmu-base-access-ui-title");
        MinSize = new Vector2(720, 520);
        SetSize = new Vector2(780, 600);

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
        var titles = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        var heading = Text(Loc.GetString("cmu-base-access-ui-heading"), _title, CrtTerminalPalette.TextBright);
        heading.ClipText = true;
        titles.AddChild(heading);
        var subheading = Text(Loc.GetString("cmu-base-access-ui-subheading"), _small, CrtTerminalPalette.TextDim);
        subheading.ClipText = true;
        titles.AddChild(subheading);
        headerRow.AddChild(titles);
        _statusPanel = new PanelContainer { VerticalAlignment = VAlignment.Center };
        _statusTag = Text(string.Empty, _bold, CrtTerminalPalette.Void);
        _statusTag.Margin = new Thickness(8, 3);
        _statusPanel.AddChild(_statusTag);
        headerRow.AddChild(_statusPanel);
        header.AddChild(headerRow);
        root.AddChild(header);

        var controls = new GridContainer
        {
            Columns = 2,
            HSeparationOverride = 8,
            HorizontalExpand = true,
        };
        controls.AddChild(BuildOperatorSlot());
        controls.AddChild(BuildDirectionSlot());
        root.AddChild(controls);

        var logPanel = Panel(CrtTerminalPalette.Surface1);
        var logColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
        };
        _entriesCaption = Text(Loc.GetString("cmu-base-access-ui-recent"), _small, CrtTerminalPalette.TextDim);
        logColumn.AddChild(_entriesCaption);

        var logBody = new Control { HorizontalExpand = true, VerticalExpand = true };
        var scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true, HScrollEnabled = false };
        _entries = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
        };
        scroll.AddChild(_entries);
        logBody.AddChild(scroll);
        _placeholder = Text(Loc.GetString("cmu-base-access-ui-placeholder"), _body, CrtTerminalPalette.TextDim);
        _placeholder.HorizontalAlignment = HAlignment.Center;
        _placeholder.VerticalAlignment = VAlignment.Center;
        logBody.AddChild(_placeholder);
        logColumn.AddChild(logBody);
        logPanel.AddChild(logColumn);
        root.AddChild(logPanel);

        var footer = Strip(CrtTerminalPalette.Surface1);
        _footer = Text(string.Empty, _small, CrtTerminalPalette.TextDim);
        _footer.ClipText = true;
        footer.AddChild(_footer);
        root.AddChild(footer);

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

    public void Update(CMUBaseAccessScannerBuiState state)
    {
        _hasOperatorCard = state.CardInserted;
        var (status, statusColor) = state.Authorized
            ? ("cmu-base-access-ui-status-authorized", CrtTerminalPalette.Accent)
            : state.CardInserted
                ? ("cmu-base-access-ui-status-denied", CrtTerminalPalette.Alert)
                : ("cmu-base-access-ui-status-locked", CrtTerminalPalette.Caution);
        _statusTag.Text = Loc.GetString(status);
        _statusPanel.PanelOverride = new StyleBoxFlat { BackgroundColor = statusColor };

        _operatorName.Text = state.CardInserted
            ? (string.IsNullOrWhiteSpace(state.OperatorName)
                ? Loc.GetString("cmu-base-access-ui-unknown")
                : state.OperatorName.ToUpperInvariant())
            : Loc.GetString("cmu-base-access-ui-no-card");
        _operatorName.FontColorOverride = state.CardInserted ? CrtTerminalPalette.TextBright : CrtTerminalPalette.TextDim;
        _operatorRank.Text = state.CardInserted ? state.OperatorRank.ToUpperInvariant() : string.Empty;
        _operatorButton.Text = Loc.GetString(state.CardInserted
            ? "cmu-base-access-ui-eject"
            : "cmu-base-access-ui-sign-in");

        _enteringButton.Disabled = !state.Authorized;
        _leavingButton.Disabled = !state.Authorized;
        StyleDirection(_enteringButton, state.Authorized && state.Direction == CMUBaseAccessDirection.Entering);
        StyleDirection(_leavingButton, state.Authorized && state.Direction == CMUBaseAccessDirection.Leaving);

        _entries.RemoveAllChildren();
        _entriesCaption.Text = Loc.GetString("cmu-base-access-ui-recent-count", ("count", state.Entries.Length));
        _placeholder.Visible = state.Entries.Length == 0;
        foreach (var entry in state.Entries)
            _entries.AddChild(BuildEntry(entry));

        _footer.Text = string.IsNullOrWhiteSpace(state.StatusMessage)
            ? Loc.GetString(state.Authorized
                ? "cmu-base-access-ui-footer-ready"
                : "cmu-base-access-ui-footer-locked")
            : state.StatusMessage;
    }

    private Control BuildOperatorSlot()
    {
        var strip = Strip(CrtTerminalPalette.Surface2);
        var row = Row();
        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        text.AddChild(Text(Loc.GetString("cmu-base-access-ui-operator-card"), _small, CrtTerminalPalette.TextDim));
        _operatorName = Text(string.Empty, _bold, CrtTerminalPalette.TextBright);
        _operatorName.ClipText = true;
        text.AddChild(_operatorName);
        _operatorRank = Text(string.Empty, _small, CrtTerminalPalette.Text);
        _operatorRank.ClipText = true;
        text.AddChild(_operatorRank);
        row.AddChild(text);
        _operatorButton = new Button { MinWidth = 100, VerticalAlignment = VAlignment.Center };
        CmuButtonStyles.Apply(_operatorButton, CmuButtonStyles.Variant.Neutral);
        _operatorButton.Label.FontOverride = _small;
        _operatorButton.OnPressed += _ =>
        {
            if (_hasOperatorCard)
                OnEjectOperatorCard?.Invoke();
            else
                OnOperatorCard?.Invoke();
        };
        row.AddChild(_operatorButton);
        strip.AddChild(row);
        return strip;
    }

    private Control BuildDirectionSlot()
    {
        var strip = Strip(CrtTerminalPalette.Surface2);
        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5,
        };
        column.AddChild(Text(Loc.GetString("cmu-base-access-ui-direction"), _small, CrtTerminalPalette.TextDim));
        var buttons = Row();
        buttons.HorizontalExpand = true;
        _enteringButton = new Button
        {
            Text = Loc.GetString("cmu-base-access-ui-entering"),
            HorizontalExpand = true,
        };
        _leavingButton = new Button
        {
            Text = Loc.GetString("cmu-base-access-ui-leaving"),
            HorizontalExpand = true,
        };
        _enteringButton.OnPressed += _ => OnDirection?.Invoke(CMUBaseAccessDirection.Entering);
        _leavingButton.OnPressed += _ => OnDirection?.Invoke(CMUBaseAccessDirection.Leaving);
        buttons.AddChild(_enteringButton);
        buttons.AddChild(_leavingButton);
        column.AddChild(buttons);
        strip.AddChild(column);
        return strip;
    }

    private Control BuildEntry(CMUBaseAccessLogEntry entry)
    {
        var panel = Strip(CrtTerminalPalette.Surface2);
        var row = Row();
        var entering = entry.Direction == CMUBaseAccessDirection.Entering;
        row.AddChild(new PanelContainer
        {
            MinWidth = 5,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = entering ? CrtTerminalPalette.Accent : CrtTerminalPalette.Caution,
            },
        });

        var identity = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        var name = Text(entry.Name.ToUpperInvariant(), _bold, CrtTerminalPalette.TextBright);
        name.ClipText = true;
        identity.AddChild(name);
        var detail = Text($"{entry.Rank.ToUpperInvariant()} // {Loc.GetString("cmu-base-access-ui-approved-by", ("name", entry.Approver.ToUpperInvariant()))}",
            _small,
            CrtTerminalPalette.TextDim);
        detail.ClipText = true;
        identity.AddChild(detail);
        row.AddChild(identity);

        var time = $"{(int) entry.Time.TotalHours:00}:{entry.Time.Minutes:00}";
        var stamp = Text($"{time}  {Loc.GetString(entering ? "cmu-base-access-ui-in" : "cmu-base-access-ui-out")}",
            _bold,
            entering ? CrtTerminalPalette.Accent : CrtTerminalPalette.Caution);
        stamp.VerticalAlignment = VAlignment.Center;
        row.AddChild(stamp);
        panel.AddChild(row);
        return panel;
    }

    private static void StyleDirection(Button button, bool selected)
    {
        CmuButtonStyles.Apply(button, selected ? CmuButtonStyles.Variant.Affirm : CmuButtonStyles.Variant.Neutral);
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

    private static PanelContainer Panel(Color color)
    {
        return new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = color,
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 8,
            },
        };
    }

    private static BoxContainer Row()
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
        };
    }

    private static Label Text(string text, Font font, Color color)
    {
        return new Label
        {
            Text = text,
            FontOverride = font,
            FontColorOverride = color,
        };
    }
}
