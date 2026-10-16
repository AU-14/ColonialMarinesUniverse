using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Tracker.SquadLeader;
using Content.Shared.Administration.Logs;
using Content.Shared.CMU14.Callsigns;
using Content.Shared.CMU14.Squads;
using Content.Shared.Database;
using Content.Shared.CMU14.Overwatch;
using Content.Shared.CMU14.Preferences;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Overwatch;

/// <summary>
/// CMU additions to the overwatch console: radio callsigns on the roster, and up to
/// <see cref="CMUOverwatchFeedsComponent.MaxFeeds"/> camera feeds open at once.
/// </summary>
public sealed class CMUOverwatchSystem : EntitySystem
{
    private static readonly EntProtoId FeedEye = "CMUOverwatchFeedEye";
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(0.5);

    [Dependency] private SharedOverwatchConsoleSystem _overwatch = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SquadLeaderTrackerSystem _squadLeaderTracker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ViewSubscriberSystem _viewSubscriber = default!;

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        SubscribeLocalEvent<AU14CallsignComponent, CMUOverwatchMarineDetailsEvent>(OnMarineDetails);

        SubscribeLocalEvent<OverwatchConsoleComponent, CMUOverwatchFeedToggleBuiMsg>(OnFeedToggle);
        SubscribeLocalEvent<OverwatchConsoleComponent, CMUOverwatchFeedClearBuiMsg>(OnFeedClear);
        SubscribeLocalEvent<CMUOverwatchFeedsComponent, ComponentShutdown>(OnFeedsShutdown);

        SubscribeLocalEvent<OverwatchConsoleComponent, CMUOverwatchAssignFireteamBuiMsg>(OnAssignFireteam);
        SubscribeLocalEvent<OverwatchConsoleComponent, CMUOverwatchFireteamLeaderBuiMsg>(OnFireteamLeader);
        SubscribeLocalEvent<OverwatchConsoleComponent, CMUOverwatchAutoLeadersBuiMsg>(OnAutoLeaders);
        SubscribeLocalEvent<CMUOverwatchFeedsComponent, PlayerDetachedEvent>(OnFeedsDetached);
    }

    private void OnMarineDetails(Entity<AU14CallsignComponent> ent, ref CMUOverwatchMarineDetailsEvent args)
    {
        if (!string.IsNullOrWhiteSpace(ent.Comp.Callsign))
            args.Callsign = ent.Comp.Callsign;
    }

    private void OnFeedToggle(Entity<OverwatchConsoleComponent> ent, ref CMUOverwatchFeedToggleBuiMsg args)
    {
        var actor = args.Actor;
        if (!TryComp(actor, out ActorComponent? player) ||
            !TryGetEntity(args.Target, out var target) ||
            !_overwatch.TryGetWatchCamera(ent.Comp, target.Value, out var camera))
        {
            return;
        }

        var feeds = EnsureComp<CMUOverwatchFeedsComponent>(actor);
        if (feeds.Console != ent.Owner)
        {
            ClearFeeds((actor, feeds));
            feeds.Console = ent.Owner;
        }

        var targetNet = args.Target;
        var existing = feeds.Feeds.FindIndex(f => f.Target == targetNet);
        if (existing >= 0)
        {
            RemoveFeed((actor, feeds), existing);
            Dirty(actor, feeds);
            return;
        }

        if (feeds.Feeds.Count >= CMUOverwatchFeedsComponent.MaxFeeds)
        {
            _popup.PopupCursor(Loc.GetString("cmu-overwatch-feeds-full", ("max", CMUOverwatchFeedsComponent.MaxFeeds)),
                actor,
                PopupType.SmallCaution);
            return;
        }

        var eye = Spawn(FeedEye, new EntityCoordinates(camera.Owner, default));
        _viewSubscriber.AddViewSubscriber(eye, player.PlayerSession);

        var name = CMUCharacterName.StripNickname(Identity.Name(target.Value, EntityManager));
        feeds.Feeds.Add(new CMUOverwatchFeed(targetNet, GetNetEntity(eye), name));
        Dirty(actor, feeds);
    }

    private void OnFeedClear(Entity<OverwatchConsoleComponent> ent, ref CMUOverwatchFeedClearBuiMsg args)
    {
        if (TryComp(args.Actor, out CMUOverwatchFeedsComponent? feeds))
            RemCompDeferred(args.Actor, feeds);
    }

    private void OnAssignFireteam(Entity<OverwatchConsoleComponent> ent, ref CMUOverwatchAssignFireteamBuiMsg args)
    {
        if (TryGetConsoleSquadMember(ent.Comp, args.Marine, out var marine, out var squad))
            _squadLeaderTracker.CMUMoveToFireteam(squad, marine, args.Fireteam, args.Actor);
    }

    private void OnFireteamLeader(Entity<OverwatchConsoleComponent> ent, ref CMUOverwatchFireteamLeaderBuiMsg args)
    {
        if (TryGetConsoleSquadMember(ent.Comp, args.Marine, out var marine, out var squad))
            _squadLeaderTracker.CMUSetFireteamLeadership(squad, marine, args.Lead, args.Actor);
    }

    private void OnAutoLeaders(Entity<OverwatchConsoleComponent> ent, ref CMUOverwatchAutoLeadersBuiMsg args)
    {
        if (!TryGetEntity(ent.Comp.Squad, out var squadUid) ||
            !TryComp(squadUid, out SquadTeamComponent? team) ||
            !CanManage(ent.Comp, team))
        {
            return;
        }

        if (args.Enabled)
            RemComp<CMUManualLeadershipComponent>(squadUid.Value);
        else
            EnsureComp<CMUManualLeadershipComponent>(squadUid.Value);

        _adminLog.Add(LogType.RMCFireteam,
            $"{ToPrettyString(args.Actor)} turned automatic leader assignment {(args.Enabled ? "on" : "off")} for {ToPrettyString(squadUid.Value)}");
    }

    private static bool CanManage(OverwatchConsoleComponent console, SquadTeamComponent team)
    {
        return string.Equals(console.Group, "ADMINISTRATOR", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(team.Group, console.Group, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The marine, if they are in the squad this console is showing and it may manage.
    /// </summary>
    private bool TryGetConsoleSquadMember(
        OverwatchConsoleComponent console,
        NetEntity marineNet,
        out EntityUid marine,
        out Entity<SquadTeamComponent> squad)
    {
        marine = default;
        squad = default;
        if (!TryGetEntity(marineNet, out var marineUid) ||
            !TryComp(marineUid, out SquadMemberComponent? member) ||
            member.Squad is not { } squadUid ||
            console.Squad != GetNetEntity(squadUid) ||
            !TryComp(squadUid, out SquadTeamComponent? team) ||
            !CanManage(console, team))
        {
            return false;
        }

        marine = marineUid.Value;
        squad = (squadUid, team);
        return true;
    }

    private void OnFeedsShutdown(Entity<CMUOverwatchFeedsComponent> ent, ref ComponentShutdown args)
    {
        ClearFeeds(ent);
    }

    private void OnFeedsDetached(Entity<CMUOverwatchFeedsComponent> ent, ref PlayerDetachedEvent args)
    {
        RemCompDeferred(ent, ent.Comp);
    }

    private void RemoveFeed(Entity<CMUOverwatchFeedsComponent> ent, int index)
    {
        var feed = ent.Comp.Feeds[index];
        ent.Comp.Feeds.RemoveAt(index);

        if (TryGetEntity(feed.Eye, out var eye) && !TerminatingOrDeleted(eye.Value))
            QueueDel(eye.Value);
    }

    private void ClearFeeds(Entity<CMUOverwatchFeedsComponent> ent)
    {
        for (var i = ent.Comp.Feeds.Count - 1; i >= 0; i--)
        {
            RemoveFeed(ent, i);
        }
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        if (time < _nextCheck)
            return;

        _nextCheck = time + CheckEvery;

        // Feeds close with the console window, and drop when their camera is gone or no longer
        // something this console may watch (helmet taken off, marine moved out of the side's squads).
        var query = EntityQueryEnumerator<CMUOverwatchFeedsComponent>();
        while (query.MoveNext(out var uid, out var feeds))
        {
            if (feeds.Console is not { } console ||
                TerminatingOrDeleted(console) ||
                !TryComp(console, out OverwatchConsoleComponent? overwatch) ||
                !_ui.IsUiOpen(console, OverwatchConsoleUI.Key, uid))
            {
                RemCompDeferred(uid, feeds);
                continue;
            }

            var changed = false;
            for (var i = feeds.Feeds.Count - 1; i >= 0; i--)
            {
                var feed = feeds.Feeds[i];
                if (TryGetEntity(feed.Target, out var target) &&
                    TryGetEntity(feed.Eye, out var eye) &&
                    !TerminatingOrDeleted(eye.Value) &&
                    _overwatch.TryGetWatchCamera(overwatch, target.Value, out var camera) &&
                    Transform(eye.Value).ParentUid == camera.Owner)
                {
                    continue;
                }

                RemoveFeed((uid, feeds), i);
                changed = true;
            }

            if (changed)
                Dirty(uid, feeds);
        }
    }
}
