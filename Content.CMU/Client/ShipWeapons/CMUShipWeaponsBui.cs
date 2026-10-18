using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.CMU14.ShipWeapons;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.ShipWeapons;

public sealed class CMUShipWeaponsBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUShipWeaponsWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUShipWeaponsWindow>();
        _window.OnArm += (first, second) => SendMessage(new CMUShipWeaponsArmBuiMsg(first, second));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is CMUShipWeaponsBuiState weapons)
            _window?.UpdateState(weapons);
    }
}

public sealed class CMUShipWeaponsWindow : DefaultWindow
{
    public event Action<ProtoId<CMUShipWeaponPrototype>, ProtoId<CMUShipWeaponPrototype>>? OnArm;

    private readonly Label _status;
    private readonly BoxContainer _choices;
    private readonly ConfirmButton _arm;
    private readonly Dictionary<ProtoId<CMUShipWeaponPrototype>, CheckBox> _buttons = new();
    private bool _locked;
    private bool _available;
    private HashSet<ProtoId<CMUShipWeaponPrototype>> _availableWeapons = new();

    public CMUShipWeaponsWindow()
    {
        Title = Loc.GetString("cmu-ship-weapons-title");
        MinSize = new Vector2(460, 480);
        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        AddChild(root);
        root.AddChild(new Label { Text = Loc.GetString("cmu-ship-weapons-instructions") });
        _status = new Label();
        root.AddChild(_status);
        _choices = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        root.AddChild(_choices);
        _arm = new ConfirmButton
        {
            Text = Loc.GetString("cmu-ship-weapons-arm"),
            ConfirmationText = Loc.GetString("cmu-ship-weapons-confirm"),
            Disabled = true,
        };

        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        foreach (var profile in prototypes.EnumeratePrototypes<CMUShipWeaponPrototype>().OrderBy(p => p.ID))
        {
            var button = new CheckBox { Text = Loc.GetString(profile.Name) };
            _choices.AddChild(button);
            _buttons.Add(profile.ID, button);
            button.OnToggled += _ =>
            {
                _arm.IsConfirming = false;
                UpdateChoices();
            };
        }

        root.AddChild(_arm);
        _arm.OnPressed += _ =>
        {
            var selected = _buttons.Where(p => p.Value.Pressed).Select(p => p.Key).ToArray();
            if (selected.Length == 2)
                OnArm?.Invoke(selected[0], selected[1]);
        };
    }

    public void UpdateState(CMUShipWeaponsBuiState state)
    {
        _available = state.Available;
        _locked = state.Weapons.Length != 0;
        _availableWeapons = state.AvailableWeapons.ToHashSet();
        _status.Text = Loc.GetString(!_available ? "cmu-ship-weapons-no-ship" :
            _locked ? "cmu-ship-weapons-locked" : "cmu-ship-weapons-unarmed");
        foreach (var (id, button) in _buttons)
        {
            if (!_locked)
            {
                if (!_availableWeapons.Contains(id))
                    button.Pressed = false;
                button.ToolTip = state.RequiresGuns && !_availableWeapons.Contains(id)
                    ? Loc.GetString("cmu-ship-weapons-missing-gun")
                    : null;
                continue;
            }

            var armed = state.Weapons.FirstOrDefault(w => w.Weapon == id);
            button.Pressed = state.Weapons.Any(w => w.Weapon == id);
            var profile = IoCManager.Resolve<IPrototypeManager>().Index(id);
            button.Text = button.Pressed
                ? Loc.GetString("cmu-ship-weapons-remaining", ("weapon", Loc.GetString(profile.Name)), ("uses", armed.Remaining))
                : Loc.GetString(profile.Name);
        }
        UpdateChoices();
    }

    private void UpdateChoices()
    {
        var count = _buttons.Values.Count(b => b.Pressed);
        foreach (var (id, button) in _buttons)
            button.Disabled = !_available || _locked || !_availableWeapons.Contains(id) || count >= 2 && !button.Pressed;
        if (!_arm.IsConfirming || !_available || _locked || count != 2)
            _arm.Disabled = !_available || _locked || count != 2;
    }
}
