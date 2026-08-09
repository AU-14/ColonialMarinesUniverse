using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.GameTicking;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Actions;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Components;
using Content.Shared.Popups;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.JoinXeno;

public sealed partial class JoinXenoSystem : EntitySystem
{
    private static readonly TimeSpan PoolUiRefreshInterval = TimeSpan.FromSeconds(1);

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
    private TimeSpan _nextPoolUiRefresh;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<JoinXenoComponent, MapInitEvent>(OnJoinXenoMapInit);
        SubscribeLocalEvent<JoinXenoComponent, JoinXenoActionEvent>(OnJoinXenoAction);
        SubscribeLocalEvent<JoinXenoComponent, JoinXenoBurrowedLarvaEvent>(OnJoinXenoBurrowedLarva);

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
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        ClientBurrowedLarva = 0;
        _nextPoolUiRefresh = default;
        SendLarvaStatus(null);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient || _timing.CurTime < _nextPoolUiRefresh)
            return;

        _nextPoolUiRefresh = _timing.CurTime + PoolUiRefreshInterval;

        var query = EntityQueryEnumerator<JoinXenoComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out _, out var actor))
        {
            if (_ui.IsUiOpen(uid, JoinXenoUIKey.Key))
                UpdateJoinXenoUi(uid, actor.PlayerSession.UserId);
        }
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

        UpdateJoinXenoUi(ent, actor.PlayerSession.UserId);
        _ui.TryOpenUi(ent.Owner, JoinXenoUIKey.Key, user);
    }

    private void UpdateJoinXenoUi(EntityUid user, NetUserId userId)
    {
        var poolStatus = new GetLarvaPoolStatusEvent(userId);
        RaiseLocalEvent(poolStatus);

        var entries = new List<JoinXenoHiveEntry>();
        var hives = EntityQueryEnumerator<HiveComponent, MetaDataComponent>();
        while (hives.MoveNext(out var hiveId, out _, out var metaData))
        {
            var status = LarvaPoolStatus.Ineligible;
            var position = 0;
            var reason = LarvaPoolIneligibilityReason.PreferenceDataLoading;
            var preferenceLoaded = false;
            var optedIn = false;
            if (poolStatus.Pools.TryGetValue(hiveId, out var poolUserStatus))
            {
                status = poolUserStatus.Status;
                position = poolUserStatus.Position;
                reason = poolUserStatus.IneligibilityReason;
                preferenceLoaded = poolUserStatus.PreferenceLoaded;
                optedIn = poolUserStatus.OptedIn;
            }

            entries.Add(new JoinXenoHiveEntry(
                GetNetEntity(hiveId),
                Name(hiveId, metaData),
                status,
                position,
                reason,
                preferenceLoaded,
                optedIn));
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
            var timeSinceDeath = _timing.RealTime.Subtract(ghostComp.TimeOfDeath);

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
                HasLarvaPoolCandidates(comp.Hive) ||
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

            var availableLarva = HasLarvaPoolCandidates(comp.Hive) ? 0 : hive.BurrowedLarva;
            var statusEv = new BurrowedLarvaStatusEvent(availableLarva);
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

    private bool HasLarvaPoolCandidates(EntityUid hive)
    {
        var ev = new GetLarvaPoolCandidateCountEvent(hive);
        RaiseLocalEvent(ev);
        return ev.Count > 0;
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
