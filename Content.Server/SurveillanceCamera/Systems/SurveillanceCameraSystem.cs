using Content.Server.Camera;
using Content.Server.Emp;
using Content.Server.Wires;
using Content.Shared.ActionBlocker;
using Content.Shared.Camera;
using Content.Shared.Power;
using Content.Shared.SurveillanceCamera;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.SurveillanceCamera;

public sealed partial class SurveillanceCameraSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private CameraNetworkSystem _cameraNetworks = default!;
    [Dependency] private CameraSessionSystem _cameraSessions = default!;
    [Dependency] private UserInterfaceSystem _userInterface = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private WiresSystem _wires = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    // Pings a surveillance camera subnet. All cameras will always respond
    // with a data message if they are on the same subnet.
    public const string CameraPingSubnetMessage = "surveillance_camera_ping_subnet";

    // Pings a surveillance camera. Useful to ensure that the camera is still on
    // before connecting fully.
    public const string CameraPingMessage = "surveillance_camera_ping";

    // Camera heartbeat. Monitors ping this to ensure that a camera is still able to
    // be contacted. If this doesn't get sent after some time, the monitor will
    // automatically disconnect.
    public const string CameraHeartbeatMessage = "surveillance_camera_heartbeat";

    // Surveillance camera data. This generally should contain nothing
    // except for the subnet that this camera is on -
    // this is because of the fact that the PacketEvent already
    // contains the sender UID, and that this will always be targeted
    // towards the sender that pinged the camera.
    public const string CameraDataMessage = "surveillance_camera_data";
    public const string CameraConnectMessage = "surveillance_camera_connect";
    public const string CameraSubnetConnectMessage = "surveillance_camera_subnet_connect";
    public const string CameraSubnetDisconnectMessage = "surveillance_camera_subnet_disconnect";

    public const string CameraAddressData = "surveillance_camera_data_origin";
    public const string CameraNameData = "surveillance_camera_data_name";
    public const string CameraSubnetData = "surveillance_camera_data_subnet";

    public const int CameraNameLimit = 32;

    public override void Initialize()
    {
        SubscribeLocalEvent<SurveillanceCameraComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SurveillanceCameraComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SurveillanceCameraComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<SurveillanceCameraComponent, SurveillanceCameraSetupSetName>(OnSetName);
        SubscribeLocalEvent<SurveillanceCameraComponent, SurveillanceCameraSetupSetNetwork>(OnSetNetwork);
        SubscribeLocalEvent<SurveillanceCameraComponent, GetVerbsEvent<AlternativeVerb>>(AddVerbs);
        SubscribeLocalEvent<SurveillanceCameraComponent, PanelChangedEvent>(OnPanelChanged);
        SubscribeLocalEvent<SurveillanceCameraComponent, CameraSessionSelectionChangedEvent>(OnSessionSelectionChanged);

        SubscribeLocalEvent<SurveillanceCameraComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<SurveillanceCameraComponent, EmpDisabledRemoved>(OnEmpDisabledRemoved);
    }

    private void OnStartup(Entity<SurveillanceCameraComponent> ent, ref ComponentStartup args)
    {
        // Camera membership is explicit in the CMU/RMC camera prototypes. Do not
        // turn arbitrary legacy SurveillanceCamera entities into generic station
        // sources: that would reintroduce the excluded station camera scope and
        // would mutate their runtime state during startup.
    }

    private void AddVerbs(EntityUid uid, SurveillanceCameraComponent component, GetVerbsEvent<AlternativeVerb> verbs)
    {
        if (!_wires.IsPanelOpen(uid))
            return;

        if (!_actionBlocker.CanInteract(verbs.User, uid) || !_actionBlocker.CanComplexInteract(verbs.User))
        {
            return;
        }

        AlternativeVerb verb = new();
        verb.Text = Loc.GetString("surveillance-camera-setup");
        verb.Act = () => OpenSetupInterface(uid, verbs.User, component);
        verbs.Verbs.Add(verb);
    }



    private void OnPowerChanged(EntityUid camera, SurveillanceCameraComponent component, ref PowerChangedEvent args)
    {
        SetActive(camera, args.Powered, component);
    }

    private void OnShutdown(EntityUid camera, SurveillanceCameraComponent component, ComponentShutdown args)
    {
        Deactivate(camera, component);
    }

    private void OnSetName(EntityUid uid, SurveillanceCameraComponent component, SurveillanceCameraSetupSetName args)
    {
        if (!_wires.IsPanelOpen(uid))
            return;

        if (args.UiKey is not SurveillanceCameraSetupUiKey key
            || key != SurveillanceCameraSetupUiKey.Camera
            || string.IsNullOrEmpty(args.Name)
            || args.Name.Length > CameraNameLimit)
        {
            return;
        }

        component.CameraId = args.Name;
        component.NameSet = true;
        _metaData.SetEntityName(uid, args.Name);
        UpdateSetupInterface(uid, component);
    }

    private void OnSetNetwork(EntityUid uid, SurveillanceCameraComponent component,
        SurveillanceCameraSetupSetNetwork args)
    {
        if (!_wires.IsPanelOpen(uid))
            return;

        if (args.UiKey is not SurveillanceCameraSetupUiKey key
            || key != SurveillanceCameraSetupUiKey.Camera)
        {
            return;
        }
        if (args.Network < 0 || args.Network >= component.AvailableNetworks.Count)
        {
            return;
        }

        if (!TrySetNetwork((uid, component), component.AvailableNetworks[args.Network]))
            return;

        UpdateSetupInterface(uid, component);
    }

    public bool TrySetNetwork(Entity<SurveillanceCameraComponent> camera, ProtoId<CameraNetworkPrototype> network)
    {
        if (!camera.Comp.AvailableNetworks.Contains(network)
            || !_prototypeManager.TryIndex(network, out CameraNetworkPrototype? prototype)
            || !prototype.Configurable)
        {
            return false;
        }

        var sourceKinds = CameraSourceKinds.Standard;
        if (TryComp(camera.Owner, out CameraNetworkMemberComponent? currentMember))
            sourceKinds = currentMember.SourceKinds;

        var member = EnsureComp<CameraNetworkMemberComponent>(camera.Owner);
        member.SourceKinds = sourceKinds;
        _cameraNetworks.SetMemberNetworks(camera.Owner, [network]);
        camera.Comp.NetworkSet = true;
        return true;
    }

    private void OpenSetupInterface(EntityUid uid, EntityUid player, SurveillanceCameraComponent? camera = null)
    {
        if (!Resolve(uid, ref camera))
            return;

        if (!_wires.IsPanelOpen(uid))
            return;

        if (!_userInterface.TryOpenUi(uid, SurveillanceCameraSetupUiKey.Camera, player))
            return;

        UpdateSetupInterface(uid, camera);
    }

    private void OnPanelChanged(EntityUid uid, SurveillanceCameraComponent component, ref PanelChangedEvent args)
    {
        if (!args.Open)
            _userInterface.CloseUi(uid, SurveillanceCameraSetupUiKey.Camera);
    }

    private void UpdateSetupInterface(EntityUid uid, SurveillanceCameraComponent? camera = null)
    {
        if (!Resolve(uid, ref camera))
        {
            return;
        }

        if (camera.AvailableNetworks.Count == 0)
        {
            if (!TryComp(uid, out CameraNetworkMemberComponent? member))
            {
                _userInterface.CloseUi(uid, SurveillanceCameraSetupUiKey.Camera);
                return;
            }

            foreach (var network in member.Networks)
            {
                if (_prototypeManager.TryIndex(network, out CameraNetworkPrototype? prototype)
                    && prototype.Configurable)
                {
                    camera.AvailableNetworks.Add(network);
                    break;
                }
            }

            if (camera.AvailableNetworks.Count == 0)
            {
                _userInterface.CloseUi(uid, SurveillanceCameraSetupUiKey.Camera);
                return;
            }
        }

        ProtoId<CameraNetworkPrototype>? currentNetwork = null;
        if (TryComp(uid, out CameraNetworkMemberComponent? currentMember))
        {
            foreach (var network in currentMember.Networks)
            {
                if (camera.AvailableNetworks.Contains(network))
                {
                    currentNetwork = network;
                    break;
                }
            }
        }

        // Camera setup remains editable for as long as the maintenance panel is
        // open. NameSet/NetworkSet record that a value exists; they must not
        // disable the controls or close the UI after the first assignment.
        var state = new SurveillanceCameraLogicalNetworkSetupBoundUiState(camera.CameraId, currentNetwork,
            camera.AvailableNetworks, nameDisabled: false, networkDisabled: false);
        _userInterface.SetUiState(uid, SurveillanceCameraSetupUiKey.Camera, state);
    }

    // If the camera deactivates for any reason, it must have all viewers removed,
    // and the relevant event broadcast to all systems.
    private void Deactivate(EntityUid camera, SurveillanceCameraComponent? component = null)
    {
        if (!Resolve(camera, ref component))
        {
            return;
        }

        var ev = new SurveillanceCameraDeactivateEvent(camera);

        component.Active = false;

        // Send a local event that's broadcasted everywhere afterwards.
        RaiseLocalEvent(ev);

        UpdateVisuals(camera, component);
    }

    public void SetActive(EntityUid camera, bool setting, SurveillanceCameraComponent? component = null)
    {
        if (!Resolve(camera, ref component))
        {
            return;
        }

        if (setting)
        {
            var attemptEv = new SurveillanceCameraSetActiveAttemptEvent();
            RaiseLocalEvent(camera, ref attemptEv);
            if (attemptEv.Cancelled)
                return;
            component.Active = setting;
        }
        else
        {
            Deactivate(camera, component);
        }

        UpdateVisuals(camera, component);
    }

    private void UpdateVisuals(EntityUid uid, SurveillanceCameraComponent? component = null, AppearanceComponent? appearance = null)
    {
        // Don't log missing, because otherwise tests fail.
        if (!Resolve(uid, ref component, ref appearance, false))
        {
            return;
        }

        var key = SurveillanceCameraVisuals.Disabled;

        if (component.Active)
        {
            key = SurveillanceCameraVisuals.Active;
        }

        if (_cameraSessions.HasActiveViewers(uid))
        {
            key = SurveillanceCameraVisuals.InUse;
        }

        _appearance.SetData(uid, SurveillanceCameraVisualsKey.Key, key, appearance);
    }

    private void OnSessionSelectionChanged(
        Entity<SurveillanceCameraComponent> camera,
        ref CameraSessionSelectionChangedEvent args)
    {
        UpdateVisuals(camera.Owner, camera.Comp);
    }

    private void OnEmpPulse(EntityUid uid, SurveillanceCameraComponent component, ref EmpPulseEvent args)
    {
        if (component.Active)
        {
            args.Affected = true;
            args.Disabled = true;
            SetActive(uid, false);
        }
    }

    private void OnEmpDisabledRemoved(EntityUid uid, SurveillanceCameraComponent component, ref EmpDisabledRemoved args)
    {
        SetActive(uid, true);
    }
}

public sealed partial class OnSurveillanceCameraViewerAddEvent : EntityEventArgs
{

}

public sealed partial class OnSurveillanceCameraViewerRemoveEvent : EntityEventArgs
{

}

// What happens when a camera deactivates.
public sealed partial class SurveillanceCameraDeactivateEvent : EntityEventArgs
{
    public EntityUid Camera { get; }

    public SurveillanceCameraDeactivateEvent(EntityUid camera)
    {
        Camera = camera;
    }
}

[ByRefEvent]
public record struct SurveillanceCameraSetActiveAttemptEvent(bool Cancelled);
