using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Follower;
using Content.Shared.Follower.Components;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Components;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    [Dependency] private FighterViewSystem _fighterView = default!;
    [Dependency] private FollowerSystem _spectatorFollowers = default!;
    [Dependency] private IPlayerManager _spectatorPlayers = default!;
    private readonly Dictionary<ICommonSession, SpectatorViews> _spectatorViews = [];
    private readonly HashSet<ICommonSession> _activeSpectators = [];
    private TimeSpan _nextSpectatorUpdate;

    private readonly record struct SpectatorViews(EntityUid Aircraft, EntityUid Camera, EntityUid Exterior)
    {
        public bool Contains(EntityUid uid) => Aircraft == uid || Camera == uid || Exterior == uid;
    }

    private void InitializeSpectators()
    {
        SubscribeNetworkEvent<FighterStopSpectatingEvent>(OnStopSpectating);
        SubscribeLocalEvent<GhostComponent, StoppedFollowingEntityEvent>(OnGhostStoppedFollowing);
    }

    private void OnStopSpectating(FighterStopSpectatingEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user || !HasComp<GhostComponent>(user) ||
            !_fighterView.TryGetView(user, out _, out var spectator) || !spectator ||
            !TryComp(user, out FollowerComponent? follower)) return;

        _spectatorFollowers.StopFollowingEntity(user, follower.Following);
    }

    private void OnGhostStoppedFollowing(Entity<GhostComponent> ghost, ref StoppedFollowingEntityEvent args)
    {
        // Switching follow targets retains the component. Leave that transfer
        // alone; only an actual stop should close subscriptions and return us.
        if (TerminatingOrDeleted(ghost) || HasComp<FollowerComponent>(ghost)) return;
        if (TryComp(ghost, out ActorComponent? actor))
            RemoveSpectatorViews(actor.PlayerSession);

        // The airborne cockpit is a separate map with no way to walk back to
        // the colony. Ground observers already occupy the real map and stay put.
        if (!TryComp(Transform(ghost).GridUid, out FighterAircraftComponent? aircraft) ||
            TerminatingOrDeleted(aircraft.TerrainMap)) return;

        var position = aircraft.Phase == FighterPhase.Holding ? aircraft.Home :
            Vector2.Clamp(aircraft.Position, aircraft.Battlefield.BottomLeft, aircraft.Battlefield.TopRight);
        _transform.SetCoordinates(ghost, new EntityCoordinates(aircraft.TerrainMap, position));
        _transform.AttachToGridOrMap(ghost);
    }

    private void OnFighterRoundCleanup(RoundRestartCleanupEvent ev)
    {
        ClearSpectatorViews();
        _manpadCharts.Clear();
        _manpadRadarMaps.Clear();
        _nextSpectatorUpdate = _nextManpadRadarUpdate = TimeSpan.Zero;
    }

    public override void Shutdown()
    {
        ClearSpectatorViews();
        base.Shutdown();
    }

    private void ClearSpectatorViews()
    {
        foreach (var session in _spectatorViews.Keys.ToArray())
            RemoveSpectatorViews(session);
        _activeSpectators.Clear();
    }

    private void RemoveSpectatorViews(ICommonSession session, SpectatorViews? keep = null)
    {
        if (!_spectatorViews.Remove(session, out var previous)) return;
        foreach (var uid in new[] { previous.Aircraft, previous.Camera, previous.Exterior })
        {
            // Subscriptions are sets, not reference counted. Boarding must retain
            // any camera that the new crew seat already subscribed to.
            if (keep?.Contains(uid) != true)
                _views.RemoveViewSubscriber(uid, session);
        }
    }

    private void UpdateFighterSpectators()
    {
        if (_timing.CurTime < _nextSpectatorUpdate) return;
        _nextSpectatorUpdate = _timing.CurTime + TimeSpan.FromSeconds(.25);
        _activeSpectators.Clear();
        foreach (var session in _spectatorPlayers.Sessions)
        {
            if (!_fighterView.TryGetView(session.AttachedEntity, out var seat, out var spectator) ||
                seat.Comp.Aircraft is not { } aircraft || seat.Comp.Camera is not { } camera ||
                seat.Comp.ExteriorCamera is not { } exterior || TerminatingOrDeleted(aircraft) ||
                TerminatingOrDeleted(camera) || TerminatingOrDeleted(exterior)) continue;

            var views = new SpectatorViews(aircraft, camera, exterior);
            if (!spectator)
            {
                RemoveSpectatorViews(session, views);
                continue;
            }
            _activeSpectators.Add(session);
            if (_spectatorViews.TryGetValue(session, out var previous) && previous == views) continue;
            RemoveSpectatorViews(session, views);
            _spectatorViews[session] = views;
            _views.AddViewSubscriber(aircraft, session);
            _views.AddViewSubscriber(camera, session);
            _views.AddViewSubscriber(exterior, session);
        }
        foreach (var session in _spectatorViews.Keys.ToArray())
        {
            if (!_activeSpectators.Contains(session)) RemoveSpectatorViews(session);
        }
    }
}
