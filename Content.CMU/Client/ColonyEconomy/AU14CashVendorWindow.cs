using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Telephone;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.ColonyEconomy;

/// <summary>
///     A cash vending machine's front, drawn as the machine: a lit product window of cards with a picture, name and
///     price tag each, and a payment panel with the credit display, the ID card balance, and the change and ID keys.
/// </summary>
public sealed class AU14CashVendorWindow : DefaultWindow
{
    private static readonly Color Cabinet = Color.FromHex("#1f2226");
    private static readonly Color CabinetEdge = Color.FromHex("#0f1113");
    private static readonly Color Glass = Color.FromHex("#14181c");
    private static readonly Color Card = Color.FromHex("#262b31");
    private static readonly Color CardHover = Color.FromHex("#303740");
    private static readonly Color CardEdge = Color.FromHex("#3a424b");
    private static readonly Color Text = Color.FromHex("#c9cdd2");
    private static readonly Color TextDim = Color.FromHex("#7d848c");
    private static readonly Color Tag = Color.FromHex("#c99a2e");
    private static readonly Color TagInk = Color.FromHex("#1a1406");
    private static readonly Color Panel = Color.FromHex("#2b3036");
    private static readonly Color Led = Color.FromHex("#5fd36a");
    private static readonly Color LedBack = Color.FromHex("#0b120c");
    private static readonly Color Key = Color.FromHex("#3d444c");
    private static readonly Color KeyEdge = Color.FromHex("#14171a");
    private static readonly Color Short = Color.FromHex("#b65a48");

    public event Action<int>? OnBuyPressed;
    public event Action? OnScanIdPressed;
    public event Action? OnReturnChangePressed;

    private readonly IEntityManager _entities;
    private readonly IPlayerManager _player;
    private readonly IPrototypeManager _prototypes;

    private readonly Font _name;
    private readonly Font _small;
    private readonly Font _bold;
    private readonly Font _led;

    private readonly LineEdit _search;
    private readonly BoxContainer _sections;
    private readonly Label _empty;
    private readonly Label _credit;
    private readonly BoxContainer _idRow;
    private readonly Label _idBalance;
    private readonly BoxContainer _deptRow;
    private readonly Label _deptBalance;
    private readonly Button _change;
    private readonly Button _scanId;
    private readonly Label _tax;

    private AU14CashVendorBuiState? _state;

    public AU14CashVendorWindow()
    {
        _entities = IoCManager.Resolve<IEntityManager>();
        _player = IoCManager.Resolve<IPlayerManager>();
        _prototypes = IoCManager.Resolve<IPrototypeManager>();

        _name = CMUPhoneStyle.Font(CMUPhoneStyle.Sans, 11);
        _small = CMUPhoneStyle.Font(CMUPhoneStyle.Sans, 10);
        _bold = CMUPhoneStyle.Font(CMUPhoneStyle.SansBold, 11);
        _led = CMUPhoneStyle.Font(CMUPhoneStyle.MonoBold, 20);

        MinSize = new Vector2(640, 480);
        SetSize = new Vector2(720, 560);

        var cabinet = CMUPhoneStyle.Panel(Cabinet, CabinetEdge, 3, 10, 10);
        cabinet.HorizontalExpand = true;
        cabinet.VerticalExpand = true;
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        cabinet.AddChild(row);

        // Product window.
        var glass = CMUPhoneStyle.Panel(Glass, CabinetEdge, 2, 8, 8);
        glass.HorizontalExpand = true;
        glass.VerticalExpand = true;
        var products = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        _search = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("cash-vendor-search-placeholder") };
        _search.OnTextChanged += _ => RefreshList();
        products.AddChild(_search);
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        var listColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        _sections = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4, HorizontalExpand = true };
        listColumn.AddChild(_sections);
        _empty = CMUPhoneStyle.Text(Loc.GetString("cash-vendor-no-items"), _bold, TextDim);
        _empty.HorizontalAlignment = HAlignment.Center;
        _empty.Margin = new Thickness(0, 20, 0, 0);
        listColumn.AddChild(_empty);
        scroll.AddChild(listColumn);
        products.AddChild(scroll);
        glass.AddChild(products);
        row.AddChild(glass);

        // Payment panel.
        var payment = CMUPhoneStyle.Panel(Panel, CabinetEdge, 2, 10, 10);
        payment.MinWidth = 190;
        payment.MaxWidth = 190;
        payment.VerticalExpand = true;
        var pay = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        pay.AddChild(CMUPhoneStyle.Text(Loc.GetString("cmu-cash-vendor-credit"), _bold, TextDim));
        var display = CMUPhoneStyle.Panel(LedBack, CabinetEdge, 2, 8, 4);
        _credit = CMUPhoneStyle.Text("$0", _led, Led);
        _credit.Align = Label.AlignMode.Right;
        display.AddChild(_credit);
        pay.AddChild(display);

        _idRow = Balance("cash-vendor-id-account", out _idBalance);
        pay.AddChild(_idRow);
        _deptRow = Balance("cash-vendor-department-budget", out _deptBalance);
        pay.AddChild(_deptRow);

        pay.AddChild(new Control { VerticalExpand = true });

        _scanId = CMUPhoneStyle.Key(Loc.GetString("cash-vendor-scan-id"), _bold, Key, KeyEdge, Text);
        _scanId.OnPressed += _ => OnScanIdPressed?.Invoke();
        pay.AddChild(_scanId);
        _change = CMUPhoneStyle.Key(Loc.GetString("cash-vendor-return-change"), _bold, Key, KeyEdge, Text);
        _change.OnPressed += _ => OnReturnChangePressed?.Invoke();
        pay.AddChild(_change);
        _tax = CMUPhoneStyle.Text(string.Empty, _small, TextDim);
        _tax.Align = Label.AlignMode.Center;
        pay.AddChild(_tax);
        payment.AddChild(pay);
        row.AddChild(payment);

        Contents.AddChild(cabinet);
    }

    private BoxContainer Balance(string caption, out Label value)
    {
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var label = CMUPhoneStyle.Text(Loc.GetString(caption).TrimEnd(':').ToUpperInvariant(), _small, TextDim);
        column.AddChild(label);
        value = CMUPhoneStyle.Text(string.Empty, _bold, Text);
        value.ClipText = true;
        column.AddChild(value);
        return column;
    }

    public void UpdateState(AU14CashVendorBuiState state)
    {
        _state = state;
        _credit.Text = $"${MathF.Floor(state.InsertedCash):0}";
        _change.Disabled = state.InsertedCash <= 0;
        _tax.Text = state.SalesTaxPercent > 0
            ? Loc.GetString("cash-vendor-sales-tax", ("percent", state.SalesTaxPercent.ToString("F0")))
            : Loc.GetString("cash-vendor-no-sales-tax");

        _scanId.Visible = state.AllowDepartmentBudget;
        _scanId.Text = Loc.GetString(state.HasDepartmentMode ? "cash-vendor-clear-department" : "cash-vendor-scan-id");

        var card = GetCardBalance();
        _idRow.Visible = !state.HasDepartmentMode && card != null;
        if (card is { } balance)
            _idBalance.Text = $"${balance}";

        _deptRow.Visible = state.HasDepartmentMode;
        if (state.HasDepartmentMode)
            _deptBalance.Text = $"${state.DepartmentBudget:F0} ({state.DepartmentName})";

        RefreshList();
    }

    private void RefreshList()
    {
        _sections.RemoveAllChildren();
        if (_state is not { } state)
            return;

        var filter = _search.Text.Trim();
        var available = state.InsertedCash + (GetCardBalance() ?? 0);
        var shown = 0;

        // Group by section, in the order each section first appears; anything unsectioned goes last.
        var groups = new List<(string? Section, List<AU14CashVendorItemState> Items)>();
        foreach (var item in state.Items)
        {
            if (filter.Length > 0 && !item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            var key = string.IsNullOrWhiteSpace(item.Section) ? null : item.Section;
            var index = groups.FindIndex(g => g.Section == key);
            if (index < 0)
            {
                groups.Add((key, new List<AU14CashVendorItemState>()));
                index = groups.Count - 1;
            }

            groups[index].Items.Add(item);
        }

        groups.Sort((a, b) => (a.Section == null).CompareTo(b.Section == null));
        var headed = groups.Any(g => g.Section != null);
        foreach (var (section, items) in groups)
        {
            if (headed)
            {
                var title = section ?? Loc.GetString("cmu-cash-vendor-other");
                var header = CMUPhoneStyle.Text(title.ToUpperInvariant(), _bold, Tag);
                header.Margin = new Thickness(2, shown > 0 ? 8 : 0, 0, 2);
                _sections.AddChild(header);
                _sections.AddChild(new PanelContainer { MinHeight = 1, PanelOverride = CMUPhoneStyle.Box(CardEdge) });
            }

            var grid = new GridContainer { Columns = 3, HSeparationOverride = 6, VSeparationOverride = 6, HorizontalExpand = true };
            foreach (var item in items)
            {
                var affordable = state.HasDepartmentMode ? state.DepartmentBudget >= item.EffectivePrice : available >= item.EffectivePrice;
                grid.AddChild(ProductCard(item, affordable));
                shown++;
            }

            _sections.AddChild(grid);
        }

        _empty.Visible = shown == 0;
    }

    private Control ProductCard(AU14CashVendorItemState item, bool affordable)
    {
        var button = new ContainerButton { HorizontalExpand = true, MinHeight = 108, Disabled = !affordable, ToolTip = item.Name };
        var panel = CMUPhoneStyle.Panel(Card, CardEdge, 1, 6, 6);
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };

        if (_prototypes.HasIndex<EntityPrototype>(item.ItemProtoId))
        {
            var picture = new EntityPrototypeView
            {
                SetSize = new Vector2(48, 48),
                Scale = new Vector2(1.5f, 1.5f),
                HorizontalAlignment = HAlignment.Center,
            };
            picture.SetPrototype(item.ItemProtoId);
            if (!affordable)
                picture.Modulate = Color.White.WithAlpha(0.4f);
            column.AddChild(picture);
        }

        var name = CMUPhoneStyle.Text(item.Name, _name, affordable ? Text : TextDim);
        name.ClipText = true;
        name.Align = Label.AlignMode.Center;
        column.AddChild(name);

        var tag = CMUPhoneStyle.Panel(affordable ? Tag : Short, null, 0, 6, 1);
        tag.HorizontalAlignment = HAlignment.Center;
        tag.AddChild(CMUPhoneStyle.Text($"${item.EffectivePrice}", _bold, TagInk));
        column.AddChild(tag);

        panel.AddChild(column);
        button.AddChild(panel);
        if (affordable)
        {
            button.OnMouseEntered += _ => panel.PanelOverride = CMUPhoneStyle.Box(CardHover, Tag, 1, 6, 6);
            button.OnMouseExited += _ => panel.PanelOverride = CMUPhoneStyle.Box(Card, CardEdge, 1, 6, 6);
        }

        var index = item.Index;
        button.OnPressed += _ => OnBuyPressed?.Invoke(index);
        return button;
    }

    /// <summary>The balance on the local player's ID card, or null if they don't have one on them.</summary>
    private int? GetCardBalance()
    {
        if (_player.LocalEntity is not { } player ||
            !_entities.System<SharedIdCardSystem>().TryFindIdCard(player, out var card))
        {
            return null;
        }

        return card.Comp.AccountBalance;
    }
}
