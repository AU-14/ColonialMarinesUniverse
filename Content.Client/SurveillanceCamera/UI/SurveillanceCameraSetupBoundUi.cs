using Content.Shared.SurveillanceCamera;
using Robust.Client.GameObjects;

namespace Content.Client.SurveillanceCamera.UI;

public sealed class SurveillanceCameraSetupBoundUi : BoundUserInterface
{
    [ViewVariables]
    private readonly SurveillanceCameraSetupUiKey _type;

    [ViewVariables]
    private SurveillanceCameraSetupWindow? _window;

    public SurveillanceCameraSetupBoundUi(EntityUid component, Enum uiKey) : base(component, uiKey)
    {
        if (uiKey is not SurveillanceCameraSetupUiKey key)
            return;

        _type = key;
    }

    protected override void Open()
    {
        base.Open();

        _window = new();

        if (_type == SurveillanceCameraSetupUiKey.Router)
        {
            _window.HideNameSelector();
        }

        _window.OpenCentered();
        _window.OnNameConfirm += SendDeviceName;
        _window.OnNetworkConfirm += SendSelectedNetwork;
        _window.OnClose += Close;
    }

    private void SendSelectedNetwork(int idx)
    {
        SendMessage(new SurveillanceCameraSetupSetNetwork(idx));
    }

    private void SendDeviceName(string name)
    {
        SendMessage(new SurveillanceCameraSetupSetName(name));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null)
        {
            return;
        }

        switch (state)
        {
            case SurveillanceCameraSetupBoundUiState frequencyState:
                _window.UpdateState(frequencyState.Name, frequencyState.NameDisabled, frequencyState.NetworkDisabled);
                _window.LoadAvailableNetworks(frequencyState.Network, frequencyState.Networks);
                break;
            case SurveillanceCameraLogicalNetworkSetupBoundUiState networkState:
                _window.UpdateState(networkState.Name, networkState.NameDisabled, networkState.NetworkDisabled);
                _window.LoadAvailableCameraNetworks(networkState.Network, networkState.Networks);
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _window?.Close();
            _window = null;
        }
    }
}
