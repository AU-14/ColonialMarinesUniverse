using Content.Client._RMC14.Xenonids.UI;
using Content.Shared.CMU14.Yautja;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Yautja;

[UsedImplicitly]
public sealed partial class YautjaBadBloodWeaponChoiceBui : BoundUserInterface
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly SpriteSystem _sprite;

    private readonly Dictionary<YautjaGearKind, XenoChoiceControl> _buttons = new();

    [ViewVariables]
    private YautjaBadBloodWeaponChoiceWindow? _window;

    private YautjaGearKind? _pendingChoice;

    public YautjaBadBloodWeaponChoiceBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _sprite = EntMan.System<SpriteSystem>();
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<YautjaBadBloodWeaponChoiceWindow>();
        _window.WarningLabel.Visible = false;
        _buttons.Clear();
        _pendingChoice = null;
    }

    // the server says which weapons are on offer and what each one is, nothing here knows the gear list
    private void RebuildChoices(List<YautjaBadBloodWeaponChoiceEntry> choices)
    {
        if (_window == null)
            return;

        _window.WeaponContainer.RemoveAllChildren();
        _buttons.Clear();

        foreach (var choice in choices)
        {
            if (!_prototype.TryIndex(choice.Prototype, out var proto))
                continue;

            var control = new XenoChoiceControl();
            control.Button.ToggleMode = true;
            // keep the menu's own names where one exists; anything new falls back to the prototype name
            var label = choice.Kind switch
            {
                YautjaGearKind.WristBlades => Loc.GetString("cmu-yautja-choice-wrist-blades"),
                YautjaGearKind.Scimitar => Loc.GetString("cmu-yautja-choice-scimitar"),
                YautjaGearKind.ChainGauntlet => Loc.GetString("cmu-yautja-choice-chain-gauntlet"),
                _ => proto.Name,
            };
            control.Set(label, _sprite.Frame0(proto));

            var capturedKind = choice.Kind;
            control.Button.OnPressed += _ =>
            {
                SendPredictedMessage(new YautjaBadBloodWeaponChoiceMsg(capturedKind));
            };

            _window.WeaponContainer.AddChild(control);
            _buttons[capturedKind] = control;
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null)
            return;

        if (state is not YautjaBadBloodWeaponChoiceBuiState s)
            return;

        RebuildChoices(s.Choices);
        _pendingChoice = s.PendingChoice;
        _window.WarningLabel.Visible = _pendingChoice != null;

        foreach (var (kind, control) in _buttons)
        {
            control.Button.Pressed = kind == _pendingChoice;
        }
    }
}
