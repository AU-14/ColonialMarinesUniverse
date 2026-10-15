using Content.Shared.CMU14.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.GameTicking;

/// <summary>
/// Tracks which players chose to hide their username from the round-end summary.
/// The choice is a client CVar, so it's cached while they're connected; that way it still applies
/// if they disconnect before the round ends.
/// </summary>
public sealed partial class CMURoundEndAnonymitySystem : EntitySystem
{
    [Dependency] private INetConfigurationManager _netConfig = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

    private readonly Dictionary<NetUserId, bool> _hidden = new();
    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();
        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus is SessionStatus.Connected or SessionStatus.InGame)
            Refresh(args.Session);
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + RefreshInterval;
        foreach (var session in _player.Sessions)
        {
            Refresh(session);
        }
    }

    private void Refresh(ICommonSession session)
    {
        if (session.Status is not (SessionStatus.Connected or SessionStatus.InGame))
            return;

        _hidden[session.UserId] = _netConfig.GetClientCVar(session.Channel, AU14CCVars.HideRoundEndUsername);
    }

    /// <summary>Whether this player asked for their username to be hidden at round end.</summary>
    public bool IsHidden(NetUserId? userId)
    {
        if (userId is not { } id)
            return false;

        if (_player.TryGetSessionById(id, out var session))
            Refresh(session);

        return _hidden.GetValueOrDefault(id);
    }
}
