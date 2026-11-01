using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Players.JobWhitelist;
using Content.Shared.Chat;
using Content.Shared.CMU14.Yautja;
using Content.Shared.GameTicking;
using Robust.Server.Audio;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Yautja;

// hunts used to only start once the round was InRound, so whitelisted players had to sit in the
// lobby and late join. now the hunt is decided while the lobby is still open, whitelisted players
// can ready up for it, and the rule pulls them out of the round-start pool and spawns them on the ship
public sealed partial class YautjaPredatorRoundSystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private IPlayerManager _players = default!;

    private static readonly SoundSpecifier HuntNoticeSound = new SoundCollectionSpecifier("CMUYautjaElderOverseer");

    private readonly HashSet<NetUserId> _huntReady = new();
    private readonly List<ICommonSession> _roundStartHunters = new();
    private int _lobbyRolledRoundId;
    private int _lobbyHuntRoundId;
    private bool _huntWasActive;

    public bool HuntActive => IsHuntActive();

    private void InitializeLobby()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<IsRoleAllowedEvent>(OnIsRoleAllowed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        TryRollLobbyHunt();

        // admins can also start/stop it from the clan hall, so just watch for the edge
        var active = IsHuntActive();
        if (active == _huntWasActive)
            return;

        _huntWasActive = active;
        if (!active)
            _huntReady.Clear();

        SendLobbyStatusToAll();
        if (active)
            NotifyWhitelistedOfHunt();
    }

    private void TryRollLobbyHunt()
    {
        if (!_randomEnabled ||
            GameTicker.DummyTicker ||
            GameTicker.RunLevel != GameRunLevel.PreRoundLobby ||
            GameTicker.RoundId <= 0 ||
            _lobbyRolledRoundId == GameTicker.RoundId)
        {
            return;
        }

        _lobbyRolledRoundId = GameTicker.RoundId;
        if (!_randomSchedule.CountRound(GameTicker.RoundId) ||
            _lastRandomAttemptRoundId == GameTicker.RoundId)
        {
            return;
        }

        _lastRandomAttemptRoundId = GameTicker.RoundId;
        if (TryInitializePredatorRound(out var message))
            _lobbyHuntRoundId = GameTicker.RoundId;
        else
            _sawmill.Warning($"Automatic Yautja hunt queueing failed: {message}");

        ScheduleNextRandomHunt();
    }

    private bool TryQueueLobbyHunt(out string message)
    {
        if (IsHuntActive())
        {
            message = Loc.GetString("cmu-yautja-admin-editor-hunt-already-initialized");
            return true;
        }

        // added, not started. StartGamePresetRules starts every added rule at round start,
        // before players spawn, so RulePlayerSpawning sees it like any preset rule
        GameTicker.AddGameRule(CMUYautjaPredatorRoundPrototype);
        _lobbyHuntRoundId = GameTicker.RoundId;
        message = Loc.GetString("cmu-yautja-admin-editor-hunt-queued");
        return true;
    }

    private bool IsHuntActive()
    {
        var query = QueryAllRules();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (comp.ModePredator && GameTicker.IsGameRuleAdded(uid, gameRule))
                return true;
        }

        return false;
    }

    public bool CanUseHuntWhitelist(ICommonSession session)
    {
        return _jobWhitelist.IsAllowed(session, "CMUYautjaHunter");
    }

    public bool TrySetHuntReady(ICommonSession session, bool ready, out string message)
    {
        if (GameTicker.RunLevel != GameRunLevel.PreRoundLobby)
        {
            message = Loc.GetString("cmu-yautja-hunt-ready-not-lobby");
            return false;
        }

        if (ready && !IsHuntActive())
        {
            message = Loc.GetString("cmu-yautja-hunt-ready-no-hunt");
            return false;
        }

        if (ready && !CanUseHuntWhitelist(session))
        {
            message = Loc.GetString("cmu-yautja-hunt-ready-not-whitelisted");
            return false;
        }

        if (ready)
        {
            _huntReady.Add(session.UserId);
            // readying for the hunt is a ready up, you end up in the round-start pool either way
            GameTicker.ToggleReady(session, true);
        }
        else
        {
            _huntReady.Remove(session.UserId);
        }

        SendLobbyStatus(session);
        message = Loc.GetString(ready ? "cmu-yautja-hunt-ready-on" : "cmu-yautja-hunt-ready-off");
        return true;
    }

    // called from OnRulePlayerSpawning once the ship is loaded
    private void PullRoundStartHunters(Entity<YautjaPredatorRoundComponent> rule, RulePlayerSpawningEvent ev)
    {
        _roundStartHunters.Clear();
        if (_huntReady.Count == 0)
            return;

        var candidates = ev.PlayerPool
            .Where(s => _huntReady.Contains(s.UserId) && CanUseHuntWhitelist(s))
            .ToList();
        _huntReady.Clear();

        _random.Shuffle(candidates);

        // leaders/ancients don't use a slot (cmss13 does the same), so take them first
        var ordinarySlots = rule.Comp.Slots;
        foreach (var session in candidates.OrderByDescending(s => GetRankSpawnPolicy(ResolveRankForSession(s)).BypassSlotCap))
        {
            if (!GetRankSpawnPolicy(ResolveRankForSession(session)).BypassSlotCap)
            {
                if (ordinarySlots <= 0)
                {
                    _chat.DispatchServerMessage(session, Loc.GetString("cmu-yautja-hunt-ready-full"));
                    continue;
                }

                ordinarySlots--;
            }

            ev.PlayerPool.Remove(session);
            _roundStartHunters.Add(session);
        }
    }

    private void SpawnRoundStartHunters()
    {
        if (_roundStartHunters.Count == 0)
            return;

        if (!TryGetActivePredatorRule(out var rule))
        {
            _roundStartHunters.Clear();
            return;
        }

        var station = GetPredatorStations(rule.Comp.PredatorJob, rule.Comp.HunterShipMap).FirstOrDefault();
        foreach (var session in _roundStartHunters)
        {
            if (session.Status != SessionStatus.InGame || GameTicker.UserHasJoinedGame(session))
                continue;

            // same path as the Join Hunt button, so slots/rank/profile all go through the checks we already trust
            GameTicker.MakeJoinGame(session, station, rule.Comp.PredatorJob);
        }

        _roundStartHunters.Clear();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _huntReady.Clear();
        _roundStartHunters.Clear();
    }

    private void SendLobbyStatusToAll()
    {
        foreach (var session in _players.Sessions)
        {
            SendLobbyStatus(session);
        }
    }

    private void SendLobbyStatus(ICommonSession session)
    {
        RaiseNetworkEvent(new YautjaHuntLobbyStatusEvent(
                IsHuntActive(),
                _huntReady.Contains(session.UserId),
                IsAncientSlotEligible(session)),
            session);
    }

    // cmss13 only told ghosts ("This is a PREDATOR ROUND!") and pinged discord. we also tell
    // whitelisted players sitting in the lobby, since they're the ones who need to ready up
    private void NotifyWhitelistedOfHunt()
    {
        var lobby = GameTicker.RunLevel == GameRunLevel.PreRoundLobby;
        foreach (var session in _players.Sessions)
        {
            if (session.Status != SessionStatus.InGame || !CanUseHuntWhitelist(session))
                continue;

            // already playing a mob, they'll hear about it in game
            if (!lobby && session.AttachedEntity is { } attached && !HasComp<Content.Shared.Ghost.Components.GhostComponent>(attached))
                continue;

            var text = Loc.GetString(lobby ? "cmu-yautja-hunt-notice-lobby" : "cmu-yautja-hunt-notice-round");
            _chat.ChatMessageToOne(ChatChannel.Server, text, text, default, false, session.Channel, Color.FromHex("#ff4040"));
            _audio.PlayGlobal(HuntNoticeSound, Filter.SinglePlayer(session), false, AudioParams.Default.WithVolume(-6f));
        }
    }
}
