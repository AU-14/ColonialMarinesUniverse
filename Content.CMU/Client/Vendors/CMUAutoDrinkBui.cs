using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Telephone;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Vendors;
using Content.Shared.FixedPoint;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using static Content.Client.CMU14.Vendors.CMUNutriCoStyle;

namespace Content.Client.CMU14.Vendors;

public sealed class CMUAutoDrinkBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUAutoDrinkWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUAutoDrinkWindow>();
        if (EntMan.TryGetComponent(Owner, out CMUAutoDrinkComponent? drink))
            _window.Setup(drink);

        _window.OnPour += (drink, amount) => SendMessage(new CMUAutoDrinkPourBuiMsg(drink, amount));
        _window.OnCup += () => SendMessage(new CMUAutoDrinkCupBuiMsg());
        _window.OnEject += () => SendMessage(new CMUAutoDrinkEjectBuiMsg());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is CMUAutoDrinkBuiState drink)
            _window?.UpdateState(drink);
    }
}

/// <summary>
///     The NutriCo AutoDrink's front panel: the drink menu and pour size on the left, the dispensing bay on the right.
/// </summary>
public sealed class CMUAutoDrinkWindow : DefaultWindow
{
    public event Action<ProtoId<ReagentPrototype>, FixedPoint2>? OnPour;
    public event Action? OnCup;
    public event Action? OnEject;

    [Dependency] private IEntityManager _entities = default!;

    private readonly Font _body;
    private readonly Font _bold;
    private readonly Font _small;

    private readonly GridContainer _drinks;
    private readonly BoxContainer _extras;
    private readonly Control _extrasSection;
    private readonly BoxContainer _sizes;
    private readonly Label _bayName;
    private readonly Label _bayVolume;
    private readonly PanelContainer _fill;
    private readonly Control _fillTrack;
    private readonly BoxContainer _contents;
    private readonly Button _cup;
    private readonly Button _eject;
    private readonly List<PanelContainer> _leds;

    private CMUAutoDrinkComponent? _machine;
    private FixedPoint2 _amount;
    private CMUAutoDrinkBuiState? _state;

    public CMUAutoDrinkWindow()
    {
        IoCManager.InjectDependencies(this);
        _body = Font(CMUPhoneStyle.Sans, 12);
        _bold = Font(CMUPhoneStyle.SansBold, 12);
        _small = Font(CMUPhoneStyle.Sans, 10);

        Title = Loc.GetString("cmu-autodrink-title");
        MinSize = new Vector2(680, 560);
        SetSize = new Vector2(720, 600);

        var cabinet = CMUPhoneStyle.Panel(Steel, Trim, 3, 12, 12);
        cabinet.HorizontalExpand = true;
        cabinet.VerticalExpand = true;
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 10 };
        cabinet.AddChild(column);
        column.AddChild(Header("AutoDrink", 4, out _leds));

        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10, VerticalExpand = true };
        column.AddChild(row);

        // Menu display.
        var display = CMUPhoneStyle.Panel(Display, TrimDark, 3, 10, 8);
        display.HorizontalExpand = true;
        display.VerticalExpand = true;
        var menu = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        menu.AddChild(CMUPhoneStyle.Text(Loc.GetString("cmu-autodrink-select"), _bold, Phosphor));
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        var menuBody = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6, HorizontalExpand = true };
        _drinks = new GridContainer { Columns = 2, HSeparationOverride = 4, VSeparationOverride = 4, HorizontalExpand = true };
        menuBody.AddChild(_drinks);
        var extras = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        extras.AddChild(CMUPhoneStyle.Text(Loc.GetString("cmu-autodrink-extras"), _bold, Phosphor));
        _extras = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        extras.AddChild(_extras);
        _extrasSection = extras;
        menuBody.AddChild(extras);
        scroll.AddChild(menuBody);
        menu.AddChild(scroll);

        var sizeRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        var sizeLabel = CMUPhoneStyle.Text(Loc.GetString("cmu-autodrink-pour"), _bold, Phosphor);
        sizeLabel.VerticalAlignment = VAlignment.Center;
        sizeLabel.MinWidth = 90;
        sizeRow.AddChild(sizeLabel);
        _sizes = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4, HorizontalExpand = true };
        sizeRow.AddChild(_sizes);
        menu.AddChild(sizeRow);
        display.AddChild(menu);
        row.AddChild(display);

        // Dispensing bay.
        var bay = CMUPhoneStyle.Panel(Trim, TrimDark, 2, 10, 8);
        bay.MinWidth = 230;
        bay.MaxWidth = 230;
        bay.VerticalExpand = true;
        var bayColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        bayColumn.AddChild(CMUPhoneStyle.Text(Loc.GetString("cmu-autodrink-bay"), _bold, White));
        var recess = CMUPhoneStyle.Panel(TrimDark, Color.Black, 2, 8, 8);
        var recessColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        _bayName = CMUPhoneStyle.Text(string.Empty, _bold, Phosphor);
        _bayName.ClipText = true;
        recessColumn.AddChild(_bayName);
        _fillTrack = new Control { MinHeight = 10, HorizontalExpand = true };
        _fillTrack.AddChild(new PanelContainer { HorizontalExpand = true, VerticalExpand = true, PanelOverride = CMUPhoneStyle.Box(Display, Trim, 1) });
        _fill = new PanelContainer { HorizontalAlignment = HAlignment.Left, VerticalExpand = true, PanelOverride = CMUPhoneStyle.Box(Orange) };
        _fillTrack.AddChild(_fill);
        recessColumn.AddChild(_fillTrack);
        _bayVolume = CMUPhoneStyle.Text(string.Empty, _small, PhosphorDim);
        recessColumn.AddChild(_bayVolume);
        _contents = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        recessColumn.AddChild(_contents);
        recess.AddChild(recessColumn);
        bayColumn.AddChild(recess);
        bayColumn.AddChild(new Control { VerticalExpand = true });
        _cup = Key(Loc.GetString("cmu-autodrink-cup"));
        _cup.OnPressed += _ => OnCup?.Invoke();
        bayColumn.AddChild(_cup);
        _eject = Key(Loc.GetString("cmu-autodrink-eject"));
        _eject.OnPressed += _ => OnEject?.Invoke();
        bayColumn.AddChild(_eject);
        bay.AddChild(bayColumn);
        row.AddChild(bay);

        column.AddChild(Footer());
        Contents.AddChild(cabinet);
    }

    public void Setup(CMUAutoDrinkComponent machine)
    {
        _machine = machine;
        _amount = machine.PourAmounts.Count > 1 ? machine.PourAmounts[1] : machine.PourAmounts.FirstOrDefault();
        BuildMenu();
        BuildSizes();
    }

    private void BuildMenu()
    {
        _drinks.RemoveAllChildren();
        _extras.RemoveAllChildren();
        if (_machine == null)
            return;

        var hasExtras = false;
        for (var i = 0; i < _machine.Drinks.Count; i++)
        {
            var drink = _machine.Drinks[i];
            _entities.System<RMCReagentSystem>().TryIndex(drink.Reagent, out var reagent);
            var name = drink.Name is { } loc ? Loc.GetString(loc) : reagent?.LocalizedName ?? drink.Reagent.Id;
            var color = reagent?.SubstanceColor ?? Color.White;

            var button = new ContainerButton { HorizontalExpand = true, MinHeight = drink.Extra ? 30 : 40 };
            var panel = CMUPhoneStyle.Panel(DisplayRow, Trim, 1, 6, 4);
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
            row.AddChild(new PanelContainer
            {
                MinSize = new Vector2(12, 12),
                VerticalAlignment = VAlignment.Center,
                PanelOverride = CMUPhoneStyle.Box(color.WithAlpha(1f), Trim, 1),
            });
            var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
            var title = CMUPhoneStyle.Text(name.ToUpperInvariant(), _bold, Phosphor);
            title.ClipText = true;
            text.AddChild(title);
            if (drink.Note is { } note)
            {
                var noteLabel = CMUPhoneStyle.Text(Loc.GetString(note), _small, PhosphorDim);
                noteLabel.ClipText = true;
                text.AddChild(noteLabel);
            }

            row.AddChild(text);
            panel.AddChild(row);
            button.AddChild(panel);
            button.OnMouseEntered += _ => panel.PanelOverride = CMUPhoneStyle.Box(KeyFaceLit, Orange, 1, 6, 4);
            button.OnMouseExited += _ => panel.PanelOverride = CMUPhoneStyle.Box(DisplayRow, Trim, 1, 6, 4);
            button.OnPressed += _ => OnPour?.Invoke(drink.Reagent, _amount);

            if (drink.Extra)
            {
                hasExtras = true;
                button.MinWidth = 160;
                button.HorizontalExpand = false;
                _extras.AddChild(button);
            }
            else
            {
                _drinks.AddChild(button);
            }
        }

        _extrasSection.Visible = hasExtras;
    }

    private void BuildSizes()
    {
        _sizes.RemoveAllChildren();
        if (_machine == null)
            return;

        foreach (var amount in _machine.PourAmounts)
        {
            var selected = amount == _amount;
            var key = Key(Loc.GetString("cmu-autodrink-units", ("amount", amount)), 0, selected);
            key.HorizontalExpand = true;
            var captured = amount;
            key.OnPressed += _ =>
            {
                _amount = captured;
                BuildSizes();
            };
            _sizes.AddChild(key);
        }
    }

    public void UpdateState(CMUAutoDrinkBuiState state)
    {
        _state = state;
        var loaded = state.Container != null;
        _bayName.Text = loaded ? state.Container!.ToUpperInvariant() : Loc.GetString("cmu-autodrink-bay-empty");
        _bayName.FontColorOverride = loaded ? Phosphor : PhosphorDim;
        _bayVolume.Text = loaded ? Loc.GetString("cmu-autodrink-volume", ("volume", state.Volume), ("max", state.MaxVolume)) : string.Empty;

        var fraction = loaded && state.MaxVolume > 0 ? Math.Clamp(state.Volume.Float() / state.MaxVolume.Float(), 0f, 1f) : 0f;
        _fill.SetWidth = Math.Max(0, (_fillTrack.Width > 0 ? _fillTrack.Width : 190) * fraction);
        _fill.Visible = fraction > 0;
        var top = state.Contents.FirstOrDefault();
        _fill.PanelOverride = CMUPhoneStyle.Box(state.Contents.Count > 0 ? top.Color.WithAlpha(1f) : Orange);

        _contents.RemoveAllChildren();
        foreach (var content in state.Contents)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
            row.AddChild(new PanelContainer { MinSize = new Vector2(8, 8), VerticalAlignment = VAlignment.Center, PanelOverride = CMUPhoneStyle.Box(content.Color.WithAlpha(1f)) });
            var name = CMUPhoneStyle.Text(content.Name, _small, Phosphor);
            name.HorizontalExpand = true;
            name.ClipText = true;
            row.AddChild(name);
            row.AddChild(CMUPhoneStyle.Text(Loc.GetString("cmu-autodrink-units", ("amount", content.Quantity)), _small, PhosphorDim));
            _contents.AddChild(row);
        }

        _cup.Disabled = loaded;
        _eject.Disabled = !loaded;

        // Power, water, concentrate, bay.
        SetLed(_leds[0], true);
        SetLed(_leds[1], true);
        SetLed(_leds[2], true);
        SetLed(_leds[3], loaded);
    }

    protected override void Resized()
    {
        base.Resized();
        if (_state != null)
            UpdateState(_state);
    }
}
