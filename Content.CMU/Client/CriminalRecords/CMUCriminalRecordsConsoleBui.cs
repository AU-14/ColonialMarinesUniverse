using System.Linq;
using System.Numerics;
using Content.Client.CMU14.FactionRoster;
using Content.Client.CMU14.Interface;
using Content.Client.Lobby.UI;
using Content.Client.UserInterface.Controls;
using Content.Shared.Access.Systems;
using Content.Shared.Administration;
using Content.Shared.CMU14.CriminalRecords;
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
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using static System.StringComparison;

namespace Content.Client.CMU14.CriminalRecords;

/// <summary>
///     The criminal records computer, drawn in the CRT theme. Sends the same messages as the upstream console, so the
///     server side is unchanged; the crime history sits in the main window instead of a separate one. A second page
///     lists the firearms colony logistics has bought this round.
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
        _window.OnIssueWarrant += charge => SendMessage(new CMURecordIssueWarrantMsg(charge));
        _window.OnRevokeWarrant += index => SendMessage(new CMURecordRevokeWarrantMsg(index));
        _window.OnLogArrest += (charges, minutes) => SendMessage(new CMURecordLogArrestMsg(charges, minutes));
        _window.OnDeleteArrest += index => SendMessage(new CMURecordDeleteArrestMsg(index));
        _window.OnPrintPhoto += () => SendMessage(new CMURecordPrintPhotoMsg());
        _window.OnPrintRecord += () => SendMessage(new CMURecordPrintRecordMsg());
        _window.OnOpenPersonnel += () => SendMessage(new CMURecordOpenPersonnelMsg());

        SendMessage(new CMUFirearmRegistryRequestMsg());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is CriminalRecordsConsoleState records)
            _window?.UpdateState(records);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is CMUFirearmRegistryMsg registry)
            _window?.UpdateFirearms(registry.Entries);
        else if (message is CMUFirearmRegistryCompareMsg compare)
            _window?.ShowFirearm(compare.Signature);
        else if (message is CMURecordPersonnelMsg personnel)
            new FactionRosterEntryWindow(personnel.Entry).OpenCentered();
    }
}

public sealed partial class CMUCriminalRecordsWindow : DefaultWindow
{
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
    public event Action<string>? OnIssueWarrant;
    public event Action<int>? OnRevokeWarrant;
    public event Action<string, int>? OnLogArrest;
    public event Action<int>? OnDeleteArrest;
    public event Action? OnPrintPhoto;
    public event Action? OnPrintRecord;
    public event Action? OnOpenPersonnel;

    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly AccessReaderSystem _accessReader;
    private readonly SpriteSystem _sprites;
    private readonly CrtConsoleKit _kit = new();

    private readonly RichTextLabel _subheading;
    private readonly PanelContainer _accessPanel;
    private readonly Label _accessTag;
    private readonly Button _recordsTab;
    private readonly Button _firearmsTab;
    private readonly Control _recordsPage;
    private readonly Control _firearmsPage;
    private readonly OptionButton _filterType;
    private readonly LineEdit _filterText;
    private readonly OptionButton _statusFilter;
    private readonly BoxContainer _list;
    private readonly Control _detailScroll;
    private readonly BoxContainer _detail;
    private readonly Label _placeholder;
    private readonly LineEdit _firearmSearch;
    private readonly BoxContainer _firearmList;
    private readonly RichTextLabel _footer;

    private EntityUid _console;
    private uint _maxLength = 256;
    private bool _access;
    private uint? _selected;
    private int _recordCount;
    private StationRecordFilterType _filter = StationRecordFilterType.Name;
    private bool _syncing;
    private bool _showFirearms;
    private DialogWindow? _dialog;
    private List<CMUFirearmRegistryEntry> _firearms = new();

    public CMUCriminalRecordsWindow()
    {
        IoCManager.InjectDependencies(this);
        _accessReader = _entities.System<AccessReaderSystem>();
        _sprites = _entities.System<SpriteSystem>();

        Title = Loc.GetString("criminal-records-console-window-title");
        MinSize = new Vector2(780, 540);
        SetSize = new Vector2(860, 640);

        // Header: heading, a subheading that wraps, and the access tag.
        var header = CrtConsoleKit.Strip(CrtTerminalPalette.Surface3);
        var headerRow = CrtConsoleKit.Row();
        var titles = CrtConsoleKit.Column(0);
        titles.HorizontalExpand = true;
        titles.AddChild(CrtConsoleKit.Text(Loc.GetString("cmu-criminal-records-heading"), _kit.Title, CrtTerminalPalette.TextBright));
        _subheading = CrtConsoleKit.Wrapping(string.Empty, CrtTerminalPalette.TextDim);
        titles.AddChild(_subheading);
        headerRow.AddChild(titles);
        _accessPanel = _kit.Tag(out _accessTag);
        _accessPanel.VerticalAlignment = VAlignment.Top;
        headerRow.AddChild(_accessPanel);
        header.AddChild(headerRow);
        _kit.Root.AddChild(header);

        // Page tabs.
        var tabs = CrtConsoleKit.Row(4);
        _recordsTab = _kit.Button(Loc.GetString("cmu-criminal-records-tab-records"));
        _recordsTab.OnPressed += _ => ShowPage(false);
        tabs.AddChild(_recordsTab);
        _firearmsTab = _kit.Button(Loc.GetString("cmu-criminal-records-tab-firearms"));
        _firearmsTab.OnPressed += _ => ShowPage(true);
        tabs.AddChild(_firearmsTab);
        _kit.Root.AddChild(tabs);

        var pages = new Control { VerticalExpand = true, HorizontalExpand = true };
        _kit.Root.AddChild(pages);

        // Records page. Left: search, status filter and the records. Right: the selected record.
        var records = CrtConsoleKit.Row();
        records.VerticalExpand = true;
        _recordsPage = records;
        pages.AddChild(records);

        var left = Panel(CrtTerminalPalette.Surface1);
        left.MinWidth = 270;
        left.MaxWidth = 270;
        var leftColumn = CrtConsoleKit.Column(6);
        leftColumn.AddChild(CrtConsoleKit.Text(Loc.GetString("cmu-criminal-records-search"), _kit.Small, CrtTerminalPalette.TextDim));
        var searchRow = CrtConsoleKit.Row(4);
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

        var statusRow = CrtConsoleKit.Row(4);
        var statusLabel = CrtConsoleKit.Text(Loc.GetString("cmu-criminal-records-show"), _kit.Small, CrtTerminalPalette.TextDim);
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
        _list = CrtConsoleKit.Column(3);
        _list.HorizontalExpand = true;
        listScroll.AddChild(_list);
        leftColumn.AddChild(listScroll);
        left.AddChild(leftColumn);
        records.AddChild(left);

        var right = Panel(CrtTerminalPalette.Surface1);
        right.HorizontalExpand = true;
        var detailHolder = new Control { HorizontalExpand = true, VerticalExpand = true };
        var detailScroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true, HScrollEnabled = false };
        _detailScroll = detailScroll;
        _detail = CrtConsoleKit.Column(8);
        _detail.HorizontalExpand = true;
        _detail.Margin = new Thickness(0, 0, 6, 0);
        detailScroll.AddChild(_detail);
        detailHolder.AddChild(detailScroll);
        _placeholder = CrtConsoleKit.Text(Loc.GetString("cmu-criminal-records-select"), _kit.Body, CrtTerminalPalette.TextDim);
        _placeholder.HorizontalAlignment = HAlignment.Center;
        _placeholder.VerticalAlignment = VAlignment.Center;
        detailHolder.AddChild(_placeholder);
        right.AddChild(detailHolder);
        records.AddChild(right);

        // Firearms page: everything colony logistics has bought this round.
        var firearms = CrtConsoleKit.Column(6);
        firearms.VerticalExpand = true;
        _firearmsPage = firearms;
        pages.AddChild(firearms);
        _firearmSearch = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("cmu-firearm-registry-search-placeholder"),
        };
        _firearmSearch.OnTextChanged += _ => BuildFirearms();
        firearms.AddChild(_firearmSearch);
        var firearmScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _firearmList = CrtConsoleKit.Column(6);
        _firearmList.HorizontalExpand = true;
        _firearmList.Margin = new Thickness(0, 0, 6, 0);
        firearmScroll.AddChild(_firearmList);
        firearms.AddChild(firearmScroll);

        var footer = CrtConsoleKit.Strip(CrtTerminalPalette.Surface1);
        _footer = CrtConsoleKit.Wrapping(string.Empty, CrtTerminalPalette.TextDim);
        footer.AddChild(_footer);
        _kit.Root.AddChild(footer);

        Contents.AddChild(_kit.Tube);
        OnClose += () => _dialog?.Close();

        ShowPage(false);
        BuildFirearms();
    }

    public void Setup(EntityUid console, uint maxLength)
    {
        _console = console;
        _maxLength = maxLength;
    }

    public void UpdateState(CriminalRecordsConsoleState state)
    {
        _access = _player.LocalSession?.AttachedEntity is { } player && _accessReader.IsAllowed(player, _console);
        CrtConsoleKit.SetTag(_accessPanel, _accessTag,
            Loc.GetString(_access ? "cmu-criminal-records-access-granted" : "cmu-criminal-records-access-read-only"),
            _access ? CrtTerminalPalette.Accent : CrtTerminalPalette.Caution);

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
        _recordCount = listing.Count;
        BuildList(listing);

        _detail.RemoveAllChildren();
        var shown = state is { CriminalRecord: not null, StationRecord: not null, SelectedKey: not null };
        _detailScroll.Visible = shown;
        _placeholder.Visible = !shown;
        if (shown)
            BuildRecord(state.StationRecord!, state.CriminalRecord!);

        UpdateHeader();
    }

    public void UpdateFirearms(List<CMUFirearmRegistryEntry> entries)
    {
        _firearms = entries;
        BuildFirearms();
        UpdateHeader();
    }

    /// <summary>Opens the firearms page filtered to one signature, after something was compared at the console.</summary>
    public void ShowFirearm(string signature)
    {
        _firearmSearch.Text = signature;
        ShowPage(true);
        BuildFirearms();
    }

    private void ShowPage(bool firearms)
    {
        _showFirearms = firearms;
        _recordsPage.Visible = !firearms;
        _firearmsPage.Visible = firearms;
        CmuButtonStyles.Apply(_recordsTab, firearms ? CmuButtonStyles.Variant.Neutral : CmuButtonStyles.Variant.Affirm);
        CmuButtonStyles.Apply(_firearmsTab, firearms ? CmuButtonStyles.Variant.Affirm : CmuButtonStyles.Variant.Neutral);
        _recordsTab.Label.FontOverride = _kit.Small;
        _firearmsTab.Label.FontOverride = _kit.Small;
        UpdateHeader();
    }

    private void UpdateHeader()
    {
        SetWrapping(_subheading, _showFirearms
            ? Loc.GetString("cmu-firearm-registry-subheading", ("count", _firearms.Count))
            : Loc.GetString("cmu-criminal-records-subheading", ("count", _recordCount)), CrtTerminalPalette.TextDim);
        SetWrapping(_footer, Loc.GetString(_showFirearms
            ? "cmu-firearm-registry-footer"
            : _access
                ? "cmu-criminal-records-footer-edit"
                : "cmu-criminal-records-footer-read-only"), CrtTerminalPalette.TextDim);
    }

    private void BuildList(Dictionary<uint, string> listing)
    {
        _list.RemoveAllChildren();
        if (listing.Count == 0)
        {
            var none = CrtConsoleKit.Text(Loc.GetString("cmu-criminal-records-none"), _kit.Small, CrtTerminalPalette.TextDim);
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
            var row = CrtConsoleKit.Row();
            row.AddChild(new PanelContainer
            {
                MinWidth = 4,
                PanelOverride = new StyleBoxFlat { BackgroundColor = selected ? CrtTerminalPalette.Accent : CrtTerminalPalette.Line },
            });
            var label = CrtConsoleKit.BoldWrapping(name.ToUpperInvariant(), selected ? CrtTerminalPalette.TextBright : CrtTerminalPalette.Text);
            label.VerticalAlignment = VAlignment.Center;
            label.Margin = new Thickness(0, 3, 4, 3);
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

        // Identity: mugshot, job icon, name and job, and the current status.
        var identity = CrtConsoleKit.Strip(CrtTerminalPalette.Surface2);
        var identityRow = CrtConsoleKit.Row();
        var mugshot = new CMURecordPortraitView(new Vector2(96, 96), _kit.Small) { VerticalAlignment = VAlignment.Top };
        mugshot.SetPortrait(person.CMUPortrait);
        identityRow.AddChild(mugshot);
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

        var names = CrtConsoleKit.Column(0);
        names.HorizontalExpand = true;
        names.AddChild(CrtConsoleKit.BoldWrapping(person.Name.ToUpperInvariant(), CrtTerminalPalette.TextBright));
        names.AddChild(CrtConsoleKit.Wrapping(string.IsNullOrEmpty(person.JobTitle) ? na : person.JobTitle.ToUpperInvariant(),
            CrtTerminalPalette.Accent));
        var actions = CrtConsoleKit.Row(4);
        actions.Margin = new Thickness(0, 4, 0, 0);
        var personnel = _kit.Button(Loc.GetString("cmu-records-personnel"));
        personnel.OnPressed += _ => OnOpenPersonnel?.Invoke();
        actions.AddChild(personnel);
        if (_access)
        {
            var printRecord = _kit.Button(Loc.GetString("cmu-records-print-record"));
            printRecord.OnPressed += _ => OnPrintRecord?.Invoke();
            actions.AddChild(printRecord);
        }

        if (_access && person.CMUPortrait != null)
        {
            var print = _kit.Button(Loc.GetString("cmu-records-print-photo"));
            print.OnPressed += _ => OnPrintPhoto?.Invoke();
            actions.AddChild(print);
        }

        names.AddChild(actions);
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

        var tag = _kit.Tag(out var tagText);
        CrtConsoleKit.SetTag(tag, tagText, StatusName(record.Status).ToUpperInvariant(), statusColor);
        identityRow.AddChild(tag);
        identity.AddChild(identityRow);
        _detail.AddChild(identity);

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
            button.Label.FontOverride = _kit.Small;
            button.Label.ClipText = true;
            button.ToolTip = StatusName(status);
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
            statusSection.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("cmu-criminal-records-no-access"), CrtTerminalPalette.Caution));

        // Particulars.
        var info = Section("cmu-criminal-records-particulars");
        Field(info, "cmu-records-age", person.Age > 0 ? person.Age.ToString() : na);
        Field(info, "cmu-records-gender", Loc.GetString($"cmu-records-gender-{person.Gender.ToString().ToLowerInvariant()}"));
        Field(info, "cmu-records-height", person.CMUHeight ?? na);
        Field(info, "cmu-criminal-records-fingerprints", person.Fingerprint ?? na);
        Field(info, "cmu-criminal-records-dna", person.DNA ?? na);
        Field(info, "cmu-criminal-records-religion", Content.Client.CMU14.Religion.CMUReligionNames.Describe(_prototypes, person.Religion));
        if (!string.IsNullOrEmpty(person.Squad))
            Field(info, "cmu-criminal-records-squad", person.Squad);
        Field(info, "cmu-criminal-records-bounty", record.Bounty > 0 ? record.Bounty.ToString() : na);

        // Warrants.
        var warrants = Section("cmu-records-warrants", record.CMUWarrants.Count > 0 ? CrtTerminalPalette.Alert : null);
        if (record.CMUWarrants.Count == 0)
            warrants.AddChild(CrtConsoleKit.Text(Loc.GetString("cmu-records-warrants-none"), _kit.Small, CrtTerminalPalette.TextDim));

        for (var i = 0; i < record.CMUWarrants.Count; i++)
        {
            var warrant = record.CMUWarrants[i];
            var index = i;
            warrants.AddChild(EntryCard(
                Loc.GetString("cmu-records-warrant-line", ("time", FormatTime(warrant.IssuedAt)), ("officer", warrant.IssuedBy.ToUpperInvariant())),
                warrant.Charge,
                Loc.GetString("cmu-records-warrant-revoke"),
                () => OnRevokeWarrant?.Invoke(index)));
        }

        if (_access)
        {
            var issue = _kit.Button(Loc.GetString("cmu-records-warrant-issue"), CmuButtonStyles.Variant.Danger);
            issue.HorizontalAlignment = HAlignment.Left;
            issue.OnPressed += _ => OpenDialog(Loc.GetString("cmu-records-warrants"), Loc.GetString("cmu-records-warrant-prompt"),
                string.Empty, charge =>
                {
                    if (charge.Length > 0 && charge.Length <= _maxLength)
                        OnIssueWarrant?.Invoke(charge);
                });
            warrants.AddChild(issue);
        }

        // Arrests, newest last.
        var arrests = Section("cmu-records-arrests");
        if (record.CMUArrests.Count == 0)
            arrests.AddChild(CrtConsoleKit.Text(Loc.GetString("cmu-records-arrests-none"), _kit.Small, CrtTerminalPalette.TextDim));

        for (var i = 0; i < record.CMUArrests.Count; i++)
        {
            var arrest = record.CMUArrests[i];
            var index = i;
            arrests.AddChild(EntryCard(
                Loc.GetString("cmu-records-arrest-line", ("time", FormatTime(arrest.ArrestedAt)),
                    ("officer", arrest.Officer.ToUpperInvariant()), ("minutes", arrest.SentenceMinutes)),
                arrest.Charges,
                Loc.GetString("cmu-criminal-records-delete"),
                () => OnDeleteArrest?.Invoke(index)));
        }

        if (_access)
        {
            var log = _kit.Button(Loc.GetString("cmu-records-arrest-log"));
            log.HorizontalAlignment = HAlignment.Left;
            log.OnPressed += _ => OpenArrestDialog();
            arrests.AddChild(log);
        }

        // Firearms this person ordered through colony logistics.
        var owned = _firearms.Where(f => string.Equals(f.Buyer, person.Name, OrdinalIgnoreCase)).ToList();
        if (owned.Count > 0)
        {
            var guns = Section("cmu-firearm-registry-ordered-by-them");
            foreach (var gun in owned)
                guns.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("cmu-firearm-registry-owned-line",
                    ("name", gun.Name), ("serial", gun.Serial)), CrtTerminalPalette.TextBright));
        }

        // Why they're flagged, and who flagged them.
        if (record.Reason is { } reason)
        {
            var title = record.Status == SecurityStatus.Suspected
                ? "criminal-records-console-suspected-reason"
                : "criminal-records-console-wanted-reason";
            var why = Section(title, statusColor);
            why.AddChild(CrtConsoleKit.Wrapping(reason, CrtTerminalPalette.TextBright));
            if (!string.IsNullOrEmpty(record.InitiatorName))
            {
                why.AddChild(CrtConsoleKit.Wrapping(
                    Loc.GetString("cmu-criminal-records-set-by", ("name", record.InitiatorName.ToUpperInvariant())),
                    CrtTerminalPalette.TextDim));
            }
        }

        // Crime history, newest last, the way it's written.
        var history = Section("criminal-records-console-crime-history");
        if (record.History.Count == 0)
            history.AddChild(CrtConsoleKit.Text(Loc.GetString("criminal-records-no-history"), _kit.Small, CrtTerminalPalette.TextDim));

        for (var i = 0; i < record.History.Count; i++)
            history.AddChild(HistoryEntry(record.History[i], (uint) i));

        if (_access)
        {
            var add = _kit.Button(Loc.GetString("cmu-criminal-records-add-entry"));
            add.HorizontalAlignment = HAlignment.Left;
            add.OnPressed += _ => AddHistory();
            history.AddChild(add);
        }
    }

    private void BuildFirearms()
    {
        _firearmList.RemoveAllChildren();
        var search = _firearmSearch.Text.Trim();
        var shown = _firearms
            .Where(f => search.Length == 0 ||
                        f.Serial.Contains(search, OrdinalIgnoreCase) ||
                        f.Signature.Contains(search, OrdinalIgnoreCase) ||
                        f.Name.Contains(search, OrdinalIgnoreCase) ||
                        f.Buyer.Contains(search, OrdinalIgnoreCase))
            .OrderByDescending(f => f.Time)
            .ToList();

        if (shown.Count == 0)
        {
            var none = CrtConsoleKit.Wrapping(Loc.GetString(_firearms.Count == 0
                ? "cmu-firearm-registry-empty"
                : "cmu-firearm-registry-no-match"), CrtTerminalPalette.TextDim);
            none.Margin = new Thickness(0, 12, 0, 0);
            _firearmList.AddChild(none);
            return;
        }

        foreach (var entry in shown)
            _firearmList.AddChild(FirearmCard(entry));
    }

    private Control FirearmCard(CMUFirearmRegistryEntry entry)
    {
        var card = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = CrtTerminalPalette.Surface1,
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 6,
            },
        };
        var row = CrtConsoleKit.Row();
        var frame = new PanelContainer
        {
            MinSize = new Vector2(48, 48),
            VerticalAlignment = VAlignment.Top,
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 },
        };
        if (entry.Prototype is { } proto && _prototypes.HasIndex<EntityPrototype>(proto))
        {
            var view = new EntityPrototypeView
            {
                SetSize = new Vector2(40, 40),
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                Stretch = SpriteView.StretchMode.Fill,
            };
            view.SetPrototype(proto);
            frame.AddChild(view);
        }

        row.AddChild(frame);

        var column = CrtConsoleKit.Column(2);
        column.HorizontalExpand = true;
        column.AddChild(CrtConsoleKit.BoldWrapping(entry.Name.ToUpperInvariant(), CrtTerminalPalette.TextBright));
        column.AddChild(_kit.Field(Loc.GetString("cmu-firearm-registry-serial"), entry.Serial, CrtTerminalPalette.Accent, 110));
        column.AddChild(_kit.Field(Loc.GetString("cmu-firearm-registry-signature"), entry.Signature, null, 110));
        column.AddChild(_kit.Field(Loc.GetString("cmu-firearm-registry-buyer"), entry.Buyer, null, 110));
        column.AddChild(_kit.Field(Loc.GetString("cmu-firearm-registry-order"), entry.Order, null, 110));
        column.AddChild(_kit.Field(Loc.GetString("cmu-firearm-registry-time"), FormatTime(entry.Time), null, 110));
        row.AddChild(column);
        card.AddChild(row);
        return card;
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
        var row = CrtConsoleKit.Row();
        var column = CrtConsoleKit.Column(2);
        column.HorizontalExpand = true;
        var stamp = FormatTime(entry.AddTime);
        if (!string.IsNullOrEmpty(entry.InitiatorName))
            stamp += " // " + entry.InitiatorName.ToUpperInvariant();
        column.AddChild(CrtConsoleKit.Wrapping(stamp, CrtTerminalPalette.Accent));
        column.AddChild(CrtConsoleKit.Wrapping(entry.Crime, CrtTerminalPalette.Text));
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
            delete.Label.FontOverride = _kit.Small;
            delete.OnPressed += _ => OnDeleteHistory?.Invoke(index);
            row.AddChild(delete);
        }

        panel.AddChild(row);
        return panel;
    }

    /// <summary>A timestamped entry, like a warrant or an arrest, with a confirm-to-remove button for those with access.</summary>
    private Control EntryCard(string stamp, string text, string removeText, Action remove)
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
        var row = CrtConsoleKit.Row();
        var column = CrtConsoleKit.Column(2);
        column.HorizontalExpand = true;
        column.AddChild(CrtConsoleKit.Wrapping(stamp, CrtTerminalPalette.Accent));
        column.AddChild(CrtConsoleKit.Wrapping(text, CrtTerminalPalette.Text));
        row.AddChild(column);

        if (_access)
        {
            var button = new ConfirmButton
            {
                Text = removeText,
                ConfirmationText = Loc.GetString("cmu-criminal-records-delete-confirm"),
                VerticalAlignment = VAlignment.Top,
                MinWidth = 70,
            };
            CmuButtonStyles.Apply(button, CmuButtonStyles.Variant.Danger);
            button.Label.FontOverride = _kit.Small;
            button.OnPressed += _ => remove();
            row.AddChild(button);
        }

        panel.AddChild(row);
        return panel;
    }

    private void OpenArrestDialog()
    {
        _dialog?.Close();

        const string charges = "charges";
        const string sentence = "sentence";
        var entries = new List<QuickDialogEntry>
        {
            new(charges, QuickDialogEntryType.LongText, Loc.GetString("cmu-records-arrest-charges-prompt")),
            new(sentence, QuickDialogEntryType.Integer, Loc.GetString("cmu-records-arrest-sentence-prompt")),
        };
        _dialog = new DialogWindow(Loc.GetString("cmu-records-arrests"), entries);
        CrtLobbyTheme.Apply(_dialog, useCrtTypography: true);
        _dialog.OnConfirmed += responses =>
        {
            var text = responses[charges].Trim();
            if (text.Length < 1 || text.Length > _maxLength || !int.TryParse(responses[sentence], out var minutes))
                return;

            OnLogArrest?.Invoke(text, Math.Clamp(minutes, 0, 120));
        };
        _dialog.OnClose += () => _dialog = null;
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

    /// <summary>A titled block in the record, with a heading that wraps; returns the column its contents go in.</summary>
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
        var column = CrtConsoleKit.Column(4);
        column.AddChild(CrtConsoleKit.BoldWrapping(Loc.GetString(title).ToUpperInvariant(), accent ?? CrtTerminalPalette.Accent));
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
        section.AddChild(_kit.Field(Loc.GetString(label), value, null, 110));
    }

    private static string FormatTime(TimeSpan time)
    {
        return $"{(int) time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }

    private static void SetWrapping(RichTextLabel label, string text, Color color)
    {
        var message = new FormattedMessage();
        message.PushColor(color);
        message.AddText(text);
        message.Pop();
        label.SetMessage(message);
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
}
