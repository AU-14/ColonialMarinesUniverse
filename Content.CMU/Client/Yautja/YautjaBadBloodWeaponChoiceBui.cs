using Content.Client._RMC14.Xenonids.UI;
using Content.Shared._CMU14.Yautja;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._CMU14.Yautja;

[UsedImplicitly]
public sealed partial class YautjaBadBloodWeaponChoiceBui : BoundUserInterface
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly SpriteSystem _sprite;

    private readonly Dictionary<YautjaGearKind, XenoChoiceControl> _buttons = new();

    [ViewVariables]
    private YautjaBadBloodWeaponChoiceWindow? _window;

    private YautjaGearKind? _pendingChoice;

    private static readonly Dictionary<YautjaGearKind, EntProtoId> GearDisplayPrototypes = new()
    {
        { YautjaGearKind.WristBlades, "CMUYautjaWristBlades" },
        { YautjaGearKind.Scimitar, "CMUYautjaScimitar" },
        { YautjaGearKind.ChainGauntlet, "CMUYautjaChainGauntlet" },
    };

    private static readonly Dictionary<YautjaGearKind, string> GearDisplayNames = new()
    {
        { YautjaGearKind.WristBlades, "Wrist Blades" },
        { YautjaGearKind.Scimitar, "Combi Scimitar" },
        { YautjaGearKind.ChainGauntlet, "Chain Gauntlet" },
    };

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

        if (!EntMan.TryGetComponent(Owner, out YautjaBadBloodGearChoiceComponent? comp))
            return;

        foreach (var kind in comp.Choices)
        {
            if (!GearDisplayPrototypes.TryGetValue(kind, out var protoId))
                continue;

            if (!_prototype.TryIndex(protoId, out var proto))
                continue;

            var displayName = GearDisplayNames.GetValueOrDefault(kind, proto.Name);

            var control = new XenoChoiceControl();
            control.Button.ToggleMode = true;
            control.Set(displayName, _sprite.Frame0(_prototype.Index(protoId)));

            var capturedKind = kind;
            control.Button.OnPressed += _ =>
            {
                SendPredictedMessage(new YautjaBadBloodWeaponChoiceMsg(capturedKind));
            };

            _window.WeaponContainer.AddChild(control);
            _buttons[kind] = control;
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null)
            return;

        if (state is not YautjaBadBloodWeaponChoiceBuiState s)
            return;

        _pendingChoice = s.PendingChoice;
        _window.WarningLabel.Visible = _pendingChoice != null;

        foreach (var (kind, control) in _buttons)
        {
            control.Button.Pressed = kind == _pendingChoice;
        }
    }
}
