using Content.Server.Chat.Managers;
using Content.Server.Database.Migrations.Postgres;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Station.Systems;
using Content.Shared.Chat;
using Content.Shared.GameTicking.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components; // CMU14
using Content.Shared.Players;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.GameTicking.Rules;

/// <summary>
/// This handles logic and interactions related to <see cref="RespawnDeadRuleComponent"/>
/// </summary>
public sealed partial class RespawnRuleSystem : GameRuleSystem<RespawnDeadRuleComponent>
{
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private StationSystem _station = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SuicideEvent>(OnSuicide);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_station.GetStations().FirstOrNull() is not { } station)
            return;

        // CMU14: delayed respawns belong to an active rule and to the death that queued them.
        var trackers = EntityQueryEnumerator<RespawnTrackerComponent, GameRuleComponent>();
        while (trackers.MoveNext(out var ruleUid, out var tracker, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(ruleUid, rule))
            {
                tracker.RespawnQueue.Clear();
                tracker.CMUQueuedDeaths.Clear();
                continue;
            }

            foreach (var (player, time) in tracker.RespawnQueue.ToArray())
            {
                if (!tracker.CMUQueuedDeaths.TryGetValue(player, out var death) ||
                    !TryComp<MindComponent>(death.Mind, out var queuedMind) ||
                    queuedMind.UserId != player ||
                    queuedMind.OwnedEntity is { } owned && owned != death.Body ||
                    TryComp<MobStateComponent>(death.Body, out var mob) && mob.CurrentState != MobState.Dead)
                {
                    tracker.RespawnQueue.Remove(player);
                    tracker.CMUQueuedDeaths.Remove(player);
                    continue;
                }

                if (_timing.CurTime < time || !_playerManager.TryGetSessionById(player, out var session))
                    continue;

                tracker.RespawnQueue.Remove(player);
                tracker.CMUQueuedDeaths.Remove(player);
                if (session.GetMind() != death.Mind)
                    continue;

                if (tracker.DeleteBody && !TerminatingOrDeleted(death.Body))
                    QueueDel(death.Body);
                GameTicker.MakeJoinGame(session, station, silent: true);
            }
        }
        // CMU14 end
    }

    private void OnSuicide(SuicideEvent ev)
    {
        if (!TryComp<ActorComponent>(ev.Victim, out var actor))
           return;

        var query = EntityQueryEnumerator<RespawnTrackerComponent>();
        while (query.MoveNext(out _, out var respawn))
        {
            if (respawn.Players.Remove(actor.PlayerSession.UserId))
                QueueDel(ev.Victim);
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (!TryComp<ActorComponent>(args.Target, out var actor))
            return;

        var query = EntityQueryEnumerator<RespawnDeadRuleComponent, RespawnTrackerComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var respawnRule, out  var tracker, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(uid, rule))
                continue;

            if (respawnRule.AlwaysRespawnDead)
                AddToTracker(actor.PlayerSession.UserId, (uid, tracker));
            if (RespawnPlayer((args.Target, actor), (uid, tracker)))
                break;
        }
    }

    /// <summary>
    /// Attempts to directly respawn a player, skipping the lobby screen.
    /// </summary>
    public bool RespawnPlayer(Entity<ActorComponent> player, Entity<RespawnTrackerComponent> respawnTracker)
    {
        if (!respawnTracker.Comp.Players.Contains(player.Comp.PlayerSession.UserId) || respawnTracker.Comp.RespawnQueue.ContainsKey(player.Comp.PlayerSession.UserId))
            return false;

        if (respawnTracker.Comp.RespawnDelay == TimeSpan.Zero)
        {
            if (_station.GetStations().FirstOrNull() is not { } station)
                return false;

            if (respawnTracker.Comp.DeleteBody)
                QueueDel(player);
            GameTicker.MakeJoinGame(player.Comp.PlayerSession, station, silent: true);
            return false;
        }

        var msg = Loc.GetString("rule-respawn-in-seconds", ("second", respawnTracker.Comp.RespawnDelay.TotalSeconds));
        var wrappedMsg = Loc.GetString("chat-manager-server-wrap-message", ("message", msg));
        _chatManager.ChatMessageToOne(ChatChannel.Server, msg, wrappedMsg, respawnTracker, false, player.Comp.PlayerSession.Channel, Color.LimeGreen);

        // CMU14: retain identity instead of deleting whichever body the player owns when the timer expires.
        if (player.Comp.PlayerSession.GetMind() is not { } mind)
            return false;
        respawnTracker.Comp.CMUQueuedDeaths[player.Comp.PlayerSession.UserId] = (mind, player.Owner);
        respawnTracker.Comp.RespawnQueue[player.Comp.PlayerSession.UserId] = _timing.CurTime + respawnTracker.Comp.RespawnDelay;
        // CMU14 end

        return true;
    }

    /// <summary>
    /// Adds a given player to the respawn tracker, ensuring that they are respawned if they die.
    /// </summary>
    public void AddToTracker(Entity<ActorComponent?> player, Entity<RespawnTrackerComponent?> respawnTracker)
    {
        if (!Resolve(respawnTracker, ref respawnTracker.Comp) || !Resolve(player, ref player.Comp, false))
            return;

        AddToTracker(player.Comp.PlayerSession.UserId, (respawnTracker, respawnTracker.Comp));
    }

    /// <summary>
    /// Adds a given player to the respawn tracker, ensuring that they are respawned if they die.
    /// </summary>
    public void AddToTracker(NetUserId id, Entity<RespawnTrackerComponent> tracker)
    {
        tracker.Comp.Players.Add(id);
    }
}
