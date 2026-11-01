using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.Weapons.ATGM;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Weapons.ATGM;

/// <summary>
///     Streams the local spotter's cursor to the server while they guide an A.T.G.M. missile.
///     Throttled and delta-gated, with a heartbeat so the server's latest point stays fresh (a locked missile
///     ignores a stale cursor) and an update lost in transit is re-sent; the server validates every update.
/// </summary>
public sealed partial class CMUATGMGuidanceInputSystem : EntitySystem
{
    private static readonly TimeSpan SendInterval = TimeSpan.FromMilliseconds(75);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMilliseconds(250);
    private const float MinCursorMoveSquared = 0.05f * 0.05f;

    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private CMU3DLiveSceneSystem _firstPerson = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private TimeSpan _nextSend;
    private MapCoordinates _lastSent = MapCoordinates.Nullspace;
    private TimeSpan _lastSentTime;

    public override void Update(float frameTime)
    {
        if (!_timing.IsFirstTimePredicted || _timing.CurTime < _nextSend)
            return;

        if (_player.LocalEntity is not { } local || !IsGuiding(local))
        {
            _lastSent = MapCoordinates.Nullspace;
            return;
        }

        // Match GunSystem: in first-person 3D the perspective ray, not the 2D eye, defines the aim point.
        MapCoordinates cursor;
        if (_firstPerson.TryFirstPersonAim(out var firstPersonAim, out _))
            cursor = firstPersonAim;
        else if (_input.MouseScreenPosition.IsValid)
            cursor = _eye.PixelToMap(_input.MouseScreenPosition);
        else
            return;

        if (cursor.MapId == MapId.Nullspace)
            return;

        var now = _timing.CurTime;
        if (cursor.MapId == _lastSent.MapId &&
            Vector2.DistanceSquared(cursor.Position, _lastSent.Position) < MinCursorMoveSquared &&
            now - _lastSentTime < HeartbeatInterval)
        {
            return;
        }

        _nextSend = now + SendInterval;
        _lastSent = cursor;
        _lastSentTime = now;
        RaiseNetworkEvent(new CMUATGMGuidanceEvent(GetNetCoordinates(_transform.ToCoordinates(cursor))));
    }

    private bool IsGuiding(EntityUid local)
    {
        var query = EntityQueryEnumerator<CMUGuidedMissileComponent>();
        while (query.MoveNext(out _, out var missile))
        {
            if (missile.Guider == local && _hands.IsHolding(local, missile.Finder))
                return true;
        }

        return false;
    }
}
