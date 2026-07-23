using Content.Shared._CMU14.Yautja;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._CMU14.Yautja;

[UsedImplicitly]
public sealed class YautjaRelayBeaconBui : BoundUserInterface
{
    private YautjaRelayBeaconWindow? _window;

    public YautjaRelayBeaconBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<YautjaRelayBeaconWindow>();
        _window.OnDestination += (destination, customIndex, destinationId) =>
            SendMessage(new YautjaRelayBeaconDestinationMsg(destination, customIndex, destinationId));

        if (State is YautjaRelayBeaconState state)
            _window.UpdateState(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is YautjaRelayBeaconState relayState)
            _window?.UpdateState(relayState);
    }
}
