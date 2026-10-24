using System.Linq;
using System.Numerics;
using System.Text;
using Content.Client.CMU14.Interface;
using Content.Shared.CMU14.Requisitions.StockConsole;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using static System.StringComparison;

namespace Content.Client.CMU14.Requisitions.StockConsole;

/// <summary>
///     Read-only stock of the automated requisitions vendors on this ship, in the CRT theme: one card per item with
///     its sprite, a name that wraps, how many are left and whether it's running low. Search and a low-stock filter
///     narrow the list.
/// </summary>
public sealed class CMURequisitionsStockConsoleWindow : DefaultWindow
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    public event Action? OnRefresh;

    /// <summary>At or under this share of the starting stock an item counts as running low.</summary>
    private const float LowShare = 0.25f;

    private readonly CrtConsoleKit _kit = new();

    private readonly Label _subheading;
    private readonly PanelContainer _outTag;
    private readonly Label _outText;
    private readonly LineEdit _search;
    private readonly Button _lowOnly;
    private readonly BoxContainer _sections;
    private readonly Label _footer;

    // Cards are kept between refreshes so the scroll position and search survive the automatic updates.
    private readonly List<Row> _rows = new();
    private readonly List<(Control Section, List<Row> Rows)> _groups = new();
    private string? _layoutKey;

    private sealed class Row
    {
        public required string Name;
        public required PanelContainer Bar;
        public required Label Count;
        public required PanelContainer Chip;
        public required Label ChipText;
        public required Control Card;
        public bool Low;
    }

    public CMURequisitionsStockConsoleWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("cmu-req-stock-console-window-title");
        MinSize = new Vector2(440, 520);
        SetSize = new Vector2(540, 720);

        _outTag = _kit.Tag(out _outText);
        _kit.Root.AddChild(_kit.Header(Loc.GetString("cmu-req-stock-console-heading"), out _subheading, _outTag));

        var tools = CrtConsoleKit.Row(6);
        _search = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("cmu-req-stock-console-search-placeholder"),
        };
        _search.OnTextChanged += _ => ApplyFilter();
        tools.AddChild(_search);
        _lowOnly = _kit.Button(Loc.GetString("cmu-req-stock-console-low-only"));
        _lowOnly.ToggleMode = true;
        _lowOnly.OnToggled += _ => ApplyFilter();
        tools.AddChild(_lowOnly);
        var refresh = _kit.Button(Loc.GetString("cmu-req-stock-console-refresh-button"));
        refresh.OnPressed += _ => OnRefresh?.Invoke();
        tools.AddChild(refresh);
        _kit.Root.AddChild(tools);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _sections = CrtConsoleKit.Column(8);
        _sections.HorizontalExpand = true;
        _sections.Margin = new Thickness(0, 0, 6, 0);
        scroll.AddChild(_sections);
        _kit.Root.AddChild(scroll);

        _kit.Root.AddChild(_kit.Footer(out _footer));
        _footer.Text = Loc.GetString("cmu-req-stock-console-footer");

        Contents.AddChild(_kit.Tube);
    }

    public void UpdateState(CMURequisitionsStockConsoleBuiState state)
    {
        var layoutKey = GetLayoutKey(state);
        if (layoutKey != _layoutKey)
        {
            _layoutKey = layoutKey;
            Rebuild(state);
        }

        var items = 0;
        var empty = 0;
        var low = 0;
        var index = 0;
        foreach (var item in state.Vendors.SelectMany(v => v.Sections).SelectMany(s => s.Items))
        {
            items++;
            if (item.Amount is <= 0)
                empty++;
            else if (IsLow(item))
                low++;

            if (index < _rows.Count)
                UpdateRow(_rows[index], item);
            index++;
        }

        _subheading.Text = Loc.GetString("cmu-req-stock-console-summary",
            ("vendors", state.Vendors.Count), ("items", items), ("empty", empty));
        CrtConsoleKit.SetTag(_outTag, _outText,
            empty > 0
                ? Loc.GetString("cmu-req-stock-console-tag-out", ("count", empty))
                : low > 0
                    ? Loc.GetString("cmu-req-stock-console-tag-low", ("count", low))
                    : Loc.GetString("cmu-req-stock-console-tag-ok"),
            empty > 0 ? CrtTerminalPalette.Alert : low > 0 ? CrtTerminalPalette.Caution : CrtTerminalPalette.Accent);

        ApplyFilter();
    }

    private void Rebuild(CMURequisitionsStockConsoleBuiState state)
    {
        _sections.RemoveAllChildren();
        _rows.Clear();
        _groups.Clear();

        if (state.Vendors.Count == 0)
        {
            var none = CrtConsoleKit.Wrapping(Loc.GetString("cmu-req-stock-console-no-vendors"), CrtTerminalPalette.TextDim);
            none.Margin = new Thickness(0, 24, 0, 0);
            _sections.AddChild(none);
            return;
        }

        foreach (var vendor in state.Vendors)
        {
            // With more than one vendor on the ship, label whose stock each block of sections is.
            if (state.Vendors.Count > 1)
            {
                var header = CrtConsoleKit.BoldWrapping(vendor.Name.ToUpperInvariant(), CrtTerminalPalette.TextBright);
                header.Margin = new Thickness(0, 6, 0, 0);
                _sections.AddChild(header);
            }

            foreach (var section in vendor.Sections)
            {
                var panel = _kit.Section(section.Name, out var column, null,
                    _kit.Chip(section.Items.Count.ToString(), CrtTerminalPalette.TextDim));
                var rows = new List<Row>();
                foreach (var item in section.Items)
                {
                    var row = BuildRow(item);
                    column.AddChild(row.Card);
                    rows.Add(row);
                    _rows.Add(row);
                }

                _sections.AddChild(panel);
                _groups.Add((panel, rows));
            }
        }
    }

    private Row BuildRow(CMURequisitionsStockItem item)
    {
        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 },
        };
        var line = CrtConsoleKit.Row(8);
        var bar = new PanelContainer { MinWidth = 4 };
        line.AddChild(bar);

        var frame = new PanelContainer
        {
            MinSize = new Vector2(40, 40),
            VerticalAlignment = VAlignment.Center,
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface1 },
        };
        if (_prototypes.TryIndex<EntityPrototype>(item.Id, out var entity))
        {
            var view = new EntityPrototypeView
            {
                SetSize = new Vector2(32, 32),
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                Stretch = SpriteView.StretchMode.Fill,
            };
            view.SetPrototype(entity.ID);
            frame.AddChild(view);
            frame.ToolTip = entity.Description;
            frame.TooltipDelay = 0;
        }

        line.AddChild(frame);

        // The name goes on its own line, last in the row, so it can use all the width that's left and wrap.
        var column = CrtConsoleKit.Column(2);
        column.HorizontalExpand = true;
        column.Margin = new Thickness(0, 5, 8, 5);
        column.AddChild(CrtConsoleKit.BoldWrapping(item.Name.ToUpperInvariant(), CrtTerminalPalette.TextBright));

        var meta = CrtConsoleKit.Row(6);
        var count = CrtConsoleKit.Text(string.Empty, _kit.Bold, CrtTerminalPalette.Text);
        meta.AddChild(count);
        meta.AddChild(new Control { HorizontalExpand = true });
        var chip = new PanelContainer { VerticalAlignment = VAlignment.Center };
        var chipText = CrtConsoleKit.Text(string.Empty, _kit.Small, CrtTerminalPalette.Void);
        chipText.Margin = new Thickness(6, 1);
        chip.AddChild(chipText);
        meta.AddChild(chip);
        column.AddChild(meta);
        line.AddChild(column);

        panel.AddChild(line);
        return new Row { Name = item.Name, Bar = bar, Count = count, Chip = chip, ChipText = chipText, Card = panel };
    }

    private static void UpdateRow(Row row, CMURequisitionsStockItem item)
    {
        var empty = item.Amount is <= 0;
        row.Low = empty || IsLow(item);

        row.Count.Text = item.Amount is not { } amount
            ? Loc.GetString("cmu-req-stock-console-unlimited")
            : item.Max is { } max and > 0
                ? Loc.GetString("cmu-req-stock-console-amount-max", ("amount", amount), ("max", max))
                : Loc.GetString("cmu-req-stock-console-amount", ("amount", amount));

        var (status, color) = empty
            ? ("cmu-req-stock-console-status-out", CrtTerminalPalette.Alert)
            : IsLow(item)
                ? ("cmu-req-stock-console-status-low", CrtTerminalPalette.Caution)
                : ("cmu-req-stock-console-status-ok", CrtTerminalPalette.Accent);

        row.Count.FontColorOverride = empty ? CrtTerminalPalette.Alert : CrtTerminalPalette.Text;
        row.Bar.PanelOverride = new StyleBoxFlat { BackgroundColor = color };
        row.Chip.PanelOverride = new StyleBoxFlat { BackgroundColor = color };
        row.ChipText.Text = Loc.GetString(status);
    }

    private static bool IsLow(CMURequisitionsStockItem item)
    {
        return item.Amount is { } amount && item.Max is { } max && max > 0 && amount <= max * LowShare;
    }

    private void ApplyFilter()
    {
        var text = _search.Text.Trim();
        var lowOnly = _lowOnly.Pressed;
        foreach (var (section, rows) in _groups)
        {
            var any = false;
            foreach (var row in rows)
            {
                row.Card.Visible = (!lowOnly || row.Low) &&
                                   (text.Length == 0 || row.Name.Contains(text, OrdinalIgnoreCase));
                any |= row.Card.Visible;
            }

            section.Visible = any;
        }
    }

    private static string GetLayoutKey(CMURequisitionsStockConsoleBuiState state)
    {
        var key = new StringBuilder();
        foreach (var vendor in state.Vendors)
        {
            key.Append(vendor.Name).Append('|');
            foreach (var section in vendor.Sections)
            {
                key.Append(section.Name).Append(':');
                foreach (var item in section.Items)
                    key.Append(item.Id).Append(',');
            }
        }

        return key.ToString();
    }
}
