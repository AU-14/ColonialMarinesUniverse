using Content.Shared.CMU14.Vehicle.Open;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Tells the server how this client's camera is turned when it changes, and keeps the same on the
/// player here, so predicted shots from a mounted gun start where the server's will.
/// </summary>
public sealed class CMUViewRotationSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan SendEvery = TimeSpan.FromSeconds(0.2);

    private Angle? _sent;
    private EntityUid? _sentFor;
    private TimeSpan _nextSend;

    public override void FrameUpdate(float frameTime)
    {
        if (_player.LocalEntity is not { } player)
            return;

        var rotation = _eye.CurrentEye.Rotation.Reduced();
        EnsureComp<CMUViewRotationComponent>(player).Rotation = rotation;

        if (_sentFor == player && _sent is { } sent && sent.EqualsApprox(rotation, 0.01))
            return;

        if (_timing.RealTime < _nextSend)
            return;

        _nextSend = _timing.RealTime + SendEvery;
        _sent = rotation;
        _sentFor = player;
        RaiseNetworkEvent(new CMUViewRotationEvent(rotation));
    }
}
