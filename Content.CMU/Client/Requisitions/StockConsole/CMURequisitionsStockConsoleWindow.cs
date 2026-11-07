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
///     Stock of the automated requisitions vendors on this ship, in the CRT theme: one card per item with its sprite, a
///     name that wraps, how many are left and whether it's running low. Search and a low-stock filter narrow the list.
///     Free items can be added to an order slip, which the console prints for requisitions to approve and fill.
/// </summary>
public sealed partial class CMURequisitionsStockConsoleWindow : DefaultWindow
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    public event Action? OnRefresh;
    public event Action<List<CMURequisitionsOrderLine>>? OnPrintSlip;

    /// <summary>At or under this share of the starting stock an item counts as running low.</summary>
    private const float LowShare = 0.25f;

    private readonly CrtConsoleKit _kit = new();

    private readonly RichTextLabel _subheading;
    private readonly PanelContainer _outTag;
    private readonly Label _outText;
    private readonly LineEdit _search;
    private readonly Button _lowOnly;
    private readonly BoxContainer _sections;
    private readonly RichTextLabel _orderSummary;
    private readonly PanelContainer _orderTag;
    private readonly Label _orderTagText;
    private readonly Button _clearSlip;
    private readonly Button _printSlip;

    // Item prototype -> how many are on the slip being put together.
    private readonly Dictionary<string, int> _order = new();
    private int _maxSlipItems = 8;

    // Cards are kept between refreshes so the scroll position and search survive the automatic updates.
    private readonly List<Row> _rows = new();
    private readonly List<(Control Section, List<Row> Rows)> _groups = new();
    private string? _layoutKey;

    private sealed class Row
    {
        public required string Id;
        public required string Name;
        public Label? OrderCount;
        public PanelContainer? OrderChip;
        public Button? OrderAdd;

        /// <summary>What's left in the vendor, or null when it's unlimited.</summary>
        public int? Stock;
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

        // The kit's header and footer cut their text off at the edge; these wrap instead, so nothing needs a wider
        // window to be read.
        _outTag = _kit.Tag(out _outText);
        var header = CrtConsoleKit.Strip(CrtTerminalPalette.Surface3);
        var headerRow = CrtConsoleKit.Row();
        var titles = CrtConsoleKit.Column(0);
        titles.HorizontalExpand = true;
        titles.AddChild(CrtConsoleKit.Text(Loc.GetString("cmu-req-stock-console-heading"), _kit.Title, CrtTerminalPalette.TextBright));
        _subheading = CrtConsoleKit.Wrapping(string.Empty, CrtTerminalPalette.TextDim);
        titles.AddChild(_subheading);
        headerRow.AddChild(titles);
        _outTag.VerticalAlignment = VAlignment.Top;
        headerRow.AddChild(_outTag);
        header.AddChild(headerRow);
        _kit.Root.AddChild(header);

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

        // The slip being put together, in a strip like the header so it reads as part of the console, not the list.
        var orderStrip = CrtConsoleKit.Strip(CrtTerminalPalette.Surface3);
        // Heading and buttons on one line, the hint under them across the full width so it never gets cut off.
        var orderColumn = CrtConsoleKit.Column(2);
        var order = CrtConsoleKit.Row(6);
        var orderHeading = CrtConsoleKit.Text(Loc.GetString("cmu-req-stock-console-order-heading"), _kit.Bold,
            CrtTerminalPalette.TextBright);
        orderHeading.HorizontalExpand = true;
        orderHeading.VerticalAlignment = VAlignment.Center;
        order.AddChild(orderHeading);
        _orderTag = _kit.Tag(out _orderTagText);
        order.AddChild(_orderTag);
        _clearSlip = _kit.Button(Loc.GetString("cmu-req-stock-console-order-clear"));
        _clearSlip.VerticalAlignment = VAlignment.Center;
        _clearSlip.OnPressed += _ =>
        {
            _order.Clear();
            UpdateOrder();
        };
        order.AddChild(_clearSlip);
        _printSlip = _kit.Button(Loc.GetString("cmu-req-stock-console-order-print"), CmuButtonStyles.Variant.Affirm);
        _printSlip.VerticalAlignment = VAlignment.Center;
        _printSlip.OnPressed += _ =>
        {
            OnPrintSlip?.Invoke(_order.Select(o => new CMURequisitionsOrderLine { Id = o.Key, Count = o.Value }).ToList());
            _order.Clear();
            UpdateOrder();
        };
        order.AddChild(_printSlip);
        orderColumn.AddChild(order);
        _orderSummary = CrtConsoleKit.Wrapping(string.Empty, CrtTerminalPalette.TextDim);
        orderColumn.AddChild(_orderSummary);
        orderStrip.AddChild(orderColumn);
        _kit.Root.AddChild(orderStrip);

        var footer = CrtConsoleKit.Strip(CrtTerminalPalette.Surface1);
        footer.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("cmu-req-stock-console-footer"), CrtTerminalPalette.TextDim));
        _kit.Root.AddChild(footer);

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
        // Stock can run out while a slip is being put together; trim the slip to what's left.
        var left = new Dictionary<string, int?>();
        foreach (var item in state.Vendors.SelectMany(v => v.Sections).SelectMany(s => s.Items))
        {
            var seen = left.TryGetValue(item.Id, out var known);
            left[item.Id] = item.Amount is not { } amount || seen && known == null
                ? null
                : (known ?? 0) + Math.Max(0, amount);
        }

        foreach (var (id, count) in _order.ToList())
        {
            if (left.GetValueOrDefault(id) is { } have && count > have)
            {
                if (have <= 0)
                    _order.Remove(id);
                else
                    _order[id] = have;
            }
        }

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

        SetWrapping(_subheading, Loc.GetString("cmu-req-stock-console-summary",
            ("vendors", state.Vendors.Count), ("items", items), ("empty", empty)), CrtTerminalPalette.TextDim);
        CrtConsoleKit.SetTag(_outTag, _outText,
            empty > 0
                ? Loc.GetString("cmu-req-stock-console-tag-out", ("count", empty))
                : low > 0
                    ? Loc.GetString("cmu-req-stock-console-tag-low", ("count", low))
                    : Loc.GetString("cmu-req-stock-console-tag-ok"),
            empty > 0 ? CrtTerminalPalette.Alert : low > 0 ? CrtTerminalPalette.Caution : CrtTerminalPalette.Accent);

        ApplyFilter();
        UpdateOrder();
    }

    /// <summary>How many items this viewer's slips can hold, sent separately since command staff get more.</summary>
    public void SetMaxSlipItems(int max)
    {
        _maxSlipItems = max;
        UpdateOrder();
    }

    private void UpdateOrder()
    {
        var total = _order.Values.Sum();
        var full = total >= _maxSlipItems;
        SetWrapping(_orderSummary, Loc.GetString(total == 0
            ? "cmu-req-stock-console-order-empty"
            : full
                ? "cmu-req-stock-console-order-full"
                : "cmu-req-stock-console-order-hint"), CrtTerminalPalette.TextDim);
        CrtConsoleKit.SetTag(_orderTag, _orderTagText,
            Loc.GetString("cmu-req-stock-console-order-summary", ("count", total), ("max", _maxSlipItems)),
            full ? CrtTerminalPalette.Caution : total > 0 ? CrtTerminalPalette.Accent : CrtTerminalPalette.TextDim);
        _printSlip.Disabled = total == 0;
        _clearSlip.Disabled = total == 0;

        foreach (var row in _rows)
        {
            if (row.OrderCount == null)
                continue;

            var count = _order.GetValueOrDefault(row.Id);
            row.OrderCount.Text = count.ToString();
            row.OrderCount.FontColorOverride = count > 0 ? CrtTerminalPalette.Void : CrtTerminalPalette.TextDim;
            row.OrderChip!.PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = count > 0 ? CrtTerminalPalette.Accent : CrtTerminalPalette.Surface1,
            };
            row.OrderAdd!.Disabled = full || row.Stock is { } stock && count >= stock;
        }
    }

    private void ChangeOrder(string id, int change)
    {
        if (change > 0 && _order.Values.Sum() >= _maxSlipItems)
            return;

        // Can't ask for more than the vendors have left.
        var stock = _rows.Where(r => r.Id == id).Select(r => r.Stock).ToList();
        if (change > 0 && stock.All(s => s != null) && _order.GetValueOrDefault(id) >= stock.Sum(s => s!.Value))
            return;

        var count = Math.Max(0, _order.GetValueOrDefault(id) + change);
        if (count == 0)
            _order.Remove(id);
        else
            _order[id] = count;

        UpdateOrder();
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
                var panel = Section(section.Name, section.Items.Count, out var column);
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

        // One line under the name: stock count on the left, then (for items that can go on a slip) the order
        // stepper, then the status chip, all centred on the same line so nothing pushes the name narrower.
        var meta = CrtConsoleKit.Row(6);
        var count = CrtConsoleKit.Text(string.Empty, _kit.Bold, CrtTerminalPalette.Text);
        count.VerticalAlignment = VAlignment.Center;
        meta.AddChild(count);
        meta.AddChild(new Control { HorizontalExpand = true });

        Label? orderCount = null;
        PanelContainer? orderChip = null;
        Button? orderAdd = null;
        if (item.Orderable)
        {
            var orderBox = CrtConsoleKit.Row(2);
            orderBox.VerticalAlignment = VAlignment.Center;
            var remove = OrderStepButton(Loc.GetString("cmu-req-stock-console-order-remove"));
            remove.ToolTip = Loc.GetString("cmu-req-stock-console-order-remove-tooltip");
            remove.OnPressed += _ => ChangeOrder(item.Id, -1);
            orderBox.AddChild(remove);
            orderChip = new PanelContainer { SetSize = StepSize with { X = 26 }, VerticalAlignment = VAlignment.Center };
            orderCount = CrtConsoleKit.Text("0", _kit.Small, CrtTerminalPalette.TextDim);
            orderCount.HorizontalAlignment = HAlignment.Center;
            orderCount.VerticalAlignment = VAlignment.Center;
            orderChip.AddChild(orderCount);
            orderBox.AddChild(orderChip);
            orderAdd = OrderStepButton(Loc.GetString("cmu-req-stock-console-order-add"));
            orderAdd.ToolTip = Loc.GetString("cmu-req-stock-console-order-add-tooltip");
            orderAdd.OnPressed += _ => ChangeOrder(item.Id, 1);
            orderBox.AddChild(orderAdd);
            meta.AddChild(orderBox);
        }

        var chip = new PanelContainer { VerticalAlignment = VAlignment.Center, MinHeight = StepSize.Y };
        var chipText = CrtConsoleKit.Text(string.Empty, _kit.Small, CrtTerminalPalette.Void);
        chipText.Margin = new Thickness(6, 0);
        chipText.VerticalAlignment = VAlignment.Center;
        chip.AddChild(chipText);
        meta.AddChild(chip);
        column.AddChild(meta);
        line.AddChild(column);

        panel.AddChild(line);
        return new Row
        {
            Id = item.Id,
            Name = item.Name,
            OrderCount = orderCount,
            OrderChip = orderChip,
            OrderAdd = orderAdd,
            Bar = bar,
            Count = count,
            Chip = chip,
            ChipText = chipText,
            Card = panel,
        };
    }

    /// <summary>
    ///     A titled block like the kit's, but its heading wraps rather than being cut off, so long section names fit.
    /// </summary>
    private PanelContainer Section(string title, int count, out BoxContainer column)
    {
        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = CrtTerminalPalette.Surface1,
                ContentMarginLeftOverride = 10,
                ContentMarginRightOverride = 10,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 8,
            },
        };
        column = CrtConsoleKit.Column(6);
        var head = CrtConsoleKit.Row();
        var heading = CrtConsoleKit.BoldWrapping(title.ToUpperInvariant(), CrtTerminalPalette.Accent);
        heading.VerticalAlignment = VAlignment.Center;
        head.AddChild(heading);
        var chip = _kit.Chip(count.ToString(), CrtTerminalPalette.TextDim);
        chip.VerticalAlignment = VAlignment.Top;
        head.AddChild(chip);
        column.AddChild(head);
        column.AddChild(new PanelContainer
        {
            MinHeight = 1,
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Line },
        });
        panel.AddChild(column);
        return panel;
    }

    private static void SetWrapping(RichTextLabel label, string text, Color color)
    {
        var message = new Robust.Shared.Utility.FormattedMessage();
        message.PushColor(color);
        message.AddText(text);
        message.Pop();
        label.SetMessage(message);
    }

    /// <summary>The order stepper's buttons and count are the same size as the status chip beside them.</summary>
    private static readonly Vector2 StepSize = new(20, 18);

    private Button OrderStepButton(string text)
    {
        var button = _kit.Button(text);
        // The shared button style pads for words; a one-character key wants even, tight padding to centre it.
        if (button.StyleBoxOverride is { } box)
        {
            box.ContentMarginLeftOverride = 0;
            box.ContentMarginRightOverride = 0;
            box.ContentMarginTopOverride = 0;
            box.ContentMarginBottomOverride = 0;
        }

        button.SetSize = StepSize;
        button.VerticalAlignment = VAlignment.Center;
        button.Label.Align = Label.AlignMode.Center;
        button.Label.VAlign = Label.VAlignMode.Center;
        return button;
    }

    private static void UpdateRow(Row row, CMURequisitionsStockItem item)
    {
        row.Stock = item.Amount;
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
