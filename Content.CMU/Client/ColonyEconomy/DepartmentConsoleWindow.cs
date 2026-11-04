using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Client.UserInterface.Controls;
using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.ColonyEconomy;

/// <summary>
///     A colony department's console, in the CRT theme. The staff page runs the budget, pay and staff; the orders page
///     buys from the department's catalog.
/// </summary>
public sealed class DepartmentConsoleWindow : DefaultWindow
{
    public event Action<float>? OnWithdraw;
    public event Action<int>? OnSetDefaultSalary;
    public event Action<string>? OnAnnounce;
    public event Action<NetEntity>? OnFirePressed;
    public event Action<NetEntity, int>? OnSetIndividualSalaryPressed;
    public event Action<NetEntity>? OnRemoveOverridePressed;
    public event Action<int, int, string, string>? OnOrderPressed;

    private enum Page
    {
        Staff,
        Orders,
    }

    private readonly CrtConsoleKit _kit = new();

    private readonly Label _subheading;
    private readonly PanelContainer _budgetTag;
    private readonly Label _budgetText;
    private readonly Dictionary<Page, Button> _pageButtons = new();
    private readonly Control _staffPage;
    private readonly Control _ordersPage;
    private readonly Label _footer;

    private readonly LineEdit _withdraw;
    private readonly LineEdit _defaultSalary;
    private readonly Label _staffCount;
    private readonly BoxContainer _staff;
    private readonly LineEdit _announcement;

    private readonly LineEdit _reason;
    private readonly LineEdit _deliverTo;
    private readonly BoxContainer _categories;
    private readonly Label _categoryTitle;
    private readonly BoxContainer _items;

    /// <summary>What's been typed into each employee's pay box, so a state update doesn't wipe it.</summary>
    private readonly Dictionary<NetEntity, string> _typedSalaries = new();

    private DepartmentConsoleBuiState? _state;
    private Page _page = Page.Staff;
    private int _category = -1;

    public DepartmentConsoleWindow()
    {
        Title = Loc.GetString("department-console-title");
        MinSize = new Vector2(640, 560);
        SetSize = new Vector2(700, 660);

        _budgetTag = _kit.Tag(out _budgetText);
        _kit.Root.AddChild(_kit.Header(Loc.GetString("cmu-department-heading"), out _subheading, _budgetTag));

        var tabs = CrtConsoleKit.Row(4);
        foreach (var page in Enum.GetValues<Page>())
        {
            var button = _kit.Button(Loc.GetString($"cmu-department-page-{page.ToString().ToLowerInvariant()}"));
            button.MinWidth = 120;
            button.Label.FontOverride = _kit.Bold;
            var captured = page;
            button.OnPressed += _ => ShowPage(captured);
            tabs.AddChild(button);
            _pageButtons[page] = button;
        }

        _kit.Root.AddChild(tabs);

        var pages = new Control { VerticalExpand = true, HorizontalExpand = true };
        _kit.Root.AddChild(pages);

        // ── Staff page ──
        var staffScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        var staff = CrtConsoleKit.Column(8);
        staff.HorizontalExpand = true;
        staff.Margin = new Thickness(0, 0, 6, 0);
        staffScroll.AddChild(staff);
        _staffPage = staffScroll;
        pages.AddChild(staffScroll);

        staff.AddChild(_kit.Section(Loc.GetString("cmu-department-treasury"), out var treasury));
        var withdrawRow = CrtConsoleKit.Row(4);
        withdrawRow.AddChild(Key("cmu-department-withdraw"));
        _withdraw = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("department-console-amount-placeholder") };
        _withdraw.OnTextEntered += _ => Withdraw();
        withdrawRow.AddChild(_withdraw);
        var withdraw = _kit.Button(Loc.GetString("cmu-department-withdraw-button"), CmuButtonStyles.Variant.Caution);
        withdraw.MinWidth = 130;
        withdraw.OnPressed += _ => Withdraw();
        withdrawRow.AddChild(withdraw);
        treasury.AddChild(withdrawRow);

        var salaryRow = CrtConsoleKit.Row(4);
        salaryRow.AddChild(Key("cmu-department-default-salary"));
        _defaultSalary = new LineEdit { HorizontalExpand = true };
        _defaultSalary.OnTextEntered += _ => SetDefaultSalary();
        salaryRow.AddChild(_defaultSalary);
        var setSalary = _kit.Button(Loc.GetString("cmu-department-set-default"));
        setSalary.MinWidth = 130;
        setSalary.OnPressed += _ => SetDefaultSalary();
        salaryRow.AddChild(setSalary);
        treasury.AddChild(salaryRow);

        _staffCount = CrtConsoleKit.Text(string.Empty, _kit.Small, CrtTerminalPalette.TextDim);
        staff.AddChild(_kit.Section(Loc.GetString("cmu-department-staff"), out var staffList, right: _staffCount));
        staffList.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("cmu-department-hire-hint"), CrtTerminalPalette.TextDim));
        _staff = CrtConsoleKit.Column(4);
        staffList.AddChild(_staff);

        staff.AddChild(_kit.Section(Loc.GetString("cmu-department-announcement"), out var announce));
        var announceRow = CrtConsoleKit.Row(4);
        _announcement = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("department-console-announcement-placeholder"),
        };
        _announcement.OnTextEntered += _ => Announce();
        announceRow.AddChild(_announcement);
        var send = _kit.Button(Loc.GetString("cmu-department-send"), CmuButtonStyles.Variant.Affirm);
        send.MinWidth = 80;
        send.OnPressed += _ => Announce();
        announceRow.AddChild(send);
        announce.AddChild(announceRow);

        // ── Orders page ──
        var orders = CrtConsoleKit.Column(8);
        orders.HorizontalExpand = true;
        orders.VerticalExpand = true;
        _ordersPage = orders;
        pages.AddChild(orders);

        orders.AddChild(_kit.Section(Loc.GetString("cmu-department-delivery"), out var delivery));
        var reasonRow = CrtConsoleKit.Row(4);
        reasonRow.AddChild(Key("cmu-department-reason"));
        _reason = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("department-console-reason-placeholder") };
        reasonRow.AddChild(_reason);
        delivery.AddChild(reasonRow);
        var deliverRow = CrtConsoleKit.Row(4);
        deliverRow.AddChild(Key("cmu-department-deliver-to"));
        _deliverTo = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("department-console-delivery-placeholder") };
        deliverRow.AddChild(_deliverTo);
        delivery.AddChild(deliverRow);

        var catalog = CrtConsoleKit.Row();
        catalog.VerticalExpand = true;
        orders.AddChild(catalog);

        var left = Panel();
        left.MinWidth = 200;
        left.MaxWidth = 200;
        var leftColumn = CrtConsoleKit.Column(6);
        leftColumn.AddChild(CrtConsoleKit.Text(Loc.GetString("cmu-department-categories"), _kit.Small, CrtTerminalPalette.TextDim));
        var categoryScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _categories = CrtConsoleKit.Column(3);
        _categories.HorizontalExpand = true;
        categoryScroll.AddChild(_categories);
        leftColumn.AddChild(categoryScroll);
        left.AddChild(leftColumn);
        catalog.AddChild(left);

        var right = Panel();
        right.HorizontalExpand = true;
        var rightColumn = CrtConsoleKit.Column(6);
        _categoryTitle = CrtConsoleKit.Text(string.Empty, _kit.Bold, CrtTerminalPalette.Accent);
        _categoryTitle.ClipText = true;
        rightColumn.AddChild(_categoryTitle);
        var itemScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _items = CrtConsoleKit.Column(3);
        _items.HorizontalExpand = true;
        _items.Margin = new Thickness(0, 0, 6, 0);
        itemScroll.AddChild(_items);
        rightColumn.AddChild(itemScroll);
        right.AddChild(rightColumn);
        catalog.AddChild(right);

        _kit.Root.AddChild(_kit.Footer(out _footer));
        Contents.AddChild(_kit.Tube);

        ShowPage(Page.Staff);
    }

    private Label Key(string text)
    {
        var key = CrtConsoleKit.Text(Loc.GetString(text), _kit.Small, CrtTerminalPalette.TextDim);
        key.MinWidth = 120;
        key.VerticalAlignment = VAlignment.Center;
        return key;
    }

    private PanelContainer Panel()
    {
        return new PanelContainer
        {
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = CrtTerminalPalette.Surface1,
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 8,
            },
        };
    }

    private void ShowPage(Page page)
    {
        _page = page;
        _staffPage.Visible = page == Page.Staff;
        _ordersPage.Visible = page == Page.Orders;
        foreach (var (p, button) in _pageButtons)
        {
            CmuButtonStyles.Apply(button, p == page ? CmuButtonStyles.Variant.Affirm : CmuButtonStyles.Variant.Neutral);
            button.Label.FontOverride = _kit.Bold;
        }

        _footer.Text = Loc.GetString(page == Page.Staff ? "cmu-department-footer-staff" : "cmu-department-footer-orders");
    }

    private void Withdraw()
    {
        if (float.TryParse(_withdraw.Text, out var amount) && amount > 0)
        {
            OnWithdraw?.Invoke(amount);
            _withdraw.Text = string.Empty;
        }
    }

    private void SetDefaultSalary()
    {
        if (int.TryParse(_defaultSalary.Text, out var salary))
            OnSetDefaultSalary?.Invoke(salary);
    }

    private void Announce()
    {
        if (string.IsNullOrWhiteSpace(_announcement.Text))
            return;

        OnAnnounce?.Invoke(_announcement.Text);
        _announcement.Text = string.Empty;
    }

    public void UpdateState(DepartmentConsoleBuiState state)
    {
        _state = state;
        _subheading.Text = Loc.GetString("cmu-department-subheading", ("department", state.DepartmentName.ToUpperInvariant()));
        CrtConsoleKit.SetTag(_budgetTag, _budgetText, CrtConsoleKit.Money(state.DepartmentBudget),
            state.DepartmentBudget > 0 ? CrtTerminalPalette.Accent : CrtTerminalPalette.Alert);

        if (!_defaultSalary.HasKeyboardFocus())
            _defaultSalary.Text = state.DefaultSalary.ToString();

        BuildStaff(state);
        BuildCatalog(state);
    }

    private void BuildStaff(DepartmentConsoleBuiState state)
    {
        _staff.RemoveAllChildren();
        _staffCount.Text = Loc.GetString("cmu-department-staff-count", ("count", state.Employees.Count));

        if (state.Employees.Count == 0)
        {
            _staff.AddChild(CrtConsoleKit.Text(Loc.GetString("department-console-no-employees"), _kit.Small, CrtTerminalPalette.TextDim));
            return;
        }

        foreach (var employee in state.Employees)
            _staff.AddChild(EmployeeCard(employee));
    }

    private Control EmployeeCard(DepartmentEmployeeEntry employee)
    {
        var id = employee.IdCardUid;
        var card = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 },
        };
        var row = CrtConsoleKit.Row();
        row.AddChild(new PanelContainer
        {
            MinWidth = 4,
            PanelOverride = new StyleBoxFlat { BackgroundColor = employee.HasOverride ? CrtTerminalPalette.Caution : CrtTerminalPalette.Accent.WithAlpha(0.45f) },
        });

        var column = CrtConsoleKit.Column(2);
        column.HorizontalExpand = true;
        column.Margin = new Thickness(0, 6);
        var name = CrtConsoleKit.Text(employee.Name.ToUpperInvariant(), _kit.Bold, CrtTerminalPalette.TextBright);
        name.ClipText = true;
        column.AddChild(name);
        var job = CrtConsoleKit.Text(employee.JobTitle.ToUpperInvariant(), _kit.Small, CrtTerminalPalette.Accent);
        job.ClipText = true;
        column.AddChild(job);
        var pay = CrtConsoleKit.Text(Loc.GetString(employee.HasOverride ? "cmu-department-pay-custom" : "cmu-department-pay",
            ("amount", CrtConsoleKit.Money(employee.Salary))), _kit.Small,
            employee.HasOverride ? CrtTerminalPalette.Caution : CrtTerminalPalette.TextDim);
        column.AddChild(pay);
        row.AddChild(column);

        var controls = CrtConsoleKit.Row(4);
        controls.VerticalAlignment = VAlignment.Center;
        controls.Margin = new Thickness(0, 0, 8, 0);
        var salary = new LineEdit
        {
            MinWidth = 70,
            PlaceHolder = Loc.GetString("department-console-salary-placeholder"),
            Text = _typedSalaries.GetValueOrDefault(id, string.Empty),
        };
        salary.OnTextChanged += args => _typedSalaries[id] = args.Text;
        void SetSalary()
        {
            if (!int.TryParse(salary.Text, out var amount))
                return;

            _typedSalaries.Remove(id);
            OnSetIndividualSalaryPressed?.Invoke(id, amount);
        }

        salary.OnTextEntered += _ => SetSalary();
        controls.AddChild(salary);
        var set = _kit.Button(Loc.GetString("cmu-department-set"));
        set.OnPressed += _ => SetSalary();
        controls.AddChild(set);

        if (employee.HasOverride)
        {
            var reset = _kit.Button(Loc.GetString("cmu-department-reset"), CmuButtonStyles.Variant.Caution);
            reset.ToolTip = Loc.GetString("cmu-department-reset-tooltip");
            reset.OnPressed += _ => OnRemoveOverridePressed?.Invoke(id);
            controls.AddChild(reset);
        }

        var fire = new ConfirmButton
        {
            Text = Loc.GetString("cmu-department-fire"),
            ConfirmationText = Loc.GetString("cmu-department-fire-confirm"),
            MinWidth = 70,
        };
        CmuButtonStyles.Apply(fire, CmuButtonStyles.Variant.Danger);
        fire.Label.FontOverride = _kit.Small;
        fire.OnPressed += _ => OnFirePressed?.Invoke(id);
        controls.AddChild(fire);
        row.AddChild(controls);

        card.AddChild(row);
        return card;
    }

    private void BuildCatalog(DepartmentConsoleBuiState state)
    {
        _categories.RemoveAllChildren();
        if (state.Catalog.Count == 0)
        {
            _categories.AddChild(CrtConsoleKit.Text(Loc.GetString("department-console-no-catalog"), _kit.Small, CrtTerminalPalette.TextDim));
            _category = -1;
        }

        if (_category >= state.Catalog.Count)
            _category = -1;
        if (_category < 0 && state.Catalog.Count > 0)
            _category = 0;

        for (var i = 0; i < state.Catalog.Count; i++)
        {
            var index = i;
            var selected = i == _category;
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
            var label = CrtConsoleKit.Text(state.Catalog[i].Name.ToUpperInvariant(), _kit.Bold,
                selected ? CrtTerminalPalette.TextBright : CrtTerminalPalette.Text);
            label.ClipText = true;
            label.HorizontalExpand = true;
            label.VerticalAlignment = VAlignment.Center;
            row.AddChild(label);
            panel.AddChild(row);
            button.AddChild(panel);
            if (!selected)
            {
                button.OnMouseEntered += _ => panel.PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface3 };
                button.OnMouseExited += _ => panel.PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 };
            }

            button.OnPressed += _ =>
            {
                _category = index;
                if (_state != null)
                    BuildCatalog(_state);
            };
            _categories.AddChild(button);
        }

        _items.RemoveAllChildren();
        if (_category < 0)
        {
            _categoryTitle.Text = Loc.GetString("cmu-department-pick-category");
            return;
        }

        var category = state.Catalog[_category];
        _categoryTitle.Text = category.Name.ToUpperInvariant();
        if (category.Entries.Count == 0)
        {
            _items.AddChild(CrtConsoleKit.Text(Loc.GetString("department-console-no-items"), _kit.Small, CrtTerminalPalette.TextDim));
            return;
        }

        foreach (var entry in category.Entries)
            _items.AddChild(ItemRow(entry, state.DepartmentBudget));
    }

    private Control ItemRow(DepartmentOrderCatalogEntry entry, float budget)
    {
        var affordable = entry.Cost <= budget;
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
        var column = CrtConsoleKit.Column(0);
        column.HorizontalExpand = true;
        column.AddChild(CrtConsoleKit.Wrapping(entry.Name, CrtTerminalPalette.TextBright));
        column.AddChild(CrtConsoleKit.Text(CrtConsoleKit.Money(entry.Cost), _kit.Small,
            affordable ? CrtTerminalPalette.Caution : CrtTerminalPalette.Alert));
        row.AddChild(column);

        var order = _kit.Button(Loc.GetString("cmu-department-order"), CmuButtonStyles.Variant.Affirm);
        order.MinWidth = 80;
        order.VerticalAlignment = VAlignment.Center;
        order.Disabled = !affordable;
        order.ToolTip = affordable ? null : Loc.GetString("cmu-department-cant-afford");
        var catIdx = entry.CategoryIndex;
        var entIdx = entry.EntryIndex;
        order.OnPressed += _ => OnOrderPressed?.Invoke(catIdx, entIdx, _reason.Text, _deliverTo.Text);
        row.AddChild(order);
        panel.AddChild(row);
        return panel;
    }
}
