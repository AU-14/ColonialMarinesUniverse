using System.Linq;
using Content.Server.Camera;
using Content.Server.SurveillanceCamera;
using Content.Shared._RMC14.Camera;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Content.Shared.SurveillanceCamera;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Camera;

public sealed partial class RMCCameraSystem : SharedRMCCameraSystem
{
    [Dependency] private CameraNetworkSystem _cameraNetworks = default!;
    [Dependency] private CameraSessionSystem _cameraSessions = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private UserInterfaceSystem _userInterface = default!;

    private EntityQuery<ActorComponent> _actorQuery;

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();

        SubscribeLocalEvent<RMCCameraComputerComponent, CameraSessionChangedEvent>(OnCameraSessionChanged);
        SubscribeLocalEvent<RMCCameraNetworkEditorComponent, ComponentShutdown>(OnCameraEditorShutdown);
        SubscribeLocalEvent<RMCCameraComponent, ComponentShutdown>(OnEditorCameraShutdown);
    }

    private void OnCameraSessionChanged(
        Entity<RMCCameraComputerComponent> computer,
        ref CameraSessionChangedEvent args)
    {
        if (!_actorQuery.TryComp(args.Actor, out var actor)
            || !_cameraSessions.TryGetSession(actor.PlayerSession, computer.Owner, out var session)
            || !_userInterface.IsUiOpen(computer.Owner, RMCCameraUiKey.Key, args.Actor))
        {
            return;
        }

        SendSessionDelta(computer, args.Actor, session);
    }

    protected override void RefreshRejectedSelection(Entity<RMCCameraComputerComponent> computer)
    {
        UpdateUserInterface(computer);
    }

    public override bool TrySelectCameraFor(
        Entity<RMCCameraComputerComponent> computer,
        EntityUid actor,
        EntityUid camera)
    {
        return TryGetSession(computer, actor, out var session)
            && CanSelectCamera(computer, camera, session.ActiveNetwork)
            && _cameraSessions.SelectCamera(session.Id, camera);
    }

    private bool CanSelectCamera(
        Entity<RMCCameraComputerComponent> computer,
        EntityUid camera,
        EntityUid? activeNetwork)
    {
        return !TerminatingOrDeleted(camera)
               && !Paused(camera)
               && _cameraNetworks.CanAccess(computer.Owner, camera)
               && TryComp(camera, out CameraNetworkMemberComponent? member)
               && (member.SourceKinds & CameraSourceKinds.Rmc) != CameraSourceKinds.None
               && activeNetwork is { } selectedNetwork
               && _cameraNetworks.IsMemberOfNetwork(camera, selectedNetwork)
               && (!TryComp(camera, out SurveillanceCameraComponent? surveillance) || surveillance.Active);
    }

    private void UpdateUserInterface(Entity<RMCCameraComputerComponent> computer)
    {
        if (!_userInterface.IsUiOpen(computer.Owner, RMCCameraUiKey.Key))
            return;

        foreach (var session in _cameraSessions.GetSessions(computer.Owner))
        {
            if (_userInterface.IsUiOpen(computer.Owner, RMCCameraUiKey.Key, session.Actor))
                SendSessionDelta(computer, session.Actor, session);
        }
    }

    protected override void OnSessionNetworkBuiMsg(
        Entity<RMCCameraComputerComponent> computer,
        RMCCameraSessionNetworkBuiMsg args)
    {
        if (TryGetSession(computer, args.Actor, out var session)
            && TryGetEntity(args.Network, out var network)
            && network is { } networkUid)
        {
            _cameraSessions.SelectNetwork(session.Id, networkUid);
        }
    }

    protected override void OnSessionResyncBuiMsg(
        Entity<RMCCameraComputerComponent> computer,
        CameraSessionResyncMessage args)
    {
        if (TryGetSession(computer, args.Actor, out var session) && session.Id == args.SessionId)
            SendSessionSnapshot(computer, args.Actor, session);
    }

    private List<EntityUid> GetVisibleNetworkEntities(EntityUid computer)
    {
        var hidden = TryComp(computer, out RMCCameraNetworkEditorComponent? editor)
            ? editor.HiddenSeededNetworks
            : [];
        return _cameraNetworks.GetEffectiveNetworkEntities(computer)
            .Where(network => !hidden.Contains(network))
            .Where(network => HasComp<CameraNetworkIdentityComponent>(network))
            .OrderBy(network => ResolveSessionNetworkName(computer, network), StringComparer.Ordinal)
            .ThenBy(network => network.Id)
            .ToList();
    }

    private CameraSessionDirectoryUiData BuildSessionDirectory(CameraViewerSession session)
    {
        var networks = GetVisibleNetworkEntities(session.Receiver)
            .Select(network => new CameraSessionNetworkUiData(
                GetNetEntity(network),
                ResolveSessionNetworkName(session.Receiver, network)))
            .ToList();
        var cameras = session.ActiveNetwork is { } activeNetwork
            ? session.AuthorizedCameras
                .Where(camera => _cameraNetworks.IsMemberOfNetwork(camera, activeNetwork))
                .Where(camera => TryComp(camera, out RMCCameraComponent? _))
                .OrderBy(camera => GetCameraName(camera, Comp<RMCCameraComponent>(camera)), StringComparer.Ordinal)
                .ThenBy(camera => camera.Id)
                .Select(camera => new CameraSessionCameraUiData(
                    GetNetEntity(camera),
                    GetCameraName(camera, Comp<RMCCameraComponent>(camera)),
                    !TryComp(camera, out SurveillanceCameraComponent? surveillance) || surveillance.Active))
                .ToList()
            : [];
        var selectedName = session.SelectedCamera is { } selected && TryComp(selected, out RMCCameraComponent? rmc)
            ? GetCameraName(selected, rmc)
            : null;
        return new CameraSessionDirectoryUiData(
            GetNetEntity(session.SelectedCamera),
            selectedName,
            networks,
            GetNetEntity(session.ActiveNetwork),
            cameras,
            (session.Capabilities & CameraSessionCapabilities.Map) != 0);
    }

    private string ResolveSessionNetworkName(EntityUid computer, EntityUid network)
    {
        return TryResolveNetworkName(computer, network, out var name)
            ? name
            : Comp<CameraNetworkIdentityComponent>(network).DisplayName;
    }

    private CameraMapUiState BuildSessionGeometry(CameraViewerSession session)
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

    private void SendSessionSnapshot(
        Entity<RMCCameraComputerComponent> computer,
        EntityUid actor,
        CameraViewerSession session)
    {
        _userInterface.ServerSendUiMessage(
            computer.Owner,
            RMCCameraUiKey.Key,
            new CameraSessionSnapshotMessage(session.Id, session.Revision, BuildSessionDirectory(session)),
            actor);
        session.LastSentRevision = session.Revision;
        SendSessionGeometry(computer.Owner, actor, session);
        SendEditorState(computer, actor);
    }

    private void SendSessionDelta(
        Entity<RMCCameraComputerComponent> computer,
        EntityUid actor,
        CameraViewerSession session)
    {
        if (session.LastSentRevision == 0 || session.LastSentRevision > session.Revision)
        {
            SendSessionSnapshot(computer, actor, session);
            return;
        }

        if (session.LastSentRevision == session.Revision)
        {
            SendSessionGeometry(computer.Owner, actor, session);
            return;
        }

        _userInterface.ServerSendUiMessage(
            computer.Owner,
            RMCCameraUiKey.Key,
            new CameraSessionDeltaMessage(
                session.Id,
                session.LastSentRevision,
                session.Revision,
                BuildSessionDirectory(session)),
            actor);
        session.LastSentRevision = session.Revision;
        SendSessionGeometry(computer.Owner, actor, session);
        SendEditorState(computer, actor);
    }

    private void SendSessionGeometry(EntityUid computer, EntityUid actor, CameraViewerSession session)
    {
        if ((session.Capabilities & CameraSessionCapabilities.Map) == 0
            || session.LastSentMarkerRevision == _cameraNetworks.MarkerRevision
            && session.LastSentGeometryNetwork == session.ActiveNetwork)
        {
            return;
        }

        _userInterface.ServerSendUiMessage(
            computer,
            RMCCameraUiKey.Key,
            new CameraSessionGeometryMessage(
                session.Id,
                GetNetEntity(session.ActiveNetwork),
                _cameraNetworks.MarkerRevision,
                BuildSessionGeometry(session)),
            actor);
        session.LastSentMarkerRevision = _cameraNetworks.MarkerRevision;
        session.LastSentGeometryNetwork = session.ActiveNetwork;
    }

    private void SendEditorState(Entity<RMCCameraComputerComponent> computer, EntityUid actor)
    {
        var enabled = _configuration.GetCVar(CCVars.CMUCameraEditorEnabled);
        _userInterface.ServerSendUiMessage(
            computer.Owner,
            RMCCameraUiKey.Key,
            new RMCCameraEditorStateBuiMsg(
                enabled,
                enabled ? BuildEditorState(computer) : new RMCCameraNetworkEditorUiState(0, [], [])),
            actor);
    }

    protected override void OnComputerUiClosed(Entity<RMCCameraComputerComponent> computer, EntityUid actor)
    {
        if (_actorQuery.TryComp(actor, out var actorComponent))
            _cameraSessions.CloseSession(actorComponent.PlayerSession, computer.Owner);
    }

    protected override void OnComputerUiOpened(Entity<RMCCameraComputerComponent> computer, EntityUid actor)
    {
        if (!_actorQuery.TryComp(actor, out var actorComponent))
            return;

        var capabilities = CameraSessionCapabilities.Browse | CameraSessionCapabilities.LiveView;
        if (_configuration.GetCVar(CCVars.CMUCameraMapEnabled))
            capabilities |= CameraSessionCapabilities.Map;

        var session = _cameraSessions.OpenSession(
            actorComponent.PlayerSession,
            actor,
            computer.Owner,
            capabilities);
        if (session == null)
            return;

        var visibleNetworks = GetVisibleNetworkEntities(computer.Owner);
        if (session.ActiveNetwork is not { } active || !visibleNetworks.Contains(active))
        {
            var first = visibleNetworks.FirstOrDefault();
            if (first != EntityUid.Invalid)
                _cameraSessions.SelectNetwork(session.Id, first);
        }

        SendSessionSnapshot(computer, actor, session);
    }

    protected override bool CanUseComputer(Entity<RMCCameraComputerComponent> computer, EntityUid actor)
    {
        return !TerminatingOrDeleted(actor) &&
            _actorQuery.TryComp(actor, out var actorComponent) &&
            actorComponent.PlayerSession.AttachedEntity == actor &&
            _userInterface.IsUiOpen(computer.Owner, RMCCameraUiKey.Key, actor) &&
            _accessReader.IsAllowed(actor, computer.Owner);
    }

    protected override void RefreshFor(Entity<RMCCameraComputerComponent> computer, EntityUid actor)
    {
        if (!TryGetSession(computer, actor, out var session))
            return;

        SendSessionSnapshot(computer, actor, session);
    }

    protected override void DisconnectFor(Entity<RMCCameraComputerComponent> computer, EntityUid actor)
    {
        if (TryGetSession(computer, actor, out var session))
            _cameraSessions.SelectCamera(session.Id, null);
    }

    protected override void SelectRelativeCamera(
        Entity<RMCCameraComputerComponent> computer,
        EntityUid actor,
        int offset)
    {
        if (!TryGetSession(computer, actor, out var session)
            || session.ActiveNetwork is not { } activeNetwork)
        {
            return;
        }

        var cameras = session.AuthorizedCameras
            .Where(camera => _cameraNetworks.IsMemberOfNetwork(camera, activeNetwork))
            .Where(camera => TryComp(camera, out RMCCameraComponent? _))
            .OrderBy(camera => GetCameraName(camera, Comp<RMCCameraComponent>(camera)), StringComparer.Ordinal)
            .ThenBy(camera => camera.Id)
            .ToList();
        if (cameras.Count == 0)
            return;

        var index = session.SelectedCamera is { } selected ? cameras.IndexOf(selected) + offset : 0;
        if (index < 0)
            index = cameras.Count - 1;
        else if (index >= cameras.Count)
            index = 0;

        _cameraSessions.SelectCamera(session.Id, cameras[index]);
    }

    private bool TryGetSession(
        Entity<RMCCameraComputerComponent> computer,
        EntityUid actor,
        out CameraViewerSession session)
    {
        if (CanUseComputer(computer, actor)
            && _actorQuery.TryComp(actor, out var actorComponent)
            && _cameraSessions.TryGetSession(actorComponent.PlayerSession, computer.Owner, out session))
        {
            return true;
        }

        session = default!;
        return false;
    }
}
