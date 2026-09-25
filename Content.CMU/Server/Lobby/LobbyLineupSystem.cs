using System.Linq;
using Content.Server.GameTicking;
using Content.Shared.CMU14.Lobby;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Lobby;

/// <summary>Transient lobby gestures. No gameplay entities or saved character data are changed.</summary>
public sealed partial class LobbyLineupSystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IConfigurationManager _configuration = default!;

    private readonly Dictionary<NetUserId, TimeSpan> _nextAction = new();
    private readonly Dictionary<string, TimeSpan> _nextRally = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LobbyLineupEmoteRequest>(OnEmote);
        SubscribeNetworkEvent<LobbyPartyShowRequest>(OnShow);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
        Subs.CVar(_configuration, CCVars.LobbyPartyTime, OnPartyTimeChanged);
        Subs.CVar(_configuration, CCVars.LobbyPartyTimeFlyby, OnPartyTimeChanged);
        Subs.CVar(_configuration, CCVars.LobbyPartyTimeParade, OnPartyTimeChanged);
    }

    public override void Shutdown()
    {
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
        base.Shutdown();
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        ResetShows();
        _nextAction.Clear();
        _nextRally.Clear();
    }

    private void OnPartyTimeChanged(bool enabled)
    {
        // A live setting change must not reset the shared cooldown and overlap an existing show.
        _nextAutomaticShow = _timing.RealTime + TimeSpan.FromSeconds(20);
        _nextAction.Clear();
        _nextRally.Clear();
        // Clients retain their previews long enough to play the departure before hiding the panel.
        _ticker.UpdateInfoText();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            _nextAction.Remove(args.Session.UserId);
    }

    private void OnEmote(LobbyLineupEmoteRequest ev, EntitySessionEventArgs args)
    {
        TryEmote(args.SenderSession, ev.Emote);
    }

    public bool TryEmote(ICommonSession session, LobbyLineupEmote emote)
    {
        if (!LobbyPartySettings.IsEnabled(_configuration) ||
            _ticker.RunLevel != GameRunLevel.PreRoundLobby || session.Status != SessionStatus.InGame ||
            !session.Channel.IsConnected || !Enum.IsDefined(emote) ||
            _ticker.PlayerGameStatuses.GetValueOrDefault(session.UserId) != PlayerGameStatus.ReadyToPlay ||
            _timing.RealTime < _nextAction.GetValueOrDefault(session.UserId))
            return false;

        var lineup = _ticker.GetLobbyLineup();
        var sender = lineup.FirstOrDefault(entry => entry.UserId == session.UserId);
        if (sender == null)
            return false;

        var rally = LobbyLineupEmoteEvent.IsTeamEmote(emote);
        if (rally && _timing.RealTime < _nextRally.GetValueOrDefault(sender.SectionId))
            return false;

        _nextAction[session.UserId] = _timing.RealTime + TimeSpan.FromSeconds(LobbyLineupEmoteEvent.ActionCooldown);
        if (rally)
            _nextRally[sender.SectionId] = _timing.RealTime + TimeSpan.FromSeconds(LobbyLineupEmoteEvent.SquadCooldown);

        var participants = rally
            ? lineup.Where(entry => entry.SectionId == sender.SectionId).Select(entry => entry.UserId).ToList()
            : new List<NetUserId> { session.UserId };
        RaiseNetworkEvent(new LobbyLineupEmoteEvent(session.UserId, emote, participants));
        return true;
    }
}
