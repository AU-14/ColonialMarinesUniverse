using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Overwatch;
using Content.Client.CMU14.Telephone;
using Content.Client.Eye;
using Content.Client.Viewport;
using Content.Shared._RMC14.Camera;
using Content.Shared.SurveillanceCamera;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Camera;

/// <summary>
///     A television set's screen, drawn as the set itself: a wooden cabinet around a curved picture tube, a channel
///     readout with up and down keys, the channel list and a power key. It talks to the same camera session as a
///     security monitor, without the map or the network editor.
/// </summary>
public sealed class CMUTelevisionBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUTelevisionWindow? _window;
    private CameraSessionDirectoryUiData _directory = new(null, null, [], null, [], false);
    private uint _sessionId;
    private ulong _revision;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUTelevisionWindow>();
        _window.OnChannel += camera => SendPredictedMessage(new RMCCameraWatchBuiMsg(camera));
        _window.OnChannelUp += () => SendPredictedMessage(new RMCCameraNextBuiMsg());
        _window.OnChannelDown += () => SendPredictedMessage(new RMCCameraPreviousBuiMsg());
        _window.OnPower += () => SendPredictedMessage(new RMCCameraDisconnectBuiMsg());
        _window.OnBand += network => SendPredictedMessage(new RMCCameraSessionNetworkBuiMsg(network));

        if (EntMan.TryGetComponent(Owner, out RMCCameraComputerComponent? computer))
        {
            if (computer.Title is { } title)
                _window.Title = Loc.GetString(title);
            _window.SetViewportSize(computer.ViewportSize);
        }

        _window.UpdateDirectory(_directory);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        switch (message)
        {
            case CameraSessionSnapshotMessage snapshot:
                _sessionId = snapshot.SessionId;
                _revision = snapshot.Revision;
                _directory = snapshot.Directory;
                _window?.UpdateDirectory(_directory);
                break;
            case CameraSessionDeltaMessage delta:
                if (delta.SessionId != _sessionId || delta.BaseRevision != _revision)
                {
                    SendMessage(new CameraSessionResyncMessage(_sessionId));
                    break;
                }

                _revision = delta.Revision;
                _directory = delta.Directory;
                _window?.UpdateDirectory(_directory);
                break;
            case CameraSessionResetMessage reset when reset.SessionId == _sessionId:
                _sessionId = 0;
                _revision = 0;
                _directory = new(null, null, [], null, [], false);
                _window?.UpdateDirectory(_directory);
                break;
        }
    }
}

public sealed class CMUTelevisionWindow : DefaultWindow
{
    private static readonly Color Wood = Color.FromHex("#5a3a22");
    private static readonly Color WoodEdge = Color.FromHex("#2e1d10");
    private static readonly Color WoodLight = Color.FromHex("#734b2d");
    private static readonly Color Bezel = Color.FromHex("#1a1a1a");
    private static readonly Color BezelEdge = Color.FromHex("#3a3a3a");
    private static readonly Color Panel = Color.FromHex("#2a2724");
    private static readonly Color Chrome = Color.FromHex("#b7b9bc");
    private static readonly Color ChromeEdge = Color.FromHex("#6a6d71");
    private static readonly Color ChromeInk = Color.FromHex("#1d1e20");
    private static readonly Color Led = Color.FromHex("#ff3b2f");
    private static readonly Color LedDim = Color.FromHex("#4a1210");
    private static readonly Color Readout = Color.FromHex("#0c0c0c");
    private static readonly Color Cream = Color.FromHex("#e8dcc0");
    private static readonly Color CreamDim = Color.FromHex("#a49a85");

    public event Action<NetEntity>? OnChannel;
    public event Action? OnChannelUp;
    public event Action? OnChannelDown;
    public event Action? OnPower;
    public event Action<NetEntity>? OnBand;

    [Dependency] private IEntityManager _entities = default!;

    private readonly EyeLerpingSystem _eyeLerping;
    private readonly ScalingViewport _viewport;
    private readonly CMUVhsScreenControl _picture;
    private readonly Label _noSignal;
    private readonly Label _channelNumber;
    private readonly Label _channelName;
    private readonly PanelContainer _powerLed;
    private readonly OptionButton _band;
    private readonly BoxContainer _channels;

    private readonly Font _readout;
    private readonly Font _label;
    private readonly Font _key;

    private CameraSessionDirectoryUiData? _directory;
    private EntityUid? _eye;
    private NetEntity? _eyeNet;
    private bool _syncingBand;

    public CMUTelevisionWindow()
    {
        IoCManager.InjectDependencies(this);
        _eyeLerping = _entities.System<EyeLerpingSystem>();

        _readout = CMUPhoneStyle.Font(CMUPhoneStyle.MonoBold, 18);
        _label = CMUPhoneStyle.Font(CMUPhoneStyle.Sans, 11);
        _key = CMUPhoneStyle.Font(CMUPhoneStyle.SansBold, 11);

        MinSize = new Vector2(760, 560);
        SetSize = new Vector2(860, 620);

        // The cabinet: picture on the left, the control panel down the right.
        var cabinet = CMUPhoneStyle.Panel(Wood, WoodEdge, 4, 14, 14);
        cabinet.HorizontalExpand = true;
        cabinet.VerticalExpand = true;
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 14 };
        cabinet.AddChild(row);

        // Picture tube in its bezel.
        var bezel = CMUPhoneStyle.Panel(Bezel, BezelEdge, 3, 10, 10);
        bezel.HorizontalExpand = true;
        bezel.VerticalExpand = true;
        var tube = new Control { HorizontalExpand = true, VerticalExpand = true };
        _viewport = new ScalingViewport
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
            ViewportSize = new Vector2i(640, 640),
            RenderZLevels = true,
            MinimumRenderInterval = TimeSpan.FromSeconds(1.0 / 24),
            Eye = new FixedEye(),
        };
        tube.AddChild(_viewport);
        _picture = new CMUVhsScreenControl { Source = _viewport, HorizontalExpand = true, VerticalExpand = true };
        tube.AddChild(_picture);
        _noSignal = CMUPhoneStyle.Text(Loc.GetString("cmu-tv-no-signal"), _readout, CreamDim);
        _noSignal.HorizontalAlignment = HAlignment.Center;
        _noSignal.VerticalAlignment = VAlignment.Center;
        tube.AddChild(_noSignal);
        bezel.AddChild(tube);
        row.AddChild(bezel);

        // Control panel.
        var controls = CMUPhoneStyle.Panel(Panel, WoodEdge, 2, 10, 10);
        controls.MinWidth = 220;
        controls.MaxWidth = 220;
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        controls.AddChild(column);

        var brand = CMUPhoneStyle.Text(Loc.GetString("cmu-tv-brand"), _key, Chrome);
        brand.HorizontalAlignment = HAlignment.Center;
        column.AddChild(brand);

        var readout = CMUPhoneStyle.Panel(Readout, ChromeEdge, 1, 8, 4);
        var readoutColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _channelNumber = CMUPhoneStyle.Text("--", _readout, Led);
        _channelNumber.HorizontalAlignment = HAlignment.Center;
        readoutColumn.AddChild(_channelNumber);
        _channelName = CMUPhoneStyle.Text(string.Empty, _label, Cream);
        _channelName.HorizontalAlignment = HAlignment.Center;
        _channelName.ClipText = true;
        readoutColumn.AddChild(_channelName);
        readout.AddChild(readoutColumn);
        column.AddChild(readout);

        var tuning = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        var down = CMUPhoneStyle.Key(Loc.GetString("cmu-tv-channel-down"), _key, Chrome, ChromeEdge, ChromeInk);
        down.HorizontalExpand = true;
        down.OnPressed += _ => OnChannelDown?.Invoke();
        tuning.AddChild(down);
        var up = CMUPhoneStyle.Key(Loc.GetString("cmu-tv-channel-up"), _key, Chrome, ChromeEdge, ChromeInk);
        up.HorizontalExpand = true;
        up.OnPressed += _ => OnChannelUp?.Invoke();
        tuning.AddChild(up);
        column.AddChild(tuning);

        column.AddChild(CMUPhoneStyle.Text(Loc.GetString("cmu-tv-band"), _label, CreamDim));
        _band = new OptionButton { HorizontalExpand = true };
        _band.OnItemSelected += args =>
        {
            _band.SelectId(args.Id);
            if (!_syncingBand && _band.GetItemMetadata(args.Id) is NetEntity network)
                OnBand?.Invoke(network);
        };
        column.AddChild(_band);

        column.AddChild(CMUPhoneStyle.Text(Loc.GetString("cmu-tv-channels"), _label, CreamDim));
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _channels = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        scroll.AddChild(_channels);
        column.AddChild(scroll);

        var powerRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        _powerLed = new PanelContainer { MinSize = new Vector2(10, 10), VerticalAlignment = VAlignment.Center };
        powerRow.AddChild(_powerLed);
        var power = CMUPhoneStyle.Key(Loc.GetString("cmu-tv-power-off"), _key, WoodLight, WoodEdge, Cream);
        power.HorizontalExpand = true;
        power.OnPressed += _ => OnPower?.Invoke();
        powerRow.AddChild(power);
        column.AddChild(powerRow);

        row.AddChild(controls);
        Contents.AddChild(cabinet);
    }

    public void SetViewportSize(Vector2i size)
    {
        if (_viewport.ViewportSize != size)
            _viewport.ViewportSize = size;
    }

    public void UpdateDirectory(CameraSessionDirectoryUiData directory)
    {
        _directory = directory;

        _syncingBand = true;
        _band.Clear();
        foreach (var network in directory.Networks)
        {
            _band.AddItem(network.Name);
            var id = _band.ItemCount - 1;
            _band.SetItemMetadata(id, network.Network);
            if (directory.ActiveNetwork == network.Network)
                _band.Select(id);
        }

        _band.Disabled = directory.Networks.Count <= 1;
        _syncingBand = false;

        _channels.RemoveAllChildren();
        for (var i = 0; i < directory.Cameras.Count; i++)
        {
            var camera = directory.Cameras[i];
            var tuned = camera.Camera == directory.ActiveCamera;
            var button = CMUPhoneStyle.Key($"{i + 1:00}  {camera.Name}", _label,
                tuned ? Color.FromHex("#3d2a1a") : Panel, tuned ? Led : WoodEdge, camera.Active ? Cream : CreamDim);
            button.Label.Align = Label.AlignMode.Left;
            button.Label.ClipText = true;
            var id = camera.Camera;
            button.OnPressed += _ => OnChannel?.Invoke(id);
            _channels.AddChild(button);
        }

        var index = directory.Cameras.FindIndex(c => c.Camera == directory.ActiveCamera);
        _channelNumber.Text = index >= 0 ? $"CH {index + 1:00}" : "--";
        _channelName.Text = directory.ActiveCameraName?.ToUpperInvariant() ?? Loc.GetString("cmu-tv-standby");
        _powerLed.PanelOverride = CMUPhoneStyle.Box(directory.ActiveCamera != null ? Led : LedDim);

        SetEye(directory.ActiveCamera);
    }

    private void SetEye(NetEntity? camera)
    {
        if (_eyeNet == camera && _eye != null)
            return;

        if (_eye is { } old && _entities.EntityExists(old))
            _eyeLerping.RemoveEye(old);

        _eye = null;
        _eyeNet = camera;
        _viewport.Eye = new FixedEye();
        TryAttachEye();
    }

    /// <summary>The camera can arrive a tick after the channel list, so keep looking until it does.</summary>
    private void TryAttachEye()
    {
        var signal = false;
        if (_eye == null && _eyeNet is { } net &&
            _entities.TryGetEntity(net, out var uid) &&
            _entities.TryGetComponent(uid, out EyeComponent? eye) && eye.Eye != null)
        {
            _eye = uid;
            _eyeLerping.AddEye(uid.Value, eye);
            _viewport.Eye = eye.Eye;
        }

        signal = _eye != null;
        _noSignal.Visible = !signal;
        _noSignal.Text = Loc.GetString(_eyeNet == null ? "cmu-tv-standby" : "cmu-tv-no-signal");
        _picture.Visible = signal;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_eye == null && _eyeNet != null)
            TryAttachEye();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_eye is { } old && _entities.EntityExists(old))
            _eyeLerping.RemoveEye(old);
        _eye = null;
    }
}
