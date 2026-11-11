using Content.Shared.CMU14.Weapons.Gauss;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Weapons.Gauss;

/// <summary>
///     While the local player charges an M60 gauss gun, keeps the server told where their crosshair is,
///     so an overcharge discharges at it rather than wherever the player happens to be facing.
/// </summary>
public sealed class CMUGaussAimSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(0.1);
    private const float MinMoveSquared = 0.05f;

    private MapCoordinates? _lastSent;
    private TimeSpan _nextSend;

    public override void FrameUpdate(float frameTime)
    {
        if (_player.LocalEntity is not { } player)
            return;

        var now = _timing.RealTime;
        if (now < _nextSend)
            return;

        EntityUid? charging = null;
        foreach (var held in _hands.EnumerateHeld(player))
        {
            if (TryComp(held, out CMUGaussGunComponent? gauss) &&
                gauss.ChargeStart != null &&
                gauss.ChargeUser == player)
            {
                charging = held;
                break;
            }
        }

        if (charging is not { } gun)
        {
            _lastSent = null;
            return;
        }

        var target = _eye.PixelToMap(_input.MouseScreenPosition);
        if (target.MapId == MapId.Nullspace)
            return;

        if (_lastSent is { } last &&
            last.MapId == target.MapId &&
            (last.Position - target.Position).LengthSquared() < MinMoveSquared)
        {
            return;
        }

        _lastSent = target;
        _nextSend = now + SendInterval;
        RaiseNetworkEvent(new CMUGaussAimMessage(GetNetEntity(gun), target));
    }
}
