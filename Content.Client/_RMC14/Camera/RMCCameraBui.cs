using Content.Client.Camera;
using Content.Client._RMC14.UserInterface;
using Content.Client.Eye;
using Content.Client.Message;
using Content.Client.UserInterface.ControlExtensions;
using Content.Shared._RMC14.Camera;
using Content.Shared.Camera;
using Content.Shared.SurveillanceCamera;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RMC14.Camera;

public sealed class RMCCameraBui : RMCPopOutBui<RMCCameraWindow>
{
    private EntityUid? _currentCamera;
    private Button? _currentCameraButton;
    private CameraMapUiState? _mapState;
    private CameraSessionDirectoryUiData _directory = new(null, null, [], null, [], false);
    private uint _sessionId;
    private ulong _revision;
    private ulong _markerRevision;

    private readonly EyeLerpingSystem _eyeLerping;
    private bool _editorEnabled;

    protected override RMCCameraWindow? Window { get; set; }

    public RMCCameraBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _eyeLerping = EntMan.System<EyeLerpingSystem>();
    }

    protected override void Open()
    {
        base.Open();
        Window = this.CreatePopOutableWindow<RMCCameraWindow>();
        Window.SearchBar.OnTextChanged += _ => RefreshSearch();
        Window.PreviousCameraButton.Text = "<";
        Window.NextCameraButton.Text = ">";
        Window.RefreshSubnetsButton.Text = Loc.GetString("surveillance-camera-monitor-ui-refresh-subnets");
        Window.DisconnectButton.Text = Loc.GetString("surveillance-camera-monitor-ui-disconnect");
        Window.PreviousCameraButton.OnPressed += _ => SendPredictedMessage(new RMCCameraPreviousBuiMsg());
        Window.NextCameraButton.OnPressed += _ => SendPredictedMessage(new RMCCameraNextBuiMsg());
        Window.RefreshSubnetsButton.OnPressed += _ => SendPredictedMessage(new RMCCameraRefreshSubnetsBuiMsg());
        Window.DisconnectButton.OnPressed += _ => SendPredictedMessage(new RMCCameraDisconnectBuiMsg());
        Window.NetworkSelector.OnItemSelected += args => SendPredictedMessage(GetNetworkSelectionMessage(args));
        Window.CameraSelected += camera => SendPredictedMessage(new RMCCameraWatchBuiMsg(camera));
        Window.NetworkEditor.CreateRequested += message => SendPredictedMessage(message);
        Window.NetworkEditor.RenameRequested += message => SendPredictedMessage(message);
        Window.NetworkEditor.DeleteRequested += message => SendPredictedMessage(message);
        Window.NetworkEditor.HiddenRequested += message => SendPredictedMessage(message);
        Window.NetworkEditor.SaveCameraRequested += message => SendPredictedMessage(message);
        Window.NetworkEditor.EditorCameraSelected += _ => RefreshEditorPreview();

        Refresh();
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        switch (message)
        {
            case CameraSessionSnapshotMessage snapshot:
                if (_sessionId != snapshot.SessionId
                    || _directory.ActiveNetwork != snapshot.Directory.ActiveNetwork)
                    ResetGeometry();

                _sessionId = snapshot.SessionId;
                _revision = snapshot.Revision;
                _directory = snapshot.Directory;
                ApplyDirectory();
                break;
            case CameraSessionDeltaMessage delta:
                if (delta.SessionId != _sessionId || delta.BaseRevision != _revision)
                {
                    SendMessage(new CameraSessionResyncMessage(_sessionId));
                    break;
                }

                if (_directory.ActiveNetwork != delta.Directory.ActiveNetwork)
                    ResetGeometry();

                _revision = delta.Revision;
                _directory = delta.Directory;
                ApplyDirectory();
                break;
            case CameraSessionGeometryMessage geometry when
                geometry.SessionId == _sessionId &&
                geometry.Network == _directory.ActiveNetwork:
                if (geometry.MarkerRevision < _markerRevision)
                    break;
                _markerRevision = geometry.MarkerRevision;
                _mapState = geometry.Geometry;
                UpdateMap(geometry.Geometry);
                break;
            case CameraSessionResetMessage reset when reset.SessionId == _sessionId:
                _sessionId = 0;
                _revision = 0;
                _directory = new(null, null, [], null, [], false);
                ResetGeometry();
                ApplyDirectory();
                break;
            case RMCCameraEditorStateBuiMsg editor:
                _editorEnabled = editor.Enabled;
                Window?.SetFeatures(_directory.MapEnabled, _editorEnabled);
                if (editor.Enabled)
                    Window?.NetworkEditor.SetState(editor.State);
                break;
            case RMCCameraNetworkEditorResultBuiMsg result:
                Window?.NetworkEditor.ShowResult(result);
                break;
        }
    }

    public static void PopulateNetworkSelector(
        OptionButton selector,
        CameraSessionDirectoryUiData state)
    {
        selector.Clear();
        selector.Disabled = state.Networks.Count == 0;

        foreach (var network in state.Networks)
        {
            selector.AddItem(network.Name);
            var id = selector.ItemCount - 1;
            selector.SetItemMetadata(id, network.Network);

            if (state.ActiveNetwork == network.Network)
                selector.Select(id);
        }
    }

    public static RMCCameraSessionNetworkBuiMsg GetNetworkSelectionMessage(OptionButton.ItemSelectedEventArgs args)
    {
        return new RMCCameraSessionNetworkBuiMsg(
            (NetEntity) args.Button.GetItemMetadata(args.Id)!);
    }

    public void Refresh()
    {
        if (Window == null)
            return;

        if (!EntMan.TryGetComponent(Owner, out RMCCameraComputerComponent? computer))
            return;

        if (computer.Title is { } title)
            Window.Title = Loc.GetString(title);

        var currentNetCamera = _directory.ActiveCamera;
        Window.DisconnectButton.Disabled = currentNetCamera == null;
        for (var i = 0; i < _directory.Cameras.Count; i++)
        {
            var id = _directory.Cameras[i].Camera;
            var name = _directory.Cameras[i].Name;

            RMCCameraButton button;
            if (i < Window.CamerasContainer.ChildCount)
            {
                if (Window.CamerasContainer.GetChild(i) is not RMCCameraButton child)
                    continue;

                button = child;
            }
            else
            {
                button = new RMCCameraButton();

                button.OnPressed += _ =>
                {
                    if (_currentCameraButton != null)
                        _currentCameraButton.Pressed = false;

                    _currentCameraButton = button;
                    SendPredictedMessage(button.Binding.CreateSelectionMessage());
                };

                Window.CamerasContainer.AddChild(button);
            }

            button.Binding.Bind(id);
            button.TextLabel.SetMarkupPermissive($"[font size=11][color=white]{name}[/color][/font]");
            button.Pressed = id == currentNetCamera;
        }

        for (var i = Window.CamerasContainer.ChildCount - 1; i >= _directory.Cameras.Count; i--)
        {
            Window.CamerasContainer.RemoveChild(i);
        }

        RefreshSearch();
        RefreshCamera();

        if (_mapState != null)
            UpdateMap(_mapState);
    }

    private void ResetGeometry()
    {
        _markerRevision = 0;
        _mapState = null;
    }

    private void UpdateMap(CameraMapUiState state)
    {
        if (Window == null)
            return;

        Window.UpdateMap(state, _directory.ActiveCamera);
    }

    private void RefreshSearch()
    {
        if (Window == null)
            return;

        foreach (var control in Window.CamerasContainer.Children)
        {
            if (control is not Button button)
                continue;

            button.Visible = button.ChildrenContainText(Window.SearchBar.Text);
        }
    }

    private void RefreshCamera()
    {
        if (Window == null)
            return;

        if (_currentCamera is { } oldCamera)
            _eyeLerping.RemoveEye(oldCamera);

        _currentCamera = null;

        if (_directory.ActiveCamera is not { } netCamera
            || !EntMan.TryGetEntity(netCamera, out var cameraUid)
            || cameraUid is not { } camera)
        {
            var emptyEye = new FixedEye();
            Window.Viewport.Eye = emptyEye;
            Window.MapViewport.Eye = emptyEye;
            Window.CameraName.Text = string.Empty;
            Window.MapCameraName.Text = string.Empty;
            RefreshEditorPreview();
            return;
        }

        if (!camera.IsValid() ||
            !EntMan.EntityExists(camera) ||
            !EntMan.TryGetComponent(camera, out EyeComponent? eye))
        {
            var fixedEye = new FixedEye();
            Window.Viewport.Eye = fixedEye;
            Window.MapViewport.Eye = fixedEye;
            Window.CameraName.Text = string.Empty;
            Window.MapCameraName.Text = string.Empty;
            RefreshEditorPreview();
            return;
        }

        _eyeLerping.AddEye(camera, eye);
        Window.Viewport.Eye = eye.Eye;
        Window.MapViewport.Eye = eye.Eye;
        _currentCamera = camera;
        if (_directory.ActiveCameraName is { } name)
        {
            Window.CameraName.Text = name;
            Window.MapCameraName.Text = name;
        }

        RefreshEditorPreview();
    }

    private void RefreshEditorPreview()
    {
        if (Window == null)
            return;

        Window.NetworkEditor.CameraPreview.Eye = new FixedEye();
        if (_directory.ActiveCamera is not { } netCamera ||
            !EntMan.TryGetEntity(netCamera, out var currentUid) ||
            currentUid is not { } current ||
            Window.NetworkEditor.SelectedCamera != netCamera ||
            !EntMan.TryGetComponent(current, out EyeComponent? eye))
        {
            return;
        }

        Window.NetworkEditor.CameraPreview.Eye = eye.Eye;
    }

    private void ApplyDirectory()
    {
        if (Window == null)
            return;

        Window.SetFeatures(_directory.MapEnabled, _editorEnabled);
        PopulateNetworkSelector(Window.NetworkSelector, _directory);
        Refresh();
    }
}
