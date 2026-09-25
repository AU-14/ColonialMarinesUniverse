using System.Linq;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Lobby;
using Content.Shared.GameTicking;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Lobby;

public sealed partial class LobbyLineupSystem
{
    [Dependency] private IRobustRandom _random = default!;
    private TimeSpan _nextShow;
    private TimeSpan _nextAutomaticShow;
    private LobbyPartyShow _automaticShow;

    private void ResetShows()
    {
        _nextShow = TimeSpan.Zero;
        _nextAutomaticShow = _timing.RealTime + TimeSpan.FromSeconds(20);
        _automaticShow = LobbyPartyShow.Flyby;
    }

    private void OnShow(LobbyPartyShowRequest ev, EntitySessionEventArgs args)
    {
        TryShow(args.SenderSession, ev.Show);
    }

    public bool TryShow(ICommonSession session, LobbyPartyShow show)
    {
        if (!LobbyPartySettings.IsShowEnabled(_configuration, show) || _ticker.RunLevel != GameRunLevel.PreRoundLobby ||
            !Enum.IsDefined(show) || session.Status != SessionStatus.InGame || !session.Channel.IsConnected ||
            _ticker.PlayerGameStatuses.GetValueOrDefault(session.UserId) != PlayerGameStatus.ReadyToPlay ||
            _timing.RealTime < _nextShow)
            return false;

        var lineup = _ticker.GetLobbyLineup();
        if (!lineup.Any(entry => entry.UserId == session.UserId))
            return false;
        BroadcastShow(show, lineup, false);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_ticker.RunLevel != GameRunLevel.PreRoundLobby ||
            _timing.RealTime < _nextAutomaticShow || _timing.RealTime < _nextShow)
            return;

        if (!LobbyPartySettings.TryNextShow(_configuration, _automaticShow, out var show))
            return;

        // Readiness is event-driven in GameTicker; only take a snapshot when a show is due.
        _nextAutomaticShow = _timing.RealTime + TimeSpan.FromSeconds(5);
        var lineup = _ticker.GetLobbyLineup();
        if (lineup.Count == 0)
            return;
        BroadcastShow(show, lineup, true);
        _automaticShow = show == LobbyPartyShow.Flyby ? LobbyPartyShow.Parade : LobbyPartyShow.Flyby;
    }

    private void BroadcastShow(LobbyPartyShow show, IReadOnlyList<LobbyLineupEntry> lineup, bool automatic)
    {
        _nextShow = _timing.RealTime + TimeSpan.FromSeconds(LobbyPartyShowEvent.Cooldown);
        _nextAutomaticShow = _timing.RealTime + TimeSpan.FromSeconds(LobbyPartyShowEvent.AutomaticInterval);
        RaiseNetworkEvent(new LobbyPartyShowEvent(show, _random.Next(),
            lineup.Select(entry => entry.UserId).ToList(), automatic));
    }
}
