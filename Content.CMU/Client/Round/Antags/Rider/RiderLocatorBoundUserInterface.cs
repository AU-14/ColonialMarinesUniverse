using Content.Shared.CMU14.Round.Antags.Rider;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Round.Antags.Rider;

[UsedImplicitly]
public sealed partial class RiderLocatorBoundUserInterface(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private RiderLocatorWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<RiderLocatorWindow>();
        _window.SetFollowTarget += rider => SendMessage(new RiderLocatorFollowMessage(rider));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is RiderLocatorState riders)
            _window?.UpdateRiders(riders.Riders);
    }
}
