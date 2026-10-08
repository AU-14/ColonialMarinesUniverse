using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.CMU14.ThreeD;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.ThreeD;

/// <summary>
/// Scoped replication for supported first-person maps and the administrator scene workbench.
/// Each live map in the rendered stack gets one origin, following the actor without the 2D visual offset.
/// </summary>
public sealed partial class CMU3DViewSystem : EntitySystem
{
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedEyeSystem _eyes = default!;
    [Dependency] private ViewSubscriberSystem _subscribers = default!;

    private sealed class ViewState(float radius)
    {
        public float Radius = radius;
        public readonly Dictionary<EntityUid, EntityUid> Probes = [];
    }

    private readonly Dictionary<ICommonSession, ViewState> _views = [];
    private readonly HashSet<EntityUid> _wanted = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CMU3DViewRequest>(OnRequest);
        _admins.OnPermsChanged += OnPermissionsChanged;
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    private void OnRequest(CMU3DViewRequest message, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!message.Enabled || !float.IsFinite(message.Radius) ||
            !CanUseView(session))
        {
            Close(session);
            return;
        }
        var radius = Math.Clamp(message.Radius, 4, CMU3DViewRequest.MaximumRadius);
        if (_views.TryGetValue(session, out var state))
            state.Radius = radius;
        else
            _views.Add(session, new ViewState(radius));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var (session, state) in _views.ToArray())
        {
            if (session.Status != SessionStatus.InGame || !CanUseView(session) ||
                session.AttachedEntity is not { } actor || TerminatingOrDeleted(actor) ||
                !TryComp(actor, out TransformComponent? actorTransform) || actorTransform.MapUid is not { } actorMap)
            {
                Close(session);
                continue;
            }

            var position = _transform.GetWorldPosition(actorTransform);
            var depth = Depth(actorMap);
            _wanted.Clear();
            foreach (var map in _zLevels.GetAllNetworkMaps(actorMap))
            {
                if (Math.Abs((long) Depth(map) - depth) <= CMU3DViewRequest.MaximumDepth &&
                    (_admins.HasAdminFlag(session, AdminFlags.Debug) || HasComp<CMU3DMapComponent>(map)))
                    _wanted.Add(map);
            }
            foreach (var (map, probe) in state.Probes.ToArray())
            {
                if (_wanted.Contains(map) && !TerminatingOrDeleted(probe))
                    continue;
                ReleaseProbe(session, probe);
                state.Probes.Remove(map);
            }

            // PVS range is a diameter. Prefetch beyond the rendered bounds to cover normal walking.
            var scale = 2 * (state.Radius + CMU3DViewRequest.PrefetchMargin) /
                Math.Max(1, _config.GetCVar(Robust.Shared.CVars.NetMaxUpdateRange));
            TryComp(actor, out EyeComponent? actorEye);
            foreach (var map in _wanted)
            {
                if (!state.Probes.TryGetValue(map, out var probe))
                {
                    probe = Spawn("CMUZLevelEye", new EntityCoordinates(map, position));
                    Transform(probe).GridTraversal = false;
                    AddComp<CMU3DViewProbeComponent>(probe);
                    EnsureComp<EyeComponent>(probe);
                    state.Probes.Add(map, probe);
                    _subscribers.AddViewSubscriber(probe, session);
                }
                _transform.SetCoordinates(probe, new EntityCoordinates(map, position));
                _eyes.SetPvsScale(probe, scale);
                // Preserve the actor's visibility layers, including changes while the view is open.
                _eyes.SetVisibilityMask(probe, actorEye?.VisibilityMask ?? EyeComponent.DefaultVisibilityMask);
            }
        }
    }

    private int Depth(EntityUid map) => TryComp(map, out CMUZLevelMapComponent? level) ? level.Depth : 0;

    private bool CanUseView(ICommonSession session) =>
        _admins.HasAdminFlag(session, AdminFlags.Debug) ||
        TryComp(session.AttachedEntity, out TransformComponent? transform) &&
        transform.MapUid is { } map && HasComp<CMU3DMapComponent>(map);

    private void OnPermissionsChanged(AdminPermsChangedEventArgs args)
    {
        if (!CanUseView(args.Player))
            Close(args.Player);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.InGame)
            Close(args.Session);
    }

    private void ReleaseProbe(ICommonSession session, EntityUid probe)
    {
        if (TerminatingOrDeleted(probe))
            return;
        _subscribers.RemoveViewSubscriber(probe, session);
        QueueDel(probe);
    }

    private void Close(ICommonSession session)
    {
        if (!_views.Remove(session, out var state))
            return;
        foreach (var probe in state.Probes.Values)
            ReleaseProbe(session, probe);
    }

    public override void Shutdown()
    {
        _admins.OnPermsChanged -= OnPermissionsChanged;
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
        foreach (var session in _views.Keys.ToArray())
            Close(session);
        base.Shutdown();
    }
}
