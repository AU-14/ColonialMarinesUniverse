using Content.Shared.CMU14.Chemistry.Temperature;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Chemistry.Temperature;

// Client side of the thermostat plate (hotplate, chiller): opens its window and sends the switch and the target the player sets.
public sealed class CMUThermostatPlateBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private CMUThermostatPlateWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CMUThermostatPlateWindow>();
        _window.SetOwner(Owner);
        _window.OnTogglePressed += () => SendMessage(new CMUThermostatToggleMessage());
        _window.OnTargetChanged += target => SendMessage(new CMUThermostatSetTargetMessage(target));
    }
}
