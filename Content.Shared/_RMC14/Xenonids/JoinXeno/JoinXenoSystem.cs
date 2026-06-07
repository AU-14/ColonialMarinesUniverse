using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.GameTicking;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Actions;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Popups;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.JoinXeno;

public sealed partial class JoinXenoSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedRMCGameTickerSystem _rmcGameTicker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedGameTicker _gameTicker = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public int ClientBurrowedLarva { get; private set; }

    private TimeSpan _burrowedLarvaDeathTime;
    private TimeSpan _burrowedLarvaDeathIgnoreTime;
    private TimeSpan _larvaQueueRoundstartDelay;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<JoinXenoComponent, MapInitEvent>(OnJoinXenoMapInit);
        SubscribeLocalEvent<JoinXenoComponent, JoinXenoActionEvent>(OnJoinXenoAction);
        SubscribeLocalEvent<JoinXenoComponent, JoinXenoBurrowedLarvaEvent>(OnJoinXenoBurrowedLarva);

        Subs.BuiEvents<JoinXenoComponent>(JoinXenoUIKey.Key, subs =>
        {
            subs.Event<JoinXenoHiveChoiceBuiMsg>(OnJoinXenoHiveChoice);
        });

        if (_net.IsClient)
        {
            SubscribeNetworkEvent<BurrowedLarvaStatusEvent>(OnBurrowedLarvaStatus);
        }
        else
        {
            SubscribeLocalEvent<RMCPlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
            SubscribeLocalEvent<BurrowedLarvaChangedEvent>(OnBurrowedLarvaChanged);
            SubscribeNetworkEvent<JoinBurrowedLarvaRequest>(OnJoinBurrowedLarva);
            SubscribeNetworkEvent<BurrowedLarvaStatusRequest>(OnBurrowedLarvaStatusRequest);
        }

        Subs.CVar(_config, RMCCVars.RMCLateJoinsBurrowedLarvaDeathTime, v => _burrowedLarvaDeathTime = TimeSpan.FromMinutes(v), true);
        Subs.CVar(_config, RMCCVars.RMCLateJoinsBurrowedLarvaDeathTimeIgnoreBeforeMinutes, v => _burrowedLarvaDeathIgnoreTime = TimeSpan.FromMinutes(v), true);
        Subs.CVar(_config, RMCCVars.RMCLarvaQueueRoundstartDelaySeconds, v => _larvaQueueRoundstartDelay = TimeSpan.FromSeconds(v), true);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        ClientBurrowedLarva = 0;
        SendLarvaStatus(null);
    }

    private void OnJoinXenoMapInit(Entity<JoinXenoComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.Action, ent.Comp.ActionId);
    }

    private void OnJoinXenoAction(Entity<JoinXenoComponent> ent, ref JoinXenoActionEvent args)
    {
        args.Handled = true;

        if (_net.IsClient)
            return;

        var user = args.Performer;
        if (!TryComp<GhostComponent>(user, out _) ||
            !TryComp(user, out ActorComponent? actor))
            return;

        if (!HasComp<JoinXenoCooldownIgnoreComponent>(user))
        {
            var remaining = _larvaQueueRoundstartDelay - _gameTicker.RoundDuration();
            if (remaining > TimeSpan.Zero)
            {
                _popup.PopupEntity(
                    Loc.GetString("rmc-xeno-larva-queue-round-delay", ("seconds", (int) Math.Ceiling(remaining.TotalSeconds))),
                    user,
                    user,
                    PopupType.MediumCaution);
                return;
            }
        }

        UpdateJoinXenoUi(ent, actor.PlayerSession.UserId);
        _ui.TryOpenUi(ent.Owner, JoinXenoUIKey.Key, user);
    }

    private void OnJoinXenoHiveChoice(Entity<JoinXenoComponent> ent, ref JoinXenoHiveChoiceBuiMsg args)
    {
        if (_net.IsClient)
            return;

        if (args.Actor != ent.Owner ||
            !TryComp(ent, out ActorComponent? actor) ||
            !HasComp<GhostComponent>(ent))
        {
            _ui.CloseUi(ent.Owner, JoinXenoUIKey.Key);
            return;
        }

        var ev = new JoinLarvaQueueEvent(args.Hive);
        RaiseLocalEvent(ent.Owner, ev, true);

        if (!TryComp(ent, out actor) ||
            !HasComp<GhostComponent>(ent))
        {
            _ui.CloseUi(ent.Owner, JoinXenoUIKey.Key);
            return;
        }

        UpdateJoinXenoUi(ent, actor.PlayerSession.UserId);
    }

    private void UpdateJoinXenoUi(EntityUid user, NetUserId userId)
    {
        var queueStatus = new GetLarvaQueueStatusEvent(userId);
        RaiseLocalEvent(queueStatus);

        var entries = new List<JoinXenoHiveEntry>();
        var hives = EntityQueryEnumerator<HiveComponent, MetaDataComponent>();
        while (hives.MoveNext(out var hiveId, out _, out var metaData))
        {
            var status = JoinXenoQueueStatus.NotQueued;
            var position = 0;
            if (queueStatus.Queues.TryGetValue(hiveId, out var queueUserStatus))
            {
                if (queueUserStatus.Position is { } queuePosition)
                {
                    status = JoinXenoQueueStatus.Queued;
                    position = queuePosition;
                }
                else
                {
                    status = JoinXenoQueueStatus.Waiting;
                }
            }

            entries.Add(new JoinXenoHiveEntry(
                GetNetEntity(hiveId),
                Name(hiveId, metaData),
                status,
                position));
        }

        entries.Sort((a, b) => string.Compare(a.HiveName, b.HiveName, StringComparison.Ordinal));
        _ui.SetUiState(user, JoinXenoUIKey.Key, new JoinXenoBuiState(entries));
    }

    public bool CanJoinXeno(EntityUid user)
    {
        if (!TryComp<GhostComponent>(user, out var ghostComp))
            return false;

        if (HasComp<JoinXenoCooldownIgnoreComponent>(user))
            return true;

        // If the game has been going on longer than the death ignore time, then check how long since the ghost has died
        if (_gameTicker.RoundDuration() > _burrowedLarvaDeathIgnoreTime)
        {
            var timeSinceDeath = _timing.CurTime.Subtract(ghostComp.TimeOfDeath);

            if (timeSinceDeath < _burrowedLarvaDeathTime)
            {
                var msg = Loc.GetString("rmc-xeno-ui-burrowed-need-time", ("seconds", _burrowedLarvaDeathTime.TotalSeconds - (int)timeSinceDeath.TotalSeconds));
                _popup.PopupEntity(msg, user, user, PopupType.MediumCaution);
                return false;
            }
        }

        return true;
    }

    private void OnJoinXenoBurrowedLarva(Entity<JoinXenoComponent> ent, ref JoinXenoBurrowedLarvaEvent args)
    {
        if (!CanJoinXeno(ent.Owner))
            return;

        if (!TryGetEntity(args.Hive, out var hive) ||
            !TryComp(hive, out HiveComponent? hiveComp) ||
            !TryComp(ent, out ActorComponent? actor))
        {
            return;
        }

        _hive.JoinBurrowedLarva((hive.Value, hiveComp), actor.PlayerSession);
    }

    private void OnBurrowedLarvaStatus(BurrowedLarvaStatusEvent ev)
    {
        ClientBurrowedLarva = ev.Larva;

        if (_net.IsServer)
            return;

        var changedEv = new BurrowedLarvaChangedEvent(ev.Larva);
        RaiseLocalEvent(ref changedEv);
    }

    private void OnPlayerJoinedLobby(ref RMCPlayerJoinedLobbyEvent ev)
    {
        SendLarvaStatus(ev.Player);
    }

    private void OnBurrowedLarvaChanged(ref BurrowedLarvaChangedEvent ev)
    {
        SendLarvaStatus(null);
    }

    private void OnJoinBurrowedLarva(JoinBurrowedLarvaRequest msg, EntitySessionEventArgs args)
    {
        if (!_rmcGameTicker.PlayerGameStatuses.TryGetValue(args.SenderSession.UserId, out var status) ||
            status == PlayerGameStatus.JoinedGame)
        {
            return;
        }

        var query = EntityQueryEnumerator<CMDistressSignalRuleComponent>();
        while (query.MoveNext(out var comp))
        {
            if (!TryComp(comp.Hive, out HiveComponent? hive) ||
                !_hive.JoinBurrowedLarva((comp.Hive, hive), args.SenderSession))
            {
                continue;
            }

            _rmcGameTicker.PlayerJoinGame(args.SenderSession);
            break;
        }
    }

    private void OnBurrowedLarvaStatusRequest(BurrowedLarvaStatusRequest msg, EntitySessionEventArgs args)
    {
        SendLarvaStatus(args.SenderSession);
    }

    private void SendLarvaStatus(ICommonSession? to)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<ActiveGameRuleComponent, CMDistressSignalRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out _, out var comp, out _))
        {
            if (!TryComp(comp.Hive, out HiveComponent? hive))
                continue;

            var statusEv = new BurrowedLarvaStatusEvent(hive.BurrowedLarva);
            if (to != null)
            {
                RaiseNetworkEvent(statusEv, to);
                return;
            }

            var filter = Filter.Empty()
                .AddWhere(s =>
                    _rmcGameTicker.PlayerGameStatuses.GetValueOrDefault(s.UserId) != PlayerGameStatus.JoinedGame);
            RaiseNetworkEvent(statusEv, filter);
        }
    }

    public void RequestBurrowedLarvaStatus()
    {
        if (_net.IsServer)
            return;

        var ev = new BurrowedLarvaStatusRequest();
        RaiseNetworkEvent(ev);
    }

    public void ClientJoinLarva()
    {
        if (_net.IsServer)
            return;

        var ev = new JoinBurrowedLarvaRequest();
        RaiseNetworkEvent(ev);
    }
}
