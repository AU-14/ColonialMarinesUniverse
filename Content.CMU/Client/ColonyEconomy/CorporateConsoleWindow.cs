using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.ColonyEconomy;

/// <summary>
///     The corporate affairs console, in the CRT theme: the corporate budget, the transit tariff it skims off the
///     colony's submissions, the economy at a glance and the corporate support menu.
/// </summary>
public sealed class CorporateConsoleWindow : DefaultWindow
{
    /// <summary>The highest tariff the console accepts.</summary>
    private const int MaxTariff = 50;

    private static readonly int[] TariffPresets = [0, 10, 20, 30, 40, 50];

    public event Action<int>? OnWithdraw;
    public event Action<float>? OnSetTariff;
    public event Action? OnOpenThirdParty;

    private readonly CrtConsoleKit _kit = new();

    private readonly Label _subheading;
    private readonly PanelContainer _budgetTag;
    private readonly Label _budgetText;
    private readonly Label _taxNote;
    private readonly Label _tariff;
    private readonly LineEdit _tariffInput;
    private readonly BoxContainer _economy;

    public CorporateConsoleWindow()
    {
        Title = Loc.GetString("corporate-console-title");
        MinSize = new Vector2(560, 540);
        SetSize = new Vector2(600, 660);

        _budgetTag = _kit.Tag(out _budgetText);
        _kit.Root.AddChild(_kit.Header(Loc.GetString("cmu-corporate-heading"), out _subheading, _budgetTag));
        _subheading.Text = Loc.GetString("cmu-corporate-subheading");

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        var body = CrtConsoleKit.Column(8);
        body.HorizontalExpand = true;
        body.Margin = new Thickness(0, 0, 6, 0);
        scroll.AddChild(body);
        _kit.Root.AddChild(scroll);

        // Treasury.
        body.AddChild(_kit.Section(Loc.GetString("cmu-corporate-treasury"), out var treasury));
        var withdraw = CrtConsoleKit.Row(4);
        foreach (var amount in new[] { 100, 250, 500 })
        {
            var button = _kit.Button(Loc.GetString("cmu-ambassador-withdraw", ("amount", amount)));
            button.HorizontalExpand = true;
            button.OnPressed += _ => OnWithdraw?.Invoke(amount);
            withdraw.AddChild(button);
        }

        treasury.AddChild(withdraw);
        _taxNote = CrtConsoleKit.Text(string.Empty, _kit.Small, CrtTerminalPalette.TextDim);
        treasury.AddChild(_taxNote);

        // Transit tariff: the current rate big, then presets and an exact figure.
        body.AddChild(_kit.Section(Loc.GetString("cmu-corporate-tariff"), out var tariff));
        var tariffRow = CrtConsoleKit.Row();
        var current = _kit.Readout(Loc.GetString("cmu-corporate-current-tariff"), string.Empty, out _tariff);
        current.HorizontalExpand = false;
        current.MinWidth = 140;
        _tariff.FontOverride = _kit.Huge;
        tariffRow.AddChild(current);
        tariffRow.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("corporate-console-transit-tariff-description"), CrtTerminalPalette.TextDim));
        tariff.AddChild(tariffRow);

        var presets = new GridContainer { Columns = TariffPresets.Length, HSeparationOverride = 4, HorizontalExpand = true };
        foreach (var preset in TariffPresets)
        {
            var button = _kit.Button($"{preset}%");
            button.HorizontalExpand = true;
            button.OnPressed += _ => OnSetTariff?.Invoke(preset);
            presets.AddChild(button);
        }

        tariff.AddChild(presets);

        var exact = CrtConsoleKit.Row(4);
        var key = CrtConsoleKit.Text(Loc.GetString("cmu-corporate-exact", ("max", MaxTariff)), _kit.Small, CrtTerminalPalette.TextDim);
        key.VerticalAlignment = VAlignment.Center;
        key.MinWidth = 150;
        exact.AddChild(key);
        _tariffInput = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("corporate-console-tariff-placeholder") };
        _tariffInput.OnTextEntered += _ => ApplyTariff();
        exact.AddChild(_tariffInput);
        var apply = _kit.Button(Loc.GetString("cmu-corporate-apply"), CmuButtonStyles.Variant.Caution);
        apply.MinWidth = 90;
        apply.OnPressed += _ => ApplyTariff();
        exact.AddChild(apply);
        tariff.AddChild(exact);
        tariff.AddChild(CrtConsoleKit.Text(Loc.GetString("cmu-corporate-tariff-announced"), _kit.Small, CrtTerminalPalette.Caution));

        // Economy at a glance.
        body.AddChild(_kit.Section(Loc.GetString("cmu-ambassador-economy"), out _economy));

        // Corporate support.
        var open = _kit.Button(Loc.GetString("cmu-ambassador-open-third-party"), CmuButtonStyles.Variant.Affirm);
        open.OnPressed += _ => OnOpenThirdParty?.Invoke();
        body.AddChild(_kit.Section(Loc.GetString("cmu-corporate-support"), out var support, right: open));
        support.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("cmu-corporate-support-hint"), CrtTerminalPalette.TextDim));

        _kit.Root.AddChild(_kit.Footer(out var footer));
        footer.Text = Loc.GetString("cmu-corporate-footer");

        Contents.AddChild(_kit.Tube);
    }

    private void ApplyTariff()
    {
        if (float.TryParse(_tariffInput.Text, out var percent))
            OnSetTariff?.Invoke(Math.Clamp(percent, 0, MaxTariff));
    }

    public void UpdateState(CorporateConsoleBuiState state)
    {
        CrtConsoleKit.SetTag(_budgetTag, _budgetText, CrtConsoleKit.Money(state.CorporateBudget),
            state.CorporateBudget > 0 ? CrtTerminalPalette.Accent : CrtTerminalPalette.Alert);

        _tariff.Text = $"{MathF.Round(state.TransitTariffPercent):0}%";
        if (!_tariffInput.HasKeyboardFocus())
            _tariffInput.Text = $"{MathF.Round(state.TransitTariffPercent):0}";

        var incomeTax = state.EconomyStatus.IncomeTaxPercent;
        _taxNote.Text = incomeTax > 0
            ? Loc.GetString("cmu-corporate-withdraw-tax", ("percent", $"{MathF.Round(incomeTax):0}"))
            : Loc.GetString("cmu-corporate-withdraw-no-tax");

        _kit.EconomyOverview(_economy, state.EconomyStatus);
    }
}
