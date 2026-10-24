using System.Numerics;
using Content.Client.CMU14.Telephone;
using Content.Shared._RMC14.Holiday;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Kitchen;
using Content.Shared.Nutrition.Components;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using static Content.Client.CMU14.Vendors.CMUNutriCoStyle;

namespace Content.Client.CMU14.Vendors;

/// <summary>
///     The NutriCo AutoChef's front panel. It vends from the same automated vendor stock as any food vendor, shown as
///     the machine's own menu display: numbered slots, a picture of each meal, what's left, and a dispense key.
/// </summary>
public sealed class CMUNutriCoFoodBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUNutriCoFoodWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUNutriCoFoodWindow>();
        _window.OnVend += (section, entry) => SendPredictedMessage(new CMVendorVendBuiMsg(section, entry, new List<int>()));
        Refresh();
    }

    /// <summary>Called when the vendor's stock changes, or the foodline opens or closes.</summary>
    public void Refresh()
    {
        if (_window == null)
            return;

        EntMan.TryGetComponent(Owner, out CMUFoodlineVendorComponent? foodline);
        _window.SetFoodline(foodline is { FoodlineOpen: true } ? Loc.GetString(foodline.Message) : null);
        if (EntMan.TryGetComponent(Owner, out CMAutomatedVendorComponent? vendor))
            _window.UpdateStock(vendor);
    }
}

public sealed class CMUNutriCoFoodWindow : DefaultWindow
{
    public event Action<int, int>? OnVend;

    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly SharedRMCHolidaySystem _holidays;
    private readonly Font _body;
    private readonly Font _bold;
    private readonly Font _small;
    private readonly Font _code;

    private readonly LineEdit _search;
    private readonly BoxContainer _menu;
    private readonly Label _readout;
    private readonly Label _foodline;
    private readonly List<PanelContainer> _leds;
    private bool _foodlineOpen;

    private CMAutomatedVendorComponent? _vendor;

    public CMUNutriCoFoodWindow()
    {
        IoCManager.InjectDependencies(this);
        _holidays = _entities.System<SharedRMCHolidaySystem>();
        _body = Font(CMUPhoneStyle.Sans, 12);
        _bold = Font(CMUPhoneStyle.SansBold, 12);
        _small = Font(CMUPhoneStyle.Sans, 10);
        _code = Font(CMUPhoneStyle.MonoBold, 12);

        Title = Loc.GetString("cmu-autochef-title");
        MinSize = new Vector2(560, 600);
        SetSize = new Vector2(600, 680);

        var cabinet = CMUPhoneStyle.Panel(Steel, Trim, 3, 12, 12);
        cabinet.HorizontalExpand = true;
        cabinet.VerticalExpand = true;
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 10 };
        cabinet.AddChild(column);
        column.AddChild(Header("AutoChef", 5, out _leds));

        var display = CMUPhoneStyle.Panel(Display, TrimDark, 3, 10, 8);
        display.VerticalExpand = true;
        var displayColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        var title = CMUPhoneStyle.Text(Loc.GetString("cmu-autochef-menu"), _bold, Phosphor);
        title.VerticalAlignment = VAlignment.Center;
        top.AddChild(title);
        _search = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("cmu-autochef-search") };
        _search.OnTextChanged += _ => Rebuild();
        top.AddChild(_search);
        displayColumn.AddChild(top);
        _foodline = CMUPhoneStyle.Text(string.Empty, _bold, Warning);
        _foodline.HorizontalAlignment = HAlignment.Center;
        _foodline.Visible = false;
        displayColumn.AddChild(_foodline);
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _menu = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 3, HorizontalExpand = true };
        scroll.AddChild(_menu);
        displayColumn.AddChild(scroll);
        display.AddChild(displayColumn);
        column.AddChild(display);

        // The little status readout under the display.
        var status = CMUPhoneStyle.Panel(Trim, TrimDark, 1, 8, 3);
        _readout = CMUPhoneStyle.Text(string.Empty, _code, Led);
        _readout.ClipText = true;
        status.AddChild(_readout);
        column.AddChild(status);

        column.AddChild(Footer());
        Contents.AddChild(cabinet);
    }

    /// <summary>Shows why it won't dispense while the DEFAC foodline is open, or null once it's closed again.</summary>
    public void SetFoodline(string? message)
    {
        _foodlineOpen = message != null;
        _foodline.Text = message?.ToUpperInvariant() ?? string.Empty;
        _foodline.Visible = _foodlineOpen;
    }

    public void UpdateStock(CMAutomatedVendorComponent vendor)
    {
        _vendor = vendor;
        Rebuild();
    }

    private void Rebuild()
    {
        _menu.RemoveAllChildren();
        if (_vendor is not { } vendor)
            return;

        var filter = _search.Text.Trim();
        var total = 0;
        var shown = 0;
        for (var s = 0; s < vendor.Sections.Count; s++)
        {
            var section = vendor.Sections[s];
            if (!SectionActive(section))
                continue;

            var letter = (char) ('A' + Math.Min(s, 25));
            var header = CMUPhoneStyle.Text(section.Name.ToUpperInvariant(), _bold, Orange);
            header.Margin = new Thickness(0, 6, 0, 2);
            var any = false;

            for (var e = 0; e < section.Entries.Count; e++)
            {
                var entry = section.Entries[e];
                _prototypes.TryIndex(entry.Id, out var proto);
                var name = entry.Name?.Replace("\\n", " ") ?? proto?.Name ?? entry.Id.Id;
                var left = entry.Amount;
                // Servings are meals; forks, spoons and the like don't count.
                if (left is { } stock && !IsUtensil(section, proto))
                    total += stock;

                if (filter.Length > 0 && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!any)
                {
                    _menu.AddChild(header);
                    any = true;
                }

                shown++;
                _menu.AddChild(Row($"{letter}{e + 1}", name, entry.Id, left, s, e));
            }
        }

        if (shown == 0)
        {
            var none = CMUPhoneStyle.Text(Loc.GetString("cmu-autochef-nothing"), _body, PhosphorDim);
            none.HorizontalAlignment = HAlignment.Center;
            none.Margin = new Thickness(0, 20, 0, 0);
            _menu.AddChild(none);
        }

        _readout.Text = Loc.GetString("cmu-autochef-readout", ("count", total));
        SetLed(_leds[0], true);
        SetLed(_leds[1], true);
        SetLed(_leds[2], total > 0);
        SetLed(_leds[3], true);
        SetLed(_leds[4], total > 0);
    }

    private Control Row(string code, string name, EntProtoId id, int? left, int section, int entry)
    {
        var soldOut = left is <= 0;
        var panel = CMUPhoneStyle.Panel(DisplayRow, Trim, 1, 6, 3);
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };

        var slot = CMUPhoneStyle.Text(code, _code, soldOut ? PhosphorDim : Orange);
        slot.MinWidth = 34;
        slot.VerticalAlignment = VAlignment.Center;
        row.AddChild(slot);

        var picture = new EntityPrototypeView { SetSize = new Vector2(32, 32), Scale = new Vector2(1, 1), VerticalAlignment = VAlignment.Center };
        picture.SetPrototype(id);
        if (soldOut)
            picture.Modulate = Color.White.WithAlpha(0.35f);
        row.AddChild(picture);

        var label = CMUPhoneStyle.Text(name.ToUpperInvariant(), _bold, soldOut ? PhosphorDim : Phosphor);
        label.HorizontalExpand = true;
        label.ClipText = true;
        label.VerticalAlignment = VAlignment.Center;
        label.ToolTip = name;
        row.AddChild(label);

        var stock = CMUPhoneStyle.Text(left switch
        {
            null => Loc.GetString("cmu-autochef-unlimited"),
            <= 0 => Loc.GetString("cmu-autochef-sold-out"),
            _ => Loc.GetString("cmu-autochef-left", ("count", left.Value)),
        }, _small, soldOut ? Warning : PhosphorDim);
        stock.VerticalAlignment = VAlignment.Center;
        stock.MinWidth = 64;
        row.AddChild(stock);

        var dispense = Key(Loc.GetString("cmu-autochef-dispense"), 90);
        dispense.Disabled = soldOut || _foodlineOpen;
        dispense.VerticalAlignment = VAlignment.Center;
        dispense.OnPressed += _ => OnVend?.Invoke(section, entry);
        row.AddChild(dispense);

        panel.AddChild(row);
        return panel;
    }

    /// <summary>Forks, spoons, knives and chopsticks, which aren't servings.</summary>
    private bool IsUtensil(CMVendorSection section, EntityPrototype? proto)
    {
        if (section.Name.Equals("Utensils", StringComparison.OrdinalIgnoreCase))
            return true;

        return proto != null &&
               proto.Components.ContainsKey(_entities.ComponentFactory.GetComponentName<UtensilComponent>());
    }

    private bool SectionActive(CMVendorSection section)
    {
        if (section.Holidays.Count == 0)
            return true;

        foreach (var holiday in section.Holidays)
        {
            if (_holidays.IsActiveHoliday(holiday))
                return true;
        }

        return false;
    }
}
