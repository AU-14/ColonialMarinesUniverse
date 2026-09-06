using System.Linq;
using Content.Server.Camera;
using Content.Shared.Access.Systems;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Content.Shared.Power;
using Content.Shared.SurveillanceCamera;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.SurveillanceCamera;

public sealed partial class SurveillanceCameraMonitorSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly CameraNetworkSystem _cameraNetworks = default!;
    [Dependency] private readonly CameraSessionSystem _cameraSessions = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SurveillanceCameraMonitorComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<SurveillanceCameraMonitorComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SurveillanceCameraMonitorComponent, CameraSessionChangedEvent>(OnCameraSessionChanged);
        SubscribeLocalEvent<SurveillanceCameraMonitorComponent, AfterActivatableUIOpenEvent>(OnToggleInterface);
        Subs.BuiEvents<SurveillanceCameraMonitorComponent>(SurveillanceCameraMonitorUiKey.Key, subs =>
        {
            subs.Event<CameraSessionResyncMessage>(OnResyncMessage);
            subs.Event<CameraSessionSelectMessage>(OnSessionSelectMessage);
            subs.Event<CameraSessionSelectNetworkMessage>(OnSessionSelectNetworkMessage);
            subs.Event<CameraSessionDisconnectMessage>(OnSessionDisconnectMessage);
            subs.Event<BoundUIClosedEvent>(OnBoundUiClose);
        });
    }

    private void OnSessionSelectMessage(EntityUid uid, SurveillanceCameraMonitorComponent component,
        CameraSessionSelectMessage args)
    {
        if (TryGetSession(uid, component, args.Actor, out var session)
            && TryGetEntity(args.Camera, out var camera)
            && camera is { } cameraUid)
        {
            _cameraSessions.SelectCamera(session.Id, cameraUid);
        }
    }

    private void OnSessionSelectNetworkMessage(EntityUid uid, SurveillanceCameraMonitorComponent component,
        CameraSessionSelectNetworkMessage args)
    {
        if (TryGetSession(uid, component, args.Actor, out var session)
            && TryGetEntity(args.Network, out var network)
            && network is { } networkUid)
        {
            _cameraSessions.SelectNetwork(session.Id, networkUid);
        }
    }

    private void OnSessionDisconnectMessage(EntityUid uid, SurveillanceCameraMonitorComponent component,
        CameraSessionDisconnectMessage args)
    {
        if (TryGetSession(uid, component, args.Actor, out var session))
            _cameraSessions.SelectCamera(session.Id, null);
    }

    private void OnResyncMessage(EntityUid uid, SurveillanceCameraMonitorComponent component,
        CameraSessionResyncMessage args)
    {
        if (TryGetSession(uid, component, args.Actor, out var session) && session.Id == args.SessionId)
            SendSnapshot(uid, args.Actor, session);
    }

    private void OnPowerChanged(EntityUid uid, SurveillanceCameraMonitorComponent component, ref PowerChangedEvent args)
    {
        if (!args.Powered)
        {
            _cameraSessions.ClearSelections(uid);
            UpdateMonitorVisual(uid);
        }
    }

    private void OnShutdown(EntityUid uid, SurveillanceCameraMonitorComponent component, ComponentShutdown args)
    {
        _cameraSessions.CloseSessions(uid);
    }

    private void OnToggleInterface(EntityUid uid, SurveillanceCameraMonitorComponent component,
        AfterActivatableUIOpenEvent args)
    {
        AfterOpenUserInterface(uid, args.User, component);
    }

    private void OnCameraSessionChanged(EntityUid uid, SurveillanceCameraMonitorComponent monitor,
        ref CameraSessionChangedEvent args)
    {
        UpdateViewer(uid, args.Actor);
        UpdateMonitorVisual(uid);
    }

    private void OnBoundUiClose(EntityUid uid, SurveillanceCameraMonitorComponent component, BoundUIClosedEvent args)
    {
        RemoveViewer(uid, args.Actor, component);
    }

    private void AddViewer(EntityUid uid, EntityUid player, SurveillanceCameraMonitorComponent monitor)
    {
        if (!TryComp(player, out ActorComponent? actor))
            return;

        var capabilities = CameraSessionCapabilities.Browse | CameraSessionCapabilities.LiveView;
        if (_configuration.GetCVar(CCVars.CMUCameraMapEnabled))
            capabilities |= CameraSessionCapabilities.Map;

        var session = _cameraSessions.OpenSession(
            actor.PlayerSession,
            player,
            uid,
            capabilities);
        if (session != null)
            SendSnapshot(uid, player, session);
    }

    private void RemoveViewer(EntityUid uid, EntityUid player, SurveillanceCameraMonitorComponent monitor)
    {
        if (!TryComp(player, out ActorComponent? actor))
            return;

        if (_cameraSessions.TryGetSession(actor.PlayerSession, uid, out var session))
        {
            _userInterface.ServerSendUiMessage(
                uid,
                SurveillanceCameraMonitorUiKey.Key,
                new CameraSessionResetMessage(session.Id),
                player);
        }

        _cameraSessions.CloseSession(actor.PlayerSession, uid);
        UpdateMonitorVisual(uid);
    }

    public void AfterOpenUserInterface(
        EntityUid uid,
        EntityUid player,
        SurveillanceCameraMonitorComponent? monitor = null,
        ActorComponent? actor = null)
    {
        if (!Resolve(uid, ref monitor) || !Resolve(player, ref actor) || !CanUseMonitor(uid, player))
            return;

        AddViewer(uid, player, monitor);
    }

    private bool TryGetSession(
        EntityUid monitorUid,
        SurveillanceCameraMonitorComponent monitor,
        EntityUid actor,
        out CameraViewerSession session)
    {
        if (CanUseMonitor(monitorUid, actor)
            && TryComp(actor, out ActorComponent? actorComponent)
            && _cameraSessions.TryGetSession(actorComponent.PlayerSession, monitorUid, out session))
        {
            return true;
        }

        RemoveViewer(monitorUid, actor, monitor);
        session = default!;
        return false;
    }

    private CameraSessionDirectoryUiData BuildDirectory(CameraViewerSession session)
    {
        var networks = _cameraNetworks.GetEffectiveNetworkEntities(session.Receiver)
            .Where(network => TryComp(network, out CameraNetworkIdentityComponent? _))
            .Select(network => new CameraSessionNetworkUiData(
                GetNetEntity(network),
                Comp<CameraNetworkIdentityComponent>(network).DisplayName))
            .OrderBy(network => network.Name, StringComparer.Ordinal)
            .ThenBy(network => network.Network.Id)
            .ToList();

        var cameras = new List<CameraSessionCameraUiData>();
        if (session.ActiveNetwork is { } activeNetwork)
        {
            cameras = session.AuthorizedCameras
                .Where(camera => _cameraNetworks.IsMemberOfNetwork(camera, activeNetwork))
                .OrderBy(camera => Name(camera), StringComparer.Ordinal)
                .ThenBy(camera => camera.Id)
                .Select(camera => new CameraSessionCameraUiData(
                    GetNetEntity(camera),
                    Name(camera),
                    TryComp(camera, out SurveillanceCameraComponent? source) && source.Active))
                .ToList();
        }

        return new CameraSessionDirectoryUiData(
            GetNetEntity(session.SelectedCamera),
            session.SelectedCamera is { } camera ? Name(camera) : null,
            networks,
            GetNetEntity(session.ActiveNetwork),
            cameras,
            (session.Capabilities & CameraSessionCapabilities.Map) != 0);
    }

    private CameraMapUiState BuildGeometry(CameraViewerSession session)
    {
        if ((session.Capabilities & CameraSessionCapabilities.Map) == 0
            || session.ActiveNetwork is not { } activeNetwork)
        {
            return new CameraMapUiState(default, []);
        }

        var map = _cameraNetworks.BuildMapState(session.Receiver);
        var grids = map.Grids
            .Select(grid => new CameraMapGridUiData(
                grid.Grid,
                grid.Name,
                grid.Markers
                    .Where(marker => TryGetEntity(marker.Camera, out var camera)
                        && camera is { } cameraUid
                        && _cameraNetworks.IsMemberOfNetwork(cameraUid, activeNetwork))
                    .ToList()))
            .Where(grid => grid.Markers.Count > 0)
            .ToList();
        return new CameraMapUiState(map.ConsoleGrid, grids);
    }

    private void SendSnapshot(EntityUid uid, EntityUid actor, CameraViewerSession session)
    {
        _userInterface.ServerSendUiMessage(
            uid,
            SurveillanceCameraMonitorUiKey.Key,
            new CameraSessionSnapshotMessage(session.Id, session.Revision, BuildDirectory(session)),
            actor);
        session.LastSentRevision = session.Revision;
        SendGeometry(uid, actor, session);
    }

    private void SendDelta(EntityUid uid, EntityUid actor, CameraViewerSession session)
    {
        if (session.LastSentRevision == 0 || session.LastSentRevision > session.Revision)
        {
            SendSnapshot(uid, actor, session);
            return;
        }

        if (session.LastSentRevision == session.Revision)
        {
            SendGeometry(uid, actor, session);
            return;
        }

        _userInterface.ServerSendUiMessage(
            uid,
            SurveillanceCameraMonitorUiKey.Key,
            new CameraSessionDeltaMessage(
                session.Id,
                session.LastSentRevision,
                session.Revision,
                BuildDirectory(session)),
            actor);
        session.LastSentRevision = session.Revision;
        SendGeometry(uid, actor, session);
    }

    private void SendGeometry(EntityUid uid, EntityUid actor, CameraViewerSession session)
    {
        if ((session.Capabilities & CameraSessionCapabilities.Map) == 0
            || session.LastSentMarkerRevision == _cameraNetworks.MarkerRevision
            && session.LastSentGeometryNetwork == session.ActiveNetwork)
        {
            return;
        }

        _userInterface.ServerSendUiMessage(
            uid,
            SurveillanceCameraMonitorUiKey.Key,
            new CameraSessionGeometryMessage(
                session.Id,
                GetNetEntity(session.ActiveNetwork),
                _cameraNetworks.MarkerRevision,
                BuildGeometry(session)),
            actor);
        session.LastSentMarkerRevision = _cameraNetworks.MarkerRevision;
        session.LastSentGeometryNetwork = session.ActiveNetwork;
    }

    private void UpdateViewer(EntityUid uid, EntityUid actor)
    {
        if (!TryComp(actor, out ActorComponent? actorComponent)
            || !_cameraSessions.TryGetSession(actorComponent.PlayerSession, uid, out var session)
            || !_userInterface.IsUiOpen(uid, SurveillanceCameraMonitorUiKey.Key, actor))
        {
            return;
        }

        SendDelta(uid, actor, session);
    }

    private void UpdateMonitorVisual(EntityUid uid)
    {
        if (_cameraSessions.HasActiveSelection(uid))
            EnsureComp<ActiveSurveillanceCameraMonitorComponent>(uid);
        else
            RemComp<ActiveSurveillanceCameraMonitorComponent>(uid);
    }

    private bool CanUseMonitor(EntityUid monitor, EntityUid actor)
    {
        return !TerminatingOrDeleted(actor)
            && TryComp(actor, out ActorComponent? actorComponent)
            && actorComponent.PlayerSession.AttachedEntity == actor
            && _userInterface.IsUiOpen(monitor, SurveillanceCameraMonitorUiKey.Key, actor)
            && _accessReader.IsAllowed(actor, monitor);
    }

}
