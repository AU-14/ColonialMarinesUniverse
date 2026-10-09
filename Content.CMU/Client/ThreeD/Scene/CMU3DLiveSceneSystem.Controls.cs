using Content.Client.UserInterface.Systems.EscapeMenu;
using Content.Shared.CMU14.Input;
using Robust.Shared.Input.Binding;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DLiveSceneSystem
{
    private CMU3DCaptureWindow? _captureControls;

    private void InitializeCaptureBinding()
    {
        CommandBinds.Builder
            .Bind(CMUKeyFunctions.CMUToggleFirstPersonMouse, InputCmdHandler.FromDelegate(_ => ToggleMouseCapture()))
            .Register<CMU3DLiveSceneSystem>();
    }

    private void ShutdownCaptureBinding() => CommandBinds.Unregister<CMU3DLiveSceneSystem>();

    private void ShowCaptureControls()
    {
        CloseCaptureControls();
        var window = new CMU3DCaptureWindow();
        _captureControls = window;
        window.StartRequested += () =>
        {
            CloseCaptureControls();
            RequestMouseCapture(true);
        };
        window.ChangeBindingRequested += () =>
        {
            CloseCaptureControls();
            RequestMouseCapture(false);
            _ui.GetUIController<OptionsUIController>().OpenKeybinds();
        };
        window.OnClose += () =>
        {
            if (_captureControls == window)
                _captureControls = null;
            window.Release();
        };
        window.OpenCentered();
    }

    private void CloseCaptureControls()
    {
        var window = _captureControls;
        _captureControls = null;
        window?.Close();
    }
}
