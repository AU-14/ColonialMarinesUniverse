using System.Linq;
using Content.Shared.Access.Systems;
using Content.Shared.Camera;
using Content.Shared.GameTicking;
using Content.Server.SurveillanceCamera;
using Robust.Server.GameObjects;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Camera;

/// <summary>
/// Owns viewer-private camera state. A receiver describes authorization; a
/// session describes one player's selection, capabilities, and live
/// view lease for that receiver.
/// </summary>
public sealed class CameraSessionSystem : EntitySystem
{
    private static readonly TimeSpan ValidationInterval = TimeSpan.FromSeconds(0.5);
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly CameraNetworkSystem _networks = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;

    private readonly Dictionary<uint, CameraViewerSession> _sessions = [];
    private readonly Dictionary<(ICommonSession Viewer, EntityUid Receiver), uint> _sessionByKey = [];
    private readonly Dictionary<ICommonSession, HashSet<uint>> _sessionsByViewer = [];
    private readonly Dictionary<EntityUid, HashSet<uint>> _sessionsByReceiver = [];
    private readonly Dictionary<EntityUid, HashSet<uint>> _sessionsBySelectedCamera = [];

    private TimeSpan _nextValidation;
    private uint _nextSessionId = 1;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CameraNetworkReceiverComponent, CameraReceiverChangedEvent>(OnReceiverChanged);
        SubscribeLocalEvent<SurveillanceCameraDeactivateEvent>(OnCameraDeactivated);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextValidation)
            return;

        _nextValidation = _timing.CurTime + ValidationInterval;
        foreach (var (id, session) in _sessions.ToArray())
        {
            if (!Validate(session.Viewer, session.Actor, session.Receiver))
                CloseSession(id);
        }
    }

    public CameraViewerSession? OpenSession(
        ICommonSession viewer,
        EntityUid actor,
        EntityUid receiver,
        CameraSessionCapabilities capabilities)
    {
        if (!Validate(viewer, actor, receiver)
            || !TryComp(receiver, out CameraNetworkReceiverComponent? receiverComponent))
        {
            return null;
        }

        var key = (viewer, receiver);
        if (_sessionByKey.TryGetValue(key, out var existingId))
        {
            var existing = _sessions[existingId];
            existing.Actor = actor;
            existing.Capabilities = capabilities;
            RefreshAuthorization(existing, receiverComponent, notify: false);
            if ((capabilities & CameraSessionCapabilities.LiveView) == 0)
                SetSelection(existing, null);
            return existing;
        }

        var authorized = _networks.GetAccessibleCameras((receiver, receiverComponent));

        var session = new CameraViewerSession(
            _nextSessionId++,
            viewer,
            actor,
            receiver,
            capabilities,
            authorized);

        _sessions.Add(session.Id, session);
        _sessionByKey.Add(key, session.Id);
        AddIndex(_sessionsByViewer, viewer, session.Id);
        AddIndex(_sessionsByReceiver, receiver, session.Id);
        EnsureActiveNetwork(session);

        return session;
    }

    public bool TryGetSession(ICommonSession viewer, EntityUid receiver, out CameraViewerSession session)
    {
        if (_sessionByKey.TryGetValue((viewer, receiver), out var id)
            && _sessions.TryGetValue(id, out var found))
        {
            session = found;
            return true;
        }

        session = default!;
        return false;
    }

    public bool SelectCamera(uint sessionId, EntityUid? camera)
    {
        if (!_sessions.TryGetValue(sessionId, out var session)
            || !Validate(session.Viewer, session.Actor, session.Receiver)
            || (session.Capabilities & CameraSessionCapabilities.LiveView) == 0)
        {
            return false;
        }

        if (camera is { } selected
            && (!_networks.IsAvailable(selected)
                || !session.AuthorizedCameras.Contains(selected)
                || !_networks.CanAccess(session.Receiver, selected)
                || session.ActiveNetwork is { } activeNetwork
                && !_networks.IsMemberOfNetwork(selected, activeNetwork)))
        {
            return false;
        }

        SetSelection(session, camera);
        return true;
    }

    public bool SelectNetwork(uint sessionId, EntityUid network)
    {
        if (!_sessions.TryGetValue(sessionId, out var session)
            || !Validate(session.Viewer, session.Actor, session.Receiver)
            || (session.Capabilities & CameraSessionCapabilities.Browse) == 0
            || !_networks.GetEffectiveNetworkEntities(session.Receiver).Contains(network))
        {
            return false;
        }

        if (session.ActiveNetwork == network)
            return true;

        session.ActiveNetwork = network;
        session.Revision++;
        if (session.SelectedCamera is { } selected && !_networks.IsMemberOfNetwork(selected, network))
            SetSelection(session, null, notify: false);
        RaiseSessionChanged(session);
        return true;
    }

    public IReadOnlyCollection<CameraViewerSession> GetSessions(EntityUid receiver)
    {
        if (!_sessionsByReceiver.TryGetValue(receiver, out var ids))
            return Array.Empty<CameraViewerSession>();

        return ids.Select(id => _sessions[id]).ToArray();
    }

    public IReadOnlyCollection<CameraViewerSession> GetSessionsForCamera(EntityUid camera)
    {
        if (!_sessionsBySelectedCamera.TryGetValue(camera, out var ids))
            return Array.Empty<CameraViewerSession>();

        return ids.Select(id => _sessions.GetValueOrDefault(id))
            .Where(session => session != null)
            .Cast<CameraViewerSession>()
            .ToArray();
    }

    public bool HasActiveViewers(EntityUid camera)
    {
        return _sessionsBySelectedCamera.TryGetValue(camera, out var ids)
            && ids.Any(_sessions.ContainsKey);
    }

    public bool HasActiveSelection(EntityUid receiver)
    {
        return _sessionsByReceiver.TryGetValue(receiver, out var ids)
            && ids.Any(id => _sessions.GetValueOrDefault(id)?.SelectedCamera != null);
    }

    public void CloseSessions(EntityUid receiver)
    {
        if (!_sessionsByReceiver.TryGetValue(receiver, out var ids))
            return;

        foreach (var id in ids.ToArray())
            CloseSession(id);
    }

    public void ClearSelections(EntityUid receiver)
    {
        if (!_sessionsByReceiver.TryGetValue(receiver, out var ids))
            return;

        foreach (var id in ids.ToArray())
        {
            if (_sessions.TryGetValue(id, out var session))
                SetSelection(session, null);
        }
    }

    public void CloseSession(ICommonSession viewer, EntityUid receiver)
    {
        if (_sessionByKey.TryGetValue((viewer, receiver), out var id))
            CloseSession(id);
    }

    private void CloseSession(uint id)
    {
        if (!_sessions.Remove(id, out var session))
            return;

        SetSelection(session, null, notify: false);
        _sessionByKey.Remove((session.Viewer, session.Receiver));
        RemoveIndex(_sessionsByViewer, session.Viewer, id);
        RemoveIndex(_sessionsByReceiver, session.Receiver, id);
        RaiseSessionChanged(session);
    }

    private void OnReceiverChanged(Entity<CameraNetworkReceiverComponent> receiver, ref CameraReceiverChangedEvent args)
    {
        if (!_sessionsByReceiver.TryGetValue(receiver.Owner, out var ids))
            return;

        foreach (var id in ids.ToArray())
        {
            if (_sessions.TryGetValue(id, out var session))
            {
                switch (args.Kind)
                {
                    case CameraReceiverChangeKind.Marker:
                        break;
                    case CameraReceiverChangeKind.Directory:
                        RefreshDirectory(session);
                        break;
                    default:
                        RefreshAuthorization(session, receiver.Comp, notify: false);
                        break;
                }

                RaiseSessionChanged(session);
            }
        }
    }

    private void RefreshAuthorization(
        CameraViewerSession session,
        CameraNetworkReceiverComponent receiver,
        bool notify)
    {
        var authorized = _networks.GetAccessibleCameras((session.Receiver, receiver));
        session.AuthorizedCameras = authorized;
        EnsureActiveNetwork(session);
        session.Revision++;
        if (session.SelectedCamera is { } selected
            && (!_networks.IsAvailable(selected)
                || !authorized.Contains(selected)
                || session.ActiveNetwork is { } activeNetwork
                && !_networks.IsMemberOfNetwork(selected, activeNetwork)))
            SetSelection(session, null, notify);
    }

    private void RefreshDirectory(CameraViewerSession session)
    {
        if (session.SelectedCamera is { } selected && !_networks.IsAvailable(selected))
        {
            SetSelection(session, null, notify: false);
            return;
        }

        session.Revision++;
    }

    private void EnsureActiveNetwork(CameraViewerSession session)
    {
        var networks = _networks.GetEffectiveNetworkEntities(session.Receiver);
        if (session.ActiveNetwork is { } active && networks.Contains(active))
            return;

        session.ActiveNetwork = networks.OrderBy(network => network.Id).FirstOrDefault();
        if (session.ActiveNetwork == EntityUid.Invalid)
            session.ActiveNetwork = null;
    }

    private void SetSelection(CameraViewerSession session, EntityUid? camera, bool notify = true)
    {
        if (session.SelectedCamera == camera)
            return;

        var previous = session.SelectedCamera;
        if (previous is { } previousCamera)
        {
            RemoveIndex(_sessionsBySelectedCamera, previousCamera, session.Id);
            _viewSubscriber.RemoveViewSubscriber(previousCamera, session.Viewer);
        }

        session.SelectedCamera = camera;
        session.Revision++;

        if (camera is not { } selected)
        {
            RaiseSelectionChanged(previous, null);
            if (notify)
                RaiseSessionChanged(session);
            return;
        }

        AddIndex(_sessionsBySelectedCamera, selected, session.Id);
        _viewSubscriber.AddViewSubscriber(selected, session.Viewer);
        RaiseSelectionChanged(previous, selected);
        if (notify)
            RaiseSessionChanged(session);
    }

    private void RaiseSelectionChanged(EntityUid? previous, EntityUid? selected)
    {
        if (previous is { } oldCamera)
        {
            var oldEvent = new CameraSessionSelectionChangedEvent();
            RaiseLocalEvent(oldCamera, ref oldEvent);
        }

        if (selected is { } newCamera && selected != previous)
        {
            var newEvent = new CameraSessionSelectionChangedEvent();
            RaiseLocalEvent(newCamera, ref newEvent);
        }
    }

    private void OnCameraDeactivated(SurveillanceCameraDeactivateEvent args)
    {
        if (!_sessionsBySelectedCamera.TryGetValue(args.Camera, out var ids))
            return;

        foreach (var id in ids.ToArray())
        {
            if (_sessions.TryGetValue(id, out var session))
                SetSelection(session, null);
        }
    }

    private void RaiseSessionChanged(CameraViewerSession session)
    {
        var ev = new CameraSessionChangedEvent(session.Actor);
        RaiseLocalEvent(session.Receiver, ref ev);
    }

    private void OnEntityTerminating(ref EntityTerminatingEvent args)
    {
        var entity = args.Entity.Owner;

        if (_sessionsByReceiver.TryGetValue(entity, out var receiverSessions))
        {
            foreach (var id in receiverSessions.ToArray())
                CloseSession(id);
        }

        if (_sessionsBySelectedCamera.TryGetValue(entity, out var selectedSessions))
        {
            foreach (var id in selectedSessions.ToArray())
            {
                if (_sessions.TryGetValue(id, out var session))
                    SetSelection(session, null);
            }
        }

        if (TryComp(entity, out ActorComponent? actor)
            && _sessionsByViewer.TryGetValue(actor.PlayerSession, out var viewerSessions))
        {
            foreach (var id in viewerSessions.ToArray())
                CloseSession(id);
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        foreach (var id in _sessions.Keys.ToArray())
            CloseSession(id);

        _sessions.Clear();
        _sessionByKey.Clear();
        _sessionsByViewer.Clear();
        _sessionsByReceiver.Clear();
        _sessionsBySelectedCamera.Clear();
    }

    private bool Validate(ICommonSession viewer, EntityUid actor, EntityUid receiver)
    {
        return viewer.Status is not (SessionStatus.Disconnected or SessionStatus.Zombie)
            && viewer.AttachedEntity == actor
            && !TerminatingOrDeleted(actor)
            && !TerminatingOrDeleted(receiver)
            && TryComp(actor, out ActorComponent? actorComponent)
            && ReferenceEquals(actorComponent.PlayerSession, viewer)
            && HasComp<CameraNetworkReceiverComponent>(receiver)
            && _accessReader.IsAllowed(actor, receiver);
    }

    private static void AddIndex<TKey>(Dictionary<TKey, HashSet<uint>> index, TKey key, uint id)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var ids))
        {
            ids = [];
            index.Add(key, ids);
        }

        ids.Add(id);
    }

    private static void RemoveIndex<TKey>(Dictionary<TKey, HashSet<uint>> index, TKey key, uint id)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var ids))
            return;

        ids.Remove(id);
        if (ids.Count == 0)
            index.Remove(key);
    }
}

[ByRefEvent]
public record struct CameraSessionSelectionChangedEvent;

public sealed class CameraViewerSession(
    uint id,
    ICommonSession viewer,
    EntityUid actor,
    EntityUid receiver,
    CameraSessionCapabilities capabilities,
    HashSet<EntityUid> authorizedCameras)
{
    public uint Id { get; } = id;
    public ICommonSession Viewer { get; } = viewer;
    public EntityUid Actor { get; internal set; } = actor;
    public EntityUid Receiver { get; } = receiver;
    public CameraSessionCapabilities Capabilities { get; internal set; } = capabilities;
    public HashSet<EntityUid> AuthorizedCameras { get; internal set; } = authorizedCameras;
    public EntityUid? SelectedCamera { get; internal set; }
    public EntityUid? ActiveNetwork { get; internal set; }
    public ulong Revision { get; internal set; } = 1;
    public ulong LastSentRevision { get; internal set; }
    public ulong LastSentMarkerRevision { get; internal set; }
    public EntityUid? LastSentGeometryNetwork { get; internal set; }
}
