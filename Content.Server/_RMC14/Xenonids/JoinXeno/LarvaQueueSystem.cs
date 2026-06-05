using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Xenonids.JoinXeno;

public sealed partial class LarvaQueueSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private GhostRoleSystem _ghostRole = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private static readonly EntProtoId LesserDrone = "CMXenoLesserDrone";
    private static readonly ProtoId<TagPrototype> LarvaTag = "RMCXenoLarva";
    private static readonly ProtoId<JobPrototype> LarvaRole = "CMXenoLarva";
    private static readonly TimeSpan ClaimConfirmDuration = TimeSpan.FromSeconds(30);

    private readonly Dictionary<EntityUid, LarvaQueueState> _queues = [];
    private readonly Dictionary<NetUserId, PendingLarvaQueueClaim> _pendingClaims = [];
    private readonly Dictionary<EntityUid, NetUserId> _pendingEntityClaims = [];
    private readonly Dictionary<EntityUid, int> _pendingBurrowedLarvaClaims = [];
    private readonly List<EntityUid> _emptyQueues = [];
    private readonly List<NetUserId> _expiredClaims = [];
    private int _nextClaimId;

    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<HiveComponent> _hiveQuery;

    public override void Initialize()
    {
        _ghostQuery = GetEntityQuery<GhostComponent>();
        _hiveQuery = GetEntityQuery<HiveComponent>();

        SubscribeLocalEvent<JoinXenoComponent, JoinLarvaQueueEvent>(OnJoinLarvaQueue);
        SubscribeLocalEvent<GetLarvaQueueStatusEvent>(OnGetLarvaQueueStatus);
        SubscribeLocalEvent<LarvaQueueClaimConfirmEvent>(OnLarvaQueueClaimConfirm);
        SubscribeLocalEvent<LarvaQueueClaimDeclineEvent>(OnLarvaQueueClaimDecline);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<BurrowedLarvaAddedEvent>(OnBurrowedLarvaAdded);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<NewXenoEvolvedEvent>(OnNewXenoEvolved);
        SubscribeLocalEvent<XenoDevolvedEvent>(OnXenoDevolved);
        SubscribeLocalEvent<AbandonedXenoQueueableComponent, ComponentStartup>(OnAbandonedQueueableStartup);
        SubscribeLocalEvent<AbandonedXenoQueueableComponent, ComponentShutdown>(OnAbandonedQueueableShutdown);
        SubscribeLocalEvent<LarvaQueueableComponent, ComponentStartup>(OnQueueableStartup);
        SubscribeLocalEvent<LarvaQueueableComponent, HiveChangedEvent>(OnQueueableHiveChanged);
        SubscribeLocalEvent<LarvaQueueableComponent, MindRemovedMessage>(OnQueueableMindRemoved);
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        _queues.Clear();
        _pendingClaims.Clear();
        _pendingEntityClaims.Clear();
        _pendingBurrowedLarvaClaims.Clear();
    }

    private void OnJoinLarvaQueue(Entity<JoinXenoComponent> ent, ref JoinLarvaQueueEvent args)
    {
        if (!TryComp(ent, out ActorComponent? actor) ||
            !TryComp(ent, out GhostComponent? ghost) ||
            !TryGetEntity(args.Hive, out var hiveUid) ||
            hiveUid is not { Valid: true } hiveId ||
            !_hiveQuery.TryComp(hiveId, out var hiveComp))
        {
            return;
        }

        if (!CanUseQueue(ent, ghost))
            return;

        var userId = actor.PlayerSession.UserId;
        var actorEntity = actor.PlayerSession.AttachedEntity ?? ent.Owner;
        var queue = QueueFor(hiveId);

        CancelPendingClaim(userId, timedOut: false, tryNext: false);

        if (queue.Remove(userId))
        {
            _popup.PopupEntity(Loc.GetString("rmc-xeno-larva-queue-removed"), actorEntity, actorEntity);
            RemoveIfEmpty(hiveId);
            return;
        }

        RemoveFromAllQueues(userId, hiveId);

        var wait = TimeSpan.FromSeconds(_config.GetCVar(RMCCVars.RMCLarvaQueueWaitSeconds));
        if (HasComp<JoinXenoCooldownIgnoreComponent>(ent) || _timing.CurTime - ghost.TimeOfDeath >= wait)
        {
            queue.AddReady(userId);
            _popup.PopupEntity(
                Loc.GetString("rmc-xeno-larva-queue-added", ("position", queue.ReadyCount)),
                actorEntity,
                actorEntity);

            TryClaimForHive((hiveId, hiveComp));
            return;
        }

        var readyAt = ghost.TimeOfDeath + wait;
        queue.AddWaiting(userId, readyAt);
        _popup.PopupEntity(
            Loc.GetString(
                "rmc-xeno-larva-prequeue-added",
                ("seconds", Math.Max(0, (int) Math.Ceiling((readyAt - _timing.CurTime).TotalSeconds)))),
            actorEntity,
            actorEntity);
    }

    private void OnGetLarvaQueueStatus(GetLarvaQueueStatusEvent args)
    {
        foreach (var (hive, queue) in _queues)
        {
            if (queue.TryGetUserStatus(args.UserId, out var status))
                args.Queues[hive] = status;
        }
    }

    private bool CanUseQueue(EntityUid user, GhostComponent ghost)
    {
        if (HasComp<JoinXenoCooldownIgnoreComponent>(user))
            return true;

        var denyQueuing = _config.GetCVar(RMCCVars.RMCLarvaQueueRoundstartDelaySeconds);
        var remaining = denyQueuing - _gameTicker.RoundDuration().TotalSeconds;
        if (remaining <= 0)
            return true;

        _popup.PopupEntity(
            Loc.GetString("rmc-xeno-larva-queue-round-delay", ("seconds", (int) Math.Ceiling(remaining))),
            user,
            user,
            PopupType.MediumCaution);
        return false;
    }

    private void OnBurrowedLarvaAdded(ref BurrowedLarvaAddedEvent ev)
    {
        if (_hiveQuery.TryComp(ev.Hive, out var hive))
            TryClaimForHive((ev.Hive, hive));
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (_ghostQuery.HasComp(ev.Entity))
            return;

        CancelPendingClaim(ev.Player.UserId, timedOut: false);
        RemoveFromAllQueues(ev.Player.UserId);
    }

    private void OnNewXenoEvolved(ref NewXenoEvolvedEvent args)
    {
        CancelPendingClaimForInvalidTarget(args.NewXeno);
    }

    private void OnXenoDevolved(ref XenoDevolvedEvent args)
    {
        CancelPendingClaimForInvalidTarget(args.NewXeno);
    }

    private void OnAbandonedQueueableStartup(Entity<AbandonedXenoQueueableComponent> ent, ref ComponentStartup args)
    {
        TryClaimQueueable(ent.Owner);
    }

    private void OnAbandonedQueueableShutdown(Entity<AbandonedXenoQueueableComponent> ent, ref ComponentShutdown args)
    {
        CancelPendingClaimForInvalidTarget(ent.Owner);
    }

    private void OnQueueableStartup(Entity<LarvaQueueableComponent> ent, ref ComponentStartup args)
    {
        TryClaimQueueable(ent.Owner);
    }

    private void OnQueueableHiveChanged(Entity<LarvaQueueableComponent> ent, ref HiveChangedEvent args)
    {
        TryClaimQueueable(ent.Owner);
    }

    private void OnQueueableMindRemoved(Entity<LarvaQueueableComponent> ent, ref MindRemovedMessage args)
    {
        TryClaimQueueable(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        _emptyQueues.Clear();
        _expiredClaims.Clear();

        foreach (var (userId, pending) in _pendingClaims)
        {
            if (pending.ExpiresAt <= time)
                _expiredClaims.Add(userId);
        }

        foreach (var userId in _expiredClaims)
        {
            CancelPendingClaim(userId, timedOut: true);
        }

        foreach (var (hiveId, queue) in _queues)
        {
            var promoted = queue.PromoteWaiting(time);
            if (promoted.Count > 0)
            {
                NotifyReadyPositions(hiveId);
                if (_hiveQuery.TryComp(hiveId, out var hive))
                    TryClaimForHive((hiveId, hive));
            }

            if (queue.Empty)
                _emptyQueues.Add(hiveId);
        }

        foreach (var hiveId in _emptyQueues)
        {
            _queues.Remove(hiveId);
        }
    }

    private LarvaQueueState QueueFor(EntityUid hive)
    {
        if (_queues.TryGetValue(hive, out var queue))
            return queue;

        queue = new LarvaQueueState();
        _queues[hive] = queue;
        return queue;
    }

    private bool TryGetQueue(EntityUid hive, out LarvaQueueState queue)
    {
        return _queues.TryGetValue(hive, out queue!);
    }

    private void TryClaimQueueable(EntityUid uid)
    {
        if (TryComp(uid, out HiveMemberComponent? member) &&
            member.Hive is { } hiveId &&
            _hiveQuery.TryComp(hiveId, out var hive))
        {
            TryClaimForHive((hiveId, hive));
        }
    }

    private void TryClaimForHive(Entity<HiveComponent> hive)
    {
        if (HasPendingClaimForHive(hive.Owner) ||
            !TryGetQueue(hive.Owner, out var queue) ||
            queue.ReadyCount == 0)
            return;

        var offered = TryOfferQueueableLarva(hive, queue) ||
                      TryOfferAbandonedXeno(hive, queue) ||
                      TryOfferBurrowedLarva(hive, queue);

        if (offered)
            NotifyReadyPositions(hive.Owner);

        RemoveIfEmpty(hive.Owner);
    }

    private bool TryOfferQueueableLarva(Entity<HiveComponent> hive, LarvaQueueState queue)
    {
        var query = EntityQueryEnumerator<LarvaQueueableComponent, HiveMemberComponent>();
        while (queue.ReadyCount > 0 && query.MoveNext(out var uid, out _, out var member))
        {
            if (!CanQueueLarva(uid, member, hive))
                continue;

            return TryOfferEntityClaim(uid, hive, queue);
        }

        return false;
    }

    private bool CanQueueLarva(EntityUid uid, HiveMemberComponent member, Entity<HiveComponent> hive)
    {
        if (!CanQueueBodyCommon(uid, member, hive, out var xeno))
            return false;

        return _tag.HasTag(uid, LarvaTag) && xeno.Role == LarvaRole;
    }

    private bool TryOfferAbandonedXeno(Entity<HiveComponent> hive, LarvaQueueState queue)
    {
        var query = EntityQueryEnumerator<AbandonedXenoQueueableComponent, LarvaQueueableComponent, HiveMemberComponent>();
        while (queue.ReadyCount > 0 && query.MoveNext(out var uid, out _, out _, out var member))
        {
            if (!CanQueueAbandonedXeno(uid, member, hive))
                continue;

            return TryOfferEntityClaim(uid, hive, queue);
        }

        return false;
    }

    private bool CanQueueAbandonedXeno(EntityUid uid, HiveMemberComponent member, Entity<HiveComponent> hive)
    {
        return HasComp<AbandonedXenoQueueableComponent>(uid) &&
               CanQueueBodyCommon(uid, member, hive, out _);
    }

    private bool CanQueueBodyCommon(EntityUid uid, HiveMemberComponent member, Entity<HiveComponent> hive, out XenoComponent xeno)
    {
        xeno = default!;
        if (!TryComp(uid, out XenoComponent? xenoComp))
            return false;

        if (member.Hive != hive.Owner ||
            TerminatingOrDeleted(uid) ||
            _pendingEntityClaims.ContainsKey(uid) ||
            HasComp<XenoEvolutionTransferComponent>(uid) ||
            HasComp<LarvaQueueClaimBlockedComponent>(uid) ||
            HasComp<XenoRecentlyDevolvedComponent>(uid) ||
            HasComp<ActorComponent>(uid) ||
            _mobState.IsDead(uid) ||
            HasComp<XenoParasiteComponent>(uid) ||
            HasComp<DropshipHijackerComponent>(uid) ||
            TryComp(uid, out MindContainerComponent? mind) && mind.HasMind)
        {
            return false;
        }

        xeno = xenoComp;
        return !TryPrototype(uid, out var prototype) || prototype.ID != LesserDrone;
    }

    private bool TryOfferEntityClaim(EntityUid uid, Entity<HiveComponent> hive, LarvaQueueState queue)
    {
        while (queue.TryDequeueReady(out var userId))
        {
            if (!TryGetQueuedSession(userId, out var session))
                continue;

            OpenPendingClaim(userId, session, hive.Owner, uid, Name(uid));
            return true;
        }

        return false;
    }

    private bool TryOfferBurrowedLarva(Entity<HiveComponent> hive, LarvaQueueState queue)
    {
        while (hive.Comp.BurrowedLarva - GetPendingBurrowedLarvaClaims(hive.Owner) > 0 &&
               queue.TryDequeueReady(out var userId))
        {
            if (!TryGetQueuedSession(userId, out var session))
                continue;

            OpenPendingClaim(
                userId,
                session,
                hive.Owner,
                null,
                Loc.GetString("rmc-xeno-larva-queue-burrowed-larva"));
            return true;
        }

        return false;
    }

    private void OpenPendingClaim(
        NetUserId userId,
        ICommonSession session,
        EntityUid hive,
        EntityUid? target,
        string xenoName)
    {
        if (session.AttachedEntity is not { } attached)
            return;

        CancelPendingClaim(userId, timedOut: false, tryNext: false);

        var claimId = ++_nextClaimId;
        var pending = new PendingLarvaQueueClaim(
            claimId,
            hive,
            target,
            target == null,
            _timing.CurTime + ClaimConfirmDuration);

        _pendingClaims[userId] = pending;
        if (target is { } targetId)
            _pendingEntityClaims[targetId] = userId;
        else
            _pendingBurrowedLarvaClaims[hive] = GetPendingBurrowedLarvaClaims(hive) + 1;

        var options = new List<DialogOption>
        {
            new(
                Loc.GetString("rmc-xeno-larva-queue-confirm-option"),
                new LarvaQueueClaimConfirmEvent(userId, claimId)),
            new(
                Loc.GetString("rmc-xeno-larva-queue-confirm-decline-option"),
                new LarvaQueueClaimDeclineEvent(userId, claimId)),
        };

        _dialog.OpenOptions(
            attached,
            attached,
            Loc.GetString("rmc-xeno-larva-queue-confirm-title"),
            options,
            Loc.GetString(
                "rmc-xeno-larva-queue-confirm-message",
                ("xeno", xenoName),
                ("seconds", (int) ClaimConfirmDuration.TotalSeconds)));
    }

    private void OnLarvaQueueClaimConfirm(LarvaQueueClaimConfirmEvent ev)
    {
        if (!_pendingClaims.TryGetValue(ev.UserId, out var pending) ||
            pending.ClaimId != ev.ClaimId)
        {
            return;
        }

        if (pending.ExpiresAt <= _timing.CurTime)
        {
            CancelPendingClaim(ev.UserId, timedOut: true);
            return;
        }

        ReleasePendingClaim(ev.UserId, pending);

        if (!_player.TryGetSessionById(ev.UserId, out var session) ||
            session.AttachedEntity is not { } attached ||
            !_ghostQuery.HasComp(attached))
        {
            TryClaimNextForHive(pending.Hive);
            return;
        }

        var claimed = false;
        if (pending.Target is { } target)
        {
            if (_hiveQuery.TryComp(pending.Hive, out var hive) &&
                TryComp(target, out HiveMemberComponent? member) &&
                (CanQueueLarva(target, member, (pending.Hive, hive)) ||
                 CanQueueAbandonedXeno(target, member, (pending.Hive, hive))))
            {
                claimed = ClaimQueueableXeno(target, session);
            }
        }
        else if (_hiveQuery.TryComp(pending.Hive, out var hive))
        {
            claimed = _hive.JoinBurrowedLarva((pending.Hive, hive), session);
        }

        if (!claimed && session.AttachedEntity is { } stillAttached)
        {
            QueueFor(pending.Hive).AddReadyFirst(ev.UserId);
            _popup.PopupEntity(
                Loc.GetString("rmc-xeno-larva-queue-confirm-invalid"),
                stillAttached,
                stillAttached,
                PopupType.MediumCaution);
            NotifyReadyPositions(pending.Hive);
        }

        TryClaimNextForHive(pending.Hive);
    }

    private void OnLarvaQueueClaimDecline(LarvaQueueClaimDeclineEvent ev)
    {
        if (!_pendingClaims.TryGetValue(ev.UserId, out var pending) ||
            pending.ClaimId != ev.ClaimId)
        {
            return;
        }

        CancelPendingClaim(ev.UserId, timedOut: false);

        if (_player.TryGetSessionById(ev.UserId, out var session) &&
            session.AttachedEntity is { } attached &&
            _ghostQuery.HasComp(attached))
        {
            _popup.PopupEntity(
                Loc.GetString("rmc-xeno-larva-queue-confirm-declined"),
                attached,
                attached,
                PopupType.MediumCaution);
        }
    }

    private bool ClaimQueueableXeno(EntityUid uid, ICommonSession session)
    {
        if (TryComp(uid, out GhostRoleComponent? role))
        {
            _ghostRole.GhostRoleInternalCreateMindAndTransfer(session, uid, uid, role);
            return true;
        }

        if (TryComp(uid, out MindContainerComponent? mind) && mind.HasMind)
            return false;

        var newMind = _mind.CreateMind(session.UserId, Name(uid));
        _mind.TransferTo(newMind, uid, ghostCheckOverride: true);
        return true;
    }

    private void CancelPendingClaim(NetUserId userId, bool timedOut, bool tryNext = true)
    {
        if (!_pendingClaims.TryGetValue(userId, out var pending))
            return;

        ReleasePendingClaim(userId, pending);

        if (_player.TryGetSessionById(userId, out var session) &&
            session.AttachedEntity is { } attached &&
            _ghostQuery.HasComp(attached))
        {
            ClosePendingDialog(attached, pending);

            if (timedOut)
            {
                _popup.PopupEntity(
                    Loc.GetString("rmc-xeno-larva-queue-confirm-timeout"),
                    attached,
                    attached,
                    PopupType.MediumCaution);
            }
        }

        if (tryNext)
            TryClaimNextForHive(pending.Hive);
    }

    private void ReleasePendingClaim(NetUserId userId, PendingLarvaQueueClaim pending)
    {
        _pendingClaims.Remove(userId);

        if (pending.Target is { } target)
        {
            _pendingEntityClaims.Remove(target);
            return;
        }

        var burrowed = GetPendingBurrowedLarvaClaims(pending.Hive);
        if (burrowed <= 1)
            _pendingBurrowedLarvaClaims.Remove(pending.Hive);
        else
            _pendingBurrowedLarvaClaims[pending.Hive] = burrowed - 1;
    }

    private void CancelPendingClaimForInvalidTarget(EntityUid target)
    {
        if (!_pendingEntityClaims.TryGetValue(target, out var userId) ||
            !_pendingClaims.TryGetValue(userId, out var pending) ||
            pending.Target != target)
        {
            return;
        }

        ReleasePendingClaim(userId, pending);
        QueueFor(pending.Hive).AddReadyFirst(userId);

        if (_player.TryGetSessionById(userId, out var session) &&
            session.AttachedEntity is { } attached &&
            _ghostQuery.HasComp(attached))
        {
            ClosePendingDialog(attached, pending);
        }

        NotifyReadyPositions(pending.Hive);
        TryClaimNextForHive(pending.Hive);
    }

    private void ClosePendingDialog(EntityUid attached, PendingLarvaQueueClaim pending)
    {
        if (!TryComp(attached, out DialogComponent? dialog))
            return;

        var isPendingDialog = false;
        foreach (var option in dialog.Options)
        {
            if (option.Event is LarvaQueueClaimConfirmEvent ev && ev.ClaimId == pending.ClaimId)
            {
                isPendingDialog = true;
                break;
            }
        }

        if (!isPendingDialog)
            return;

        _ui.CloseUi(attached, DialogUiKey.Key);
        RemComp<DialogComponent>(attached);
    }

    private int GetPendingBurrowedLarvaClaims(EntityUid hive)
    {
        return _pendingBurrowedLarvaClaims.GetValueOrDefault(hive);
    }

    private bool HasPendingClaimForHive(EntityUid hive)
    {
        foreach (var pending in _pendingClaims.Values)
        {
            if (pending.Hive == hive)
                return true;
        }

        return false;
    }

    private void TryClaimNextForHive(EntityUid hiveId)
    {
        if (_hiveQuery.TryComp(hiveId, out var hive))
            TryClaimForHive((hiveId, hive));
    }

    private bool TryGetQueuedSession(NetUserId userId, out ICommonSession session)
    {
        if (!_player.TryGetSessionById(userId, out session!))
            return false;

        if (session.AttachedEntity is { } attached && _ghostQuery.HasComp(attached))
            return true;

        RemoveFromAllQueues(userId);
        return false;
    }

    private void NotifyReadyPositions(EntityUid hive)
    {
        if (!TryGetQueue(hive, out var queue))
            return;

        for (var i = 0; i < queue.ReadyUsers.Count; i++)
        {
            var userId = queue.ReadyUsers[i];
            if (!TryGetQueuedSession(userId, out var session) || session.AttachedEntity is not { } attached)
                continue;

            _popup.PopupEntity(
                Loc.GetString("rmc-xeno-larva-queue-position", ("position", i + 1)),
                attached,
                attached);
        }
    }

    private void RemoveFromAllQueues(NetUserId userId, EntityUid? except = null)
    {
        _emptyQueues.Clear();
        foreach (var (hive, queue) in _queues)
        {
            if (hive == except)
                continue;

            queue.Remove(userId);
            if (queue.Empty)
                _emptyQueues.Add(hive);
        }

        foreach (var hive in _emptyQueues)
        {
            _queues.Remove(hive);
        }
    }

    private void RemoveIfEmpty(EntityUid hive)
    {
        if (_queues.TryGetValue(hive, out var queue) && queue.Empty)
            _queues.Remove(hive);
    }

    private sealed record PendingLarvaQueueClaim(
        int ClaimId,
        EntityUid Hive,
        EntityUid? Target,
        bool BurrowedLarva,
        TimeSpan ExpiresAt);
}
