using Content.Shared._CMU14.Blackfoot;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._CMU14.Blackfoot;

[UsedImplicitly]
public sealed class BlackfootFlightComputerBui : BoundUserInterface
{
    private BlackfootFlightComputerWindow? _window;

    public BlackfootFlightComputerBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BlackfootFlightComputerWindow>();
        _window.OnFuelToggle += () => SendMessage(new BlackfootFlightComputerFuelToggleMsg());
        _window.OnBatteryToggle += () => SendMessage(new BlackfootFlightComputerBatteryToggleMsg());

        if (State is BlackfootFlightComputerBuiState state)
            _window.UpdateState(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is BlackfootFlightComputerBuiState computerState)
            _window?.UpdateState(computerState);
    }
}
