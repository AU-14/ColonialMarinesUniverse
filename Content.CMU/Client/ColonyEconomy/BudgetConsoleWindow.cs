using System.Linq;
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
///     The colony budget console, in the CRT theme: the colony's money, payday, and transfers into each department.
/// </summary>
public sealed class BudgetConsoleWindow : DefaultWindow
{
    private static readonly int[] TransferPresets = [100, 250, 500, 1000];

    public event Action<int>? OnWithdraw;
    public event Action? OnDispenseSalaries;
    public event Action<NetEntity, float>? OnTransfer;

    private readonly CrtConsoleKit _kit = new();

    private readonly PanelContainer _budgetTag;
    private readonly Label _budgetText;
    private readonly Label _held;
    private readonly Label _count;
    private readonly LineEdit _amount;
    private readonly BoxContainer _departments;

    private BudgetConsoleBuiState? _state;

    public BudgetConsoleWindow()
    {
        Title = Loc.GetString("budget-console-title");
        MinSize = new Vector2(540, 520);
        SetSize = new Vector2(580, 620);

        _budgetTag = _kit.Tag(out _budgetText);
        _kit.Root.AddChild(_kit.Header(Loc.GetString("cmu-budget-heading"), out var subheading, _budgetTag));
        subheading.Text = Loc.GetString("cmu-budget-subheading");

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        var body = CrtConsoleKit.Column(8);
        body.HorizontalExpand = true;
        body.Margin = new Thickness(0, 0, 6, 0);
        scroll.AddChild(body);
        _kit.Root.AddChild(scroll);

        // Treasury.
        body.AddChild(_kit.Section(Loc.GetString("cmu-budget-treasury"), out var treasury));
        var withdraw = CrtConsoleKit.Row(4);
        foreach (var amount in new[] { 100, 250, 500 })
        {
            var button = _kit.Button(Loc.GetString("cmu-ambassador-withdraw", ("amount", amount)));
            button.HorizontalExpand = true;
            button.OnPressed += _ => OnWithdraw?.Invoke(amount);
            withdraw.AddChild(button);
        }

        treasury.AddChild(withdraw);

        // Payroll.
        var dispense = new ConfirmButton
        {
            Text = Loc.GetString("cmu-budget-dispense"),
            ConfirmationText = Loc.GetString("cmu-budget-dispense-confirm"),
            MinWidth = 200,
        };
        CmuButtonStyles.Apply(dispense, CmuButtonStyles.Variant.Affirm);
        dispense.Label.FontOverride = _kit.Small;
        dispense.OnPressed += _ => OnDispenseSalaries?.Invoke();
        body.AddChild(_kit.Section(Loc.GetString("cmu-budget-payroll"), out var payroll, right: dispense));
        payroll.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("cmu-budget-payroll-hint"), CrtTerminalPalette.TextDim));

        // Departments: one amount, sent to whichever department you pick.
        _count = CrtConsoleKit.Text(string.Empty, _kit.Small, CrtTerminalPalette.TextDim);
        body.AddChild(_kit.Section(Loc.GetString("cmu-budget-departments"), out var departments, right: _count));
        var amountRow = CrtConsoleKit.Row(4);
        var key = CrtConsoleKit.Text(Loc.GetString("cmu-budget-transfer-amount"), _kit.Small, CrtTerminalPalette.TextDim);
        key.VerticalAlignment = VAlignment.Center;
        key.MinWidth = 130;
        amountRow.AddChild(key);
        _amount = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("budget-console-amount-placeholder") };
        _amount.OnTextChanged += _ => Rebuild();
        amountRow.AddChild(_amount);
        departments.AddChild(amountRow);

        var presets = new GridContainer { Columns = TransferPresets.Length, HSeparationOverride = 4, HorizontalExpand = true };
        foreach (var preset in TransferPresets)
        {
            var button = _kit.Button(CrtConsoleKit.Money(preset));
            button.HorizontalExpand = true;
            button.OnPressed += _ =>
            {
                _amount.Text = preset.ToString();
                Rebuild();
            };
            presets.AddChild(button);
        }

        departments.AddChild(presets);
        _departments = CrtConsoleKit.Column(4);
        departments.AddChild(_departments);
        _held = CrtConsoleKit.Text(string.Empty, _kit.Small, CrtTerminalPalette.TextDim);
        departments.AddChild(_held);

        _kit.Root.AddChild(_kit.Footer(out var footer));
        footer.Text = Loc.GetString("cmu-budget-footer");

        Contents.AddChild(_kit.Tube);
    }

    public void UpdateState(BudgetConsoleBuiState state)
    {
        _state = state;
        CrtConsoleKit.SetTag(_budgetTag, _budgetText, CrtConsoleKit.Money(state.Budget),
            state.Budget > 0 ? CrtTerminalPalette.Accent : CrtTerminalPalette.Alert);
        _count.Text = Loc.GetString("cmu-budget-department-count", ("count", state.Departments.Count));
        _held.Text = Loc.GetString("cmu-budget-total") + ": " + CrtConsoleKit.Money(state.Departments.Sum(d => d.Budget));
        Rebuild();
    }

    /// <summary>Redraws the department cards, so their transfer buttons match the amount typed in.</summary>
    private void Rebuild()
    {
        _departments.RemoveAllChildren();
        if (_state is not { } state)
            return;

        if (state.Departments.Count == 0)
        {
            _departments.AddChild(CrtConsoleKit.Text(Loc.GetString("budget-console-no-departments"), _kit.Small, CrtTerminalPalette.TextDim));
            return;
        }

        var valid = float.TryParse(_amount.Text, out var amount) && amount > 0;
        foreach (var department in state.Departments.OrderBy(d => d.Name))
        {
            var funded = department.Budget > 0;
            var card = new PanelContainer
            {
                HorizontalExpand = true,
                PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 },
            };
            var row = CrtConsoleKit.Row();
            row.AddChild(new PanelContainer
            {
                MinWidth = 4,
                PanelOverride = new StyleBoxFlat { BackgroundColor = funded ? CrtTerminalPalette.Accent : CrtTerminalPalette.Alert },
            });
            var column = CrtConsoleKit.Column(0);
            column.HorizontalExpand = true;
            column.Margin = new Thickness(0, 6);
            var name = CrtConsoleKit.Text(department.Name.ToUpperInvariant(), _kit.Bold, CrtTerminalPalette.TextBright);
            name.ClipText = true;
            column.AddChild(name);
            column.AddChild(CrtConsoleKit.Text(
                Loc.GetString("cmu-budget-department-budget", ("amount", CrtConsoleKit.Money(department.Budget))),
                _kit.Small, funded ? CrtTerminalPalette.Accent : CrtTerminalPalette.Alert));
            row.AddChild(column);

            var transfer = _kit.Button(valid
                ? $"{Loc.GetString("cmu-budget-transfer")} {CrtConsoleKit.Money(amount)}"
                : Loc.GetString("cmu-budget-transfer"), CmuButtonStyles.Variant.Affirm);
            transfer.MinWidth = 150;
            transfer.VerticalAlignment = VAlignment.Center;
            transfer.Margin = new Thickness(0, 0, 8, 0);
            transfer.Disabled = !valid || amount > state.Budget;
            var uid = department.Uid;
            transfer.OnPressed += _ =>
            {
                if (float.TryParse(_amount.Text, out var sent) && sent > 0)
                    OnTransfer?.Invoke(uid, sent);
            };
            row.AddChild(transfer);
            card.AddChild(row);
            _departments.AddChild(card);
        }
    }
}
