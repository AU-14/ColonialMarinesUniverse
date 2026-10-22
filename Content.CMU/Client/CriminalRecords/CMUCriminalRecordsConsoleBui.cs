using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Client.Lobby.UI;
using Content.Client.Resources;
using Content.Client.UserInterface.Controls;
using Content.Shared.Access.Systems;
using Content.Shared.Administration;
using Content.Shared.CriminalRecords;
using Content.Shared.CriminalRecords.Components;
using Content.Shared.Dataset;
using Content.Shared.Random.Helpers;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Content.Shared.StatusIcon;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.CriminalRecords;

/// <summary>
///     The criminal records computer, drawn in the CRT theme. Sends the same messages as the upstream console, so the
///     server side is unchanged; the crime history sits in the main window instead of a separate one.
/// </summary>
public sealed class CMUCriminalRecordsConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUCriminalRecordsWindow? _window;

    protected override void Open()
    {
        base.Open();

        var maxLength = EntMan.TryGetComponent(Owner, out CriminalRecordsConsoleComponent? console)
            ? console.MaxStringLength
            : 256u;

        _window = this.CreateWindow<CMUCriminalRecordsWindow>();
        _window.Setup(Owner, maxLength);
        _window.OnKeySelected += key => SendMessage(new SelectStationRecord(key));
        _window.OnFiltersChanged += (type, value) => SendMessage(new SetStationRecordFilter(type, value));
        _window.OnStatusFilter += status => SendMessage(new CriminalRecordSetStatusFilter(status));
        _window.OnStatusChanged += (status, reason) => SendMessage(new CriminalRecordChangeStatus(status, reason));
        _window.OnAddHistory += line => SendMessage(new CriminalRecordAddHistory(line));
        _window.OnDeleteHistory += index => SendMessage(new CriminalRecordDeleteHistory(index));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is CriminalRecordsConsoleState records)
            _window?.UpdateState(records);
    }
}

public sealed class CMUCriminalRecordsWindow : DefaultWindow
{
    private const string MonoRegular = "/Fonts/RobotoMono/RobotoMono-Regular.ttf";
    private const string MonoBold = "/Fonts/RobotoMono/RobotoMono-Bold.ttf";

    private static readonly ProtoId<LocalizedDatasetPrototype> ReasonPlaceholders = "CriminalRecordsWantedReasonPlaceholders";

    /// <summary>Filters that make sense here; job and species aren't on these records.</summary>
    private static readonly StationRecordFilterType[] FilterTypes =
        [StationRecordFilterType.Name, StationRecordFilterType.Prints, StationRecordFilterType.DNA];

    public event Action<uint?>? OnKeySelected;
    public event Action<StationRecordFilterType, string>? OnFiltersChanged;
    public event Action<SecurityStatus>? OnStatusFilter;
    public event Action<SecurityStatus, string?>? OnStatusChanged;
    public event Action<string>? OnAddHistory;
    public event Action<uint>? OnDeleteHistory;

    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly AccessReaderSystem _accessReader;
    private readonly SpriteSystem _sprites;

    private readonly Font _small;
    private readonly Font _body;
    private readonly Font _bold;
    private readonly Font _title;

    private readonly Label _subheading;
    private readonly Label _accessTag;
    private readonly PanelContainer _accessPanel;
    private readonly OptionButton _filterType;
    private readonly LineEdit _filterText;
    private readonly OptionButton _statusFilter;
    private readonly BoxContainer _list;
    private readonly Control _detailHolder;
    private readonly BoxContainer _detail;
    private readonly Label _placeholder;
    private readonly Label _footer;

    private EntityUid _console;
    private uint _maxLength = 256;
    private bool _access;
    private uint? _selected;
    private StationRecordFilterType _filter = StationRecordFilterType.Name;
    private bool _syncing;
    private DialogWindow? _dialog;

    public CMUCriminalRecordsWindow()
    {
        IoCManager.InjectDependencies(this);
        _accessReader = _entities.System<AccessReaderSystem>();
        _sprites = _entities.System<SpriteSystem>();

        var cache = IoCManager.Resolve<IResourceCache>();
        _small = cache.GetFont(MonoRegular, 10);
        _body = cache.GetFont(MonoRegular, 12);
        _bold = cache.GetFont(MonoBold, 12);
        _title = cache.GetFont(MonoBold, 15);

        Title = Loc.GetString("criminal-records-console-window-title");
        MinSize = new Vector2(780, 540);
        SetSize = new Vector2(860, 640);

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

        // Header strip.
        var header = Strip(CrtTerminalPalette.Surface3);
        var headerRow = Row();
        var titles = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        titles.AddChild(Text(Loc.GetString("cmu-criminal-records-heading"), _title, CrtTerminalPalette.TextBright));
        _subheading = Text(string.Empty, _small, CrtTerminalPalette.TextDim);
        _subheading.ClipText = true;
        titles.AddChild(_subheading);
        headerRow.AddChild(titles);
        _accessPanel = new PanelContainer { VerticalAlignment = VAlignment.Center };
        _accessTag = Text(string.Empty, _bold, CrtTerminalPalette.Void);
        _accessTag.Margin = new Thickness(8, 3);
        _accessPanel.AddChild(_accessTag);
        headerRow.AddChild(_accessPanel);
        header.AddChild(headerRow);
        root.AddChild(header);

        var body = Row();
        body.VerticalExpand = true;
        root.AddChild(body);

        // Left: search, status filter, and the records.
        var left = Panel(CrtTerminalPalette.Surface1);
        left.MinWidth = 270;
        left.MaxWidth = 270;
        var leftColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        leftColumn.AddChild(Text(Loc.GetString("cmu-criminal-records-search"), _small, CrtTerminalPalette.TextDim));
        var searchRow = Row();
        searchRow.SeparationOverride = 4;
        _filterType = new OptionButton { MinWidth = 96 };
        foreach (var type in FilterTypes)
            _filterType.AddItem(Loc.GetString($"criminal-records-{type.ToString().ToLowerInvariant()}-filter"), (int) type);
        _filterType.OnItemSelected += args =>
        {
            _filterType.SelectId(args.Id);
            _filter = (StationRecordFilterType) args.Id;
            if (!_syncing)
                OnFiltersChanged?.Invoke(_filter, _filterText?.Text ?? string.Empty);
        };
        CrtLobbyTheme.ApplyToOptionButton(_filterType);
        searchRow.AddChild(_filterType);
        _filterText = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("cmu-criminal-records-search-placeholder") };
        _filterText.OnTextChanged += args =>
        {
            if (!_syncing)
                OnFiltersChanged?.Invoke(_filter, args.Text);
        };
        searchRow.AddChild(_filterText);
        leftColumn.AddChild(searchRow);

        var statusRow = Row();
        statusRow.SeparationOverride = 4;
        var statusLabel = Text(Loc.GetString("cmu-criminal-records-show"), _small, CrtTerminalPalette.TextDim);
        statusLabel.VerticalAlignment = VAlignment.Center;
        statusRow.AddChild(statusLabel);
        _statusFilter = new OptionButton { HorizontalExpand = true };
        foreach (var status in Enum.GetValues<SecurityStatus>())
        {
            _statusFilter.AddItem(status == SecurityStatus.None
                ? Loc.GetString("cmu-criminal-records-show-all")
                : StatusName(status), (int) status);
        }

        _statusFilter.OnItemSelected += args =>
        {
            _statusFilter.SelectId(args.Id);
            if (!_syncing)
                OnStatusFilter?.Invoke((SecurityStatus) args.Id);
        };
        CrtLobbyTheme.ApplyToOptionButton(_statusFilter);
        statusRow.AddChild(_statusFilter);
        leftColumn.AddChild(statusRow);

        var listScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 3, HorizontalExpand = true };
        listScroll.AddChild(_list);
        leftColumn.AddChild(listScroll);
        left.AddChild(leftColumn);
        body.AddChild(left);

        // Right: the selected record.
        var right = Panel(CrtTerminalPalette.Surface1);
        right.HorizontalExpand = true;
        _detailHolder = new Control { HorizontalExpand = true, VerticalExpand = true };
        var detailScroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true, HScrollEnabled = false };
        _detail = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 6, 0),
        };
        detailScroll.AddChild(_detail);
        _detailHolder.AddChild(detailScroll);
        _placeholder = Text(Loc.GetString("cmu-criminal-records-select"), _body, CrtTerminalPalette.TextDim);
        _placeholder.HorizontalAlignment = HAlignment.Center;
        _placeholder.VerticalAlignment = VAlignment.Center;
        _detailHolder.AddChild(_placeholder);
        right.AddChild(_detailHolder);
        body.AddChild(right);

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

        OnClose += () => _dialog?.Close();
    }

    public void Setup(EntityUid console, uint maxLength)
    {
        _console = console;
        _maxLength = maxLength;
    }

    public void UpdateState(CriminalRecordsConsoleState state)
    {
        _access = _player.LocalSession?.AttachedEntity is { } player && _accessReader.IsAllowed(player, _console);
        _accessTag.Text = Loc.GetString(_access ? "cmu-criminal-records-access-granted" : "cmu-criminal-records-access-read-only");
        _accessPanel.PanelOverride = new StyleBoxFlat { BackgroundColor = _access ? CrtTerminalPalette.Accent : CrtTerminalPalette.Caution };
        _footer.Text = Loc.GetString(_access ? "cmu-criminal-records-footer-edit" : "cmu-criminal-records-footer-read-only");

        // Echo the server's filters back without sending them again.
        _syncing = true;
        if (state.Filter is { } filter)
        {
            _filter = filter.Type;
            // Not while someone's typing, or a late echo would eat their latest keystrokes.
            if (filter.Value != _filterText.Text && !_filterText.HasKeyboardFocus())
                _filterText.Text = filter.Value;
        }

        _filterType.SelectId((int) _filter);
        _statusFilter.SelectId((int) state.FilterStatus);
        _syncing = false;

        _selected = state.SelectedKey;
        var listing = state.RecordListing ?? new Dictionary<uint, string>();
        _subheading.Text = Loc.GetString("cmu-criminal-records-subheading", ("count", listing.Count));
        BuildList(listing);

        _detail.RemoveAllChildren();
        var shown = state is { CriminalRecord: not null, StationRecord: not null, SelectedKey: not null };
        _detailHolder.Children.First().Visible = shown;
        _placeholder.Visible = !shown;
        if (shown)
            BuildRecord(state.StationRecord!, state.CriminalRecord!);
    }

    private void BuildList(Dictionary<uint, string> listing)
    {
        _list.RemoveAllChildren();
        if (listing.Count == 0)
        {
            var none = Text(Loc.GetString("cmu-criminal-records-none"), _small, CrtTerminalPalette.TextDim);
            none.HorizontalAlignment = HAlignment.Center;
            none.Margin = new Thickness(0, 12, 0, 0);
            _list.AddChild(none);
            return;
        }

        foreach (var (key, name) in listing.OrderBy(p => p.Value, StringComparer.OrdinalIgnoreCase))
        {
            var selected = key == _selected;
            var button = new ContainerButton { HorizontalExpand = true, MinHeight = 26 };
            var panel = new PanelContainer
            {
                PanelOverride = new StyleBoxFlat { BackgroundColor = selected ? CrtTerminalPalette.Surface4 : CrtTerminalPalette.Surface2 },
            };
            var row = Row();
            row.AddChild(new PanelContainer
            {
                MinWidth = 4,
                PanelOverride = new StyleBoxFlat { BackgroundColor = selected ? CrtTerminalPalette.Accent : CrtTerminalPalette.Line },
            });
            var label = Text(name.ToUpperInvariant(), _bold, selected ? CrtTerminalPalette.TextBright : CrtTerminalPalette.Text);
            label.ClipText = true;
            label.HorizontalExpand = true;
            label.VerticalAlignment = VAlignment.Center;
            row.AddChild(label);
            panel.AddChild(row);
            button.AddChild(panel);
            button.ToolTip = name;

            if (!selected)
            {
                button.OnMouseEntered += _ => panel.PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface3 };
                button.OnMouseExited += _ => panel.PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 };
            }

            var captured = key;
            button.OnPressed += _ => OnKeySelected?.Invoke(selected ? null : captured);
            _list.AddChild(button);
        }
    }

    private void BuildRecord(GeneralStationRecord person, CriminalRecord record)
    {
        var na = Loc.GetString("generic-not-available-shorthand");
        var statusColor = StatusColor(record.Status);

        // Identity: job icon, name and job, and the current status.
        var identity = Strip(CrtTerminalPalette.Surface2);
        var identityRow = Row();
        if (_prototypes.TryIndex<JobIconPrototype>(person.JobIcon, out var jobIcon))
        {
            identityRow.AddChild(new TextureRect
            {
                Texture = _sprites.Frame0(jobIcon.Icon),
                TextureScale = new Vector2(2, 2),
                Stretch = TextureRect.StretchMode.KeepCentered,
                VerticalAlignment = VAlignment.Center,
                MinSize = new Vector2(32, 32),
            });
        }

        var names = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var name = Text(person.Name.ToUpperInvariant(), _title, CrtTerminalPalette.TextBright);
        name.ClipText = true;
        names.AddChild(name);
        var job = Text(string.IsNullOrEmpty(person.JobTitle) ? na : person.JobTitle.ToUpperInvariant(), _small, CrtTerminalPalette.Accent);
        job.ClipText = true;
        names.AddChild(job);
        identityRow.AddChild(names);

        if (record.Status != SecurityStatus.None)
        {
            identityRow.AddChild(new TextureRect
            {
                Texture = _sprites.Frame0(new SpriteSpecifier.Rsi(new ResPath("Interface/Misc/security_icons.rsi"), StatusIcon(record.Status))),
                TextureScale = new Vector2(2, 2),
                Stretch = TextureRect.StretchMode.KeepCentered,
                VerticalAlignment = VAlignment.Center,
            });
        }

        var tag = new PanelContainer
        {
            VerticalAlignment = VAlignment.Center,
            PanelOverride = new StyleBoxFlat { BackgroundColor = statusColor },
        };
        var tagText = Text(StatusName(record.Status).ToUpperInvariant(), _bold, CrtTerminalPalette.Void);
        tagText.Margin = new Thickness(8, 3);
        tag.AddChild(tagText);
        identityRow.AddChild(tag);
        identity.AddChild(identityRow);
        _detail.AddChild(identity);

        // Particulars.
        var info = Section("cmu-criminal-records-particulars");
        Field(info, "cmu-criminal-records-fingerprints", person.Fingerprint ?? na);
        Field(info, "cmu-criminal-records-dna", person.DNA ?? na);
        Field(info, "cmu-criminal-records-religion", Content.Client.CMU14.Religion.CMUReligionNames.Describe(_prototypes, person.Religion));
        if (!string.IsNullOrEmpty(person.Squad))
            Field(info, "cmu-criminal-records-squad", person.Squad);
        Field(info, "cmu-criminal-records-bounty", record.Bounty > 0 ? record.Bounty.ToString() : na);

        // Why they're flagged, and who flagged them.
        if (record.Reason is { } reason)
        {
            var title = record.Status == SecurityStatus.Suspected
                ? "criminal-records-console-suspected-reason"
                : "criminal-records-console-wanted-reason";
            var why = Section(title, statusColor);
            why.AddChild(Wrapping(reason, CrtTerminalPalette.TextBright));
            if (!string.IsNullOrEmpty(record.InitiatorName))
            {
                var by = Text(Loc.GetString("cmu-criminal-records-set-by", ("name", record.InitiatorName.ToUpperInvariant())),
                    _small, CrtTerminalPalette.TextDim);
                by.ClipText = true;
                why.AddChild(by);
            }
        }

        // Status: one button per status, four to a row.
        var statusSection = Section("cmu-criminal-records-set-status");
        var statuses = new GridContainer { Columns = 4, HSeparationOverride = 4, VSeparationOverride = 4, HorizontalExpand = true };
        foreach (var status in Enum.GetValues<SecurityStatus>())
        {
            var button = new Button
            {
                Text = StatusName(status).ToUpperInvariant(),
                ToggleMode = true,
                Pressed = status == record.Status,
                Disabled = !_access,
                HorizontalExpand = true,
            };
            // The button style replaces the pressed look, so only the current status is lit in its colour.
            CmuButtonStyles.Apply(button, status != record.Status
                ? CmuButtonStyles.Variant.Neutral
                : status switch
                {
                    SecurityStatus.Wanted or SecurityStatus.Hostile => CmuButtonStyles.Variant.Danger,
                    SecurityStatus.Suspected or SecurityStatus.Paroled => CmuButtonStyles.Variant.Caution,
                    _ => CmuButtonStyles.Variant.Affirm,
                });
            button.Label.FontOverride = _small;
            button.Label.ClipText = true;
            var captured = status;
            button.OnPressed += _ =>
            {
                button.Pressed = captured == record.Status;
                if (captured != record.Status)
                    SetStatus(captured);
            };
            statuses.AddChild(button);
        }

        statusSection.AddChild(statuses);
        if (!_access)
            statusSection.AddChild(Text(Loc.GetString("cmu-criminal-records-no-access"), _small, CrtTerminalPalette.Caution));

        // Crime history, newest last, the way it's written.
        var history = Section("criminal-records-console-crime-history");
        if (record.History.Count == 0)
            history.AddChild(Text(Loc.GetString("criminal-records-no-history"), _small, CrtTerminalPalette.TextDim));

        for (var i = 0; i < record.History.Count; i++)
            history.AddChild(HistoryEntry(record.History[i], (uint) i));

        if (_access)
        {
            var add = new Button { Text = Loc.GetString("cmu-criminal-records-add-entry"), HorizontalAlignment = HAlignment.Left };
            CmuButtonStyles.Apply(add, CmuButtonStyles.Variant.Neutral);
            add.Label.FontOverride = _small;
            add.OnPressed += _ => AddHistory();
            history.AddChild(add);
        }
    }

    private Control HistoryEntry(CrimeHistory entry, uint index)
    {
        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = CrtTerminalPalette.Surface2,
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 4,
            },
        };
        var row = Row();
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var time = entry.AddTime;
        var stamp = $"{(int) time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
        if (!string.IsNullOrEmpty(entry.InitiatorName))
            stamp += " // " + entry.InitiatorName.ToUpperInvariant();
        var stampLabel = Text(stamp, _small, CrtTerminalPalette.Accent);
        stampLabel.ClipText = true;
        column.AddChild(stampLabel);
        column.AddChild(Wrapping(entry.Crime, CrtTerminalPalette.Text));
        row.AddChild(column);

        if (_access)
        {
            var delete = new ConfirmButton
            {
                Text = Loc.GetString("cmu-criminal-records-delete"),
                ConfirmationText = Loc.GetString("cmu-criminal-records-delete-confirm"),
                VerticalAlignment = VAlignment.Top,
                MinWidth = 70,
            };
            CmuButtonStyles.Apply(delete, CmuButtonStyles.Variant.Danger);
            delete.Label.FontOverride = _small;
            delete.OnPressed += _ => OnDeleteHistory?.Invoke(index);
            row.AddChild(delete);
        }

        panel.AddChild(row);
        return panel;
    }

    private void SetStatus(SecurityStatus status)
    {
        if (status is not (SecurityStatus.Wanted or SecurityStatus.Suspected or SecurityStatus.Hostile))
        {
            OnStatusChanged?.Invoke(status, null);
            return;
        }

        var placeholders = _prototypes.Index(ReasonPlaceholders);
        var placeholder = Loc.GetString("criminal-records-console-reason-placeholder", ("placeholder", _random.Pick(placeholders)));
        OpenDialog(StatusName(status), Loc.GetString("criminal-records-console-reason"), placeholder, reason =>
        {
            if (reason.Length > _maxLength)
                return;

            OnStatusChanged?.Invoke(status, string.IsNullOrWhiteSpace(reason) ? null : reason);
        });
    }

    private void AddHistory()
    {
        OpenDialog(Loc.GetString("criminal-records-console-crime-history"), Loc.GetString("criminal-records-console-reason"),
            Loc.GetString("criminal-records-history-placeholder"), line =>
            {
                if (line.Length < 1 || line.Length > _maxLength)
                    return;

                OnAddHistory?.Invoke(line);
            });
    }

    private void OpenDialog(string title, string prompt, string placeholder, Action<string> confirmed)
    {
        _dialog?.Close();

        const string field = "text";
        var entries = new List<QuickDialogEntry> { new(field, QuickDialogEntryType.LongText, prompt, placeholder) };
        _dialog = new DialogWindow(title, entries);
        CrtLobbyTheme.Apply(_dialog, useCrtTypography: true);
        _dialog.OnConfirmed += responses => confirmed(responses[field]);
        _dialog.OnClose += () => _dialog = null;
    }

    /// <summary>A titled block in the record; returns the column its contents go in.</summary>
    private BoxContainer Section(string title, Color? accent = null)
    {
        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = CrtTerminalPalette.Surface0,
                ContentMarginLeftOverride = 10,
                ContentMarginRightOverride = 10,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 8,
            },
        };
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        var heading = Text(Loc.GetString(title).ToUpperInvariant(), _bold, accent ?? CrtTerminalPalette.Accent);
        heading.ClipText = true;
        column.AddChild(heading);
        column.AddChild(new PanelContainer
        {
            MinHeight = 1,
            Margin = new Thickness(0, 0, 0, 2),
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Line },
        });
        panel.AddChild(column);
        _detail.AddChild(panel);
        return column;
    }

    private void Field(BoxContainer section, string label, string value)
    {
        var row = Row();
        var key = Text(Loc.GetString(label).ToUpperInvariant(), _small, CrtTerminalPalette.TextDim);
        key.MinWidth = 110;
        key.VerticalAlignment = VAlignment.Top;
        row.AddChild(key);
        var text = Wrapping(value, CrtTerminalPalette.TextBright);
        row.AddChild(text);
        section.AddChild(row);
    }

    /// <summary>Text that wraps to the panel's width, shown exactly as written.</summary>
    private static RichTextLabel Wrapping(string text, Color color)
    {
        var message = new FormattedMessage();
        message.PushColor(color);
        message.AddText(text);
        message.Pop();
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(message);
        return label;
    }

    private static string StatusName(SecurityStatus status)
    {
        return Loc.GetString($"criminal-records-status-{status.ToString().ToLowerInvariant()}");
    }

    private static Color StatusColor(SecurityStatus status)
    {
        return status switch
        {
            SecurityStatus.Wanted or SecurityStatus.Hostile => CrtTerminalPalette.Alert,
            SecurityStatus.Suspected or SecurityStatus.Paroled => CrtTerminalPalette.Caution,
            SecurityStatus.Detained => CrtTerminalPalette.Accent,
            _ => CrtTerminalPalette.TextDim,
        };
    }

    private static string StatusIcon(SecurityStatus status)
    {
        return status switch
        {
            SecurityStatus.Paroled => "hud_paroled",
            SecurityStatus.Wanted => "hud_wanted",
            SecurityStatus.Detained => "hud_incarcerated",
            SecurityStatus.Discharged => "hud_discharged",
            SecurityStatus.Suspected => "hud_suspected",
            SecurityStatus.Hostile => "hud_hostile",
            SecurityStatus.Eliminated => "hud_eliminated",
            _ => "SecurityIconNone",
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

    private static PanelContainer Panel(Color color)
    {
        return new PanelContainer
        {
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
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
    }

    private static Label Text(string text, Font font, Color color)
    {
        return new Label { Text = text, FontOverride = font, FontColorOverride = color };
    }
}
