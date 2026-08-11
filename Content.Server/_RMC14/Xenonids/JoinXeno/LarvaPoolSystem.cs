using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Preferences.Managers;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.GameTicking;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Thunderdome;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Xenonids.JoinXeno;

public sealed partial class LarvaPoolSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IBanManager _bans = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private GhostRoleSystem _ghostRole = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private IServerPreferencesManager _preferences = default!;
    [Dependency] private PlayTimeTrackingSystem _playTime = default!;
    [Dependency] private LarvaPoolPreferenceManager _larvaPoolPreferences = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RMCUnrevivableSystem _unrevivable = default!;

    private static readonly ProtoId<JobPrototype> LarvaRole = "CMXenoLarva";
    private static readonly ProtoId<TagPrototype> LarvaTag = "RMCXenoLarva";
    private static readonly ProtoId<JobPrototype> LesserDroneRole = "CMXenoLesserDrone";
    private static readonly ProtoId<JobPrototype> QueenRole = "CMXenoQueen";
    private static readonly ProtoId<JobPrototype> SelectableXenoRole = "CMXenoSelectableXeno";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PositionUpdateInterval = TimeSpan.FromMinutes(2);

    private readonly LarvaPoolState _pool = new();
    private readonly Dictionary<NetUserId, DeathTransition> _recordedDeaths = [];
    private readonly Dictionary<NetUserId, TimeSpan> _deathTimes = [];
    private readonly Dictionary<NetUserId, DeathTransition> _pendingDeaths = [];
    private readonly HashSet<NetUserId> _preserveNextDeath = [];
    private readonly HashSet<NetUserId> _staffOptIns = [];
    private readonly List<StrandedXenoCredit> _strandedXenoCredits = [];
    private readonly List<EntityUid> _strandedXenoHives = [];

    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<HiveComponent> _hiveQuery;
    private TimeSpan _nextRefresh;
    private TimeSpan _nextPositionUpdate;
    private bool _refreshRequested;

    public override void Initialize()
    {
        _ghostQuery = GetEntityQuery<GhostComponent>();
        _hiveQuery = GetEntityQuery<HiveComponent>();

        InitializeOffers();

        SubscribeLocalEvent<GetLarvaPoolCandidateCountEvent>(OnGetLarvaPoolCandidateCount);
        SubscribeLocalEvent<GetLarvaPoolStatusEvent>(OnGetLarvaPoolStatus);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<RMCPlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        SubscribeLocalEvent<BurrowedLarvaAddedEvent>(OnBurrowedLarvaAdded);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<AbandonedXenoPoolAvailableComponent, ComponentStartup>(OnAbandonedAvailableStartup);
        SubscribeLocalEvent<LarvaPoolAvailableComponent, ComponentStartup>(OnAvailableStartup);
        SubscribeLocalEvent<LarvaPoolAvailableComponent, HiveChangedEvent>(OnAvailableHiveChanged);
        SubscribeLocalEvent<LarvaPoolAvailableComponent, MindRemovedMessage>(OnAvailableMindRemoved);
        SubscribeLocalEvent<JoinXenoComponent, SetLarvaPoolOptInBuiMsg>(OnSetLarvaPoolOptIn);
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        _pool.Clear();
        _recordedDeaths.Clear();
        _deathTimes.Clear();
        _pendingDeaths.Clear();
        _preserveNextDeath.Clear();
        _staffOptIns.Clear();
        _strandedXenoCredits.Clear();
        _strandedXenoHives.Clear();
        CleanupOffers();
        _nextRefresh = default;
        _nextPositionUpdate = default;
        _refreshRequested = false;

        // ResettingCleanup moves players to the lobby before raising this event. Seed the
        // new round here so those lobby events are not erased by the cleanup above.
        foreach (var session in _player.Sessions)
        {
            if (session.Status is not (SessionStatus.Disconnected or SessionStatus.Zombie))
                _pool.RecordJoined(session.UserId, _timing.CurTime);
        }
    }

    private void OnPlayerJoinedLobby(ref RMCPlayerJoinedLobbyEvent ev)
    {
        _pool.RecordJoined(ev.Player.UserId, _timing.CurTime);
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        NetUserId? userId = null;
        if (_mind.TryGetMind(ev.Target, out _, out var mind) && mind.UserId is { } mindUser)
            userId = mindUser;
        else if (TryComp(ev.Target, out ActorComponent? actor))
            userId = actor.PlayerSession.UserId;

        if (userId is not { } user)
            return;

        if (ev.NewMobState != MobState.Dead)
        {
            if (ev.OldMobState == MobState.Dead)
            {
                _recordedDeaths.Remove(user);
                _deathTimes.Remove(user);
            }

            return;
        }

        var preserveNextDeath = _preserveNextDeath.Remove(user);
        var transition = new DeathTransition(
            preserveNextDeath || PreservesPoolTime(ev.Target),
            BypassesDeathTimer(ev.Target));
        _pool.RecordJoined(user, _timing.CurTime);
        _pool.RecordDeath(user, _timing.CurTime, transition.PreservePoolTime);
        _recordedDeaths[user] = transition;
        _deathTimes[user] = _timing.CurTime;
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        var userId = ev.Player.UserId;
        CancelPendingOffer(userId, LarvaPoolOfferCancellation.Invalid);
        _pool.RecordJoined(userId, _timing.CurTime);

        if (_ghostQuery.TryComp(ev.Entity, out var ghost))
        {
            var ghostDeathTime = GetGhostDeathTime(ghost);
            if (_pendingDeaths.Remove(userId, out var pending))
            {
                // MobStateChanged records actual death. Only use the ghost timestamp for
                // direct transfers that did not pass through a mob death.
                var hadRecordedDeath = _recordedDeaths.Remove(userId, out var recorded);
                if (!hadRecordedDeath)
                {
                    _pool.RecordDeath(userId, ghostDeathTime, pending.PreservePoolTime);
                    _deathTimes[userId] = ghostDeathTime;
                }

                var bypassDeathTimer = hadRecordedDeath
                    ? recorded.BypassDeathTimer
                    : pending.BypassDeathTimer;
                if (bypassDeathTimer)
                    EnsureComp<JoinXenoCooldownIgnoreComponent>(ev.Entity);
            }

            _deathTimes.TryAdd(userId, ghostDeathTime);

            _refreshRequested = true;
            return;
        }

        RemoveStrandedXenoCredit(userId);
        _staffOptIns.Remove(userId);
        _pendingDeaths.Remove(userId);
        if (!_mobState.IsDead(ev.Entity))
        {
            _recordedDeaths.Remove(userId);
            _deathTimes.Remove(userId);
        }
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        if (_ghostQuery.HasComp(ev.Entity))
        {
            CancelPendingOffer(ev.Player.UserId, LarvaPoolOfferCancellation.Disconnected);
            return;
        }

        var userId = ev.Player.UserId;
        var preserveNextDeath = _preserveNextDeath.Remove(userId);
        var recorded = _recordedDeaths.GetValueOrDefault(userId);
        // A pure SSD transfer keeps the original pool timestamp and does not create a new death timer.
        var disconnectedAlive = ev.Player.Status is SessionStatus.Disconnected or SessionStatus.Zombie &&
                                !_mobState.IsDead(ev.Entity);
        _pendingDeaths[userId] = new DeathTransition(
            disconnectedAlive ||
            recorded.PreservePoolTime ||
            preserveNextDeath ||
            PreservesPoolTime(ev.Entity),
            disconnectedAlive || recorded.BypassDeathTimer || BypassesDeathTimer(ev.Entity));
    }

    private bool PreservesPoolTime(EntityUid uid)
    {
        return BypassesDeathTimer(uid) ||
               Transform(uid).MapUid is { } map && HasComp<ThunderdomeMapComponent>(map);
    }

    private bool BypassesDeathTimer(EntityUid uid)
    {
        if (HasComp<XenoParasiteComponent>(uid) ||
            TryComp(uid, out XenoComponent? xeno) && xeno.Role == LesserDroneRole)
        {
            return true;
        }

        return false;
    }

    private void OnBurrowedLarvaAdded(BurrowedLarvaAddedEvent ev)
    {
        if (_hiveQuery.HasComp(GetEntity(ev.Hive)))
            _refreshRequested = true;
    }

    private void OnAbandonedAvailableStartup(Entity<AbandonedXenoPoolAvailableComponent> ent, ref ComponentStartup args)
    {
        RequestAssignmentFor(ent.Owner);
    }

    private void OnAvailableStartup(Entity<LarvaPoolAvailableComponent> ent, ref ComponentStartup args)
    {
        RequestAssignmentFor(ent.Owner);
    }

    private void OnAvailableHiveChanged(Entity<LarvaPoolAvailableComponent> ent, ref HiveChangedEvent args)
    {
        RequestAssignmentFor(ent.Owner);
    }

    private void OnAvailableMindRemoved(Entity<LarvaPoolAvailableComponent> ent, ref MindRemovedMessage args)
    {
        RequestAssignmentFor(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        ExpirePendingOffers(time);
        if (_nextPositionUpdate == default)
            _nextPositionUpdate = time + PositionUpdateInterval;

        var updatePositions = time >= _nextPositionUpdate;
        if (!_refreshRequested && time < _nextRefresh && !updatePositions)
            return;

        if (_refreshRequested || time >= _nextRefresh)
        {
            _refreshRequested = false;
            _nextRefresh = time + RefreshInterval;
            TryClaimForAllHives();
        }

        if (updatePositions)
        {
            _nextPositionUpdate = time + PositionUpdateInterval;
            NotifyPoolPositions();
        }
    }

    private void TryClaimForAllHives()
    {
        _strandedXenoHives.Clear();
        foreach (var credit in _strandedXenoCredits)
        {
            if (!_strandedXenoHives.Contains(credit.Hive))
                _strandedXenoHives.Add(credit.Hive);
        }

        foreach (var hiveId in _strandedXenoHives)
        {
            if (_hiveQuery.TryComp(hiveId, out var hive))
                TryClaimForHive((hiveId, hive));
        }

        var hives = EntityQueryEnumerator<HiveComponent>();
        while (hives.MoveNext(out var hiveId, out var hive))
        {
            if (_strandedXenoHives.Contains(hiveId))
                continue;

            TryClaimForHive((hiveId, hive));
        }

        _strandedXenoHives.Clear();
    }

    public void RequestAssignmentFor(EntityUid uid)
    {
        if (TryComp(uid, out HiveMemberComponent? member) &&
            member.Hive is { } hiveId &&
            _hiveQuery.HasComp(hiveId))
        {
            if (TryComp(uid, out GhostRoleComponent? ghostRole))
                _ghostRole.UnregisterGhostRole((uid, ghostRole));

            _refreshRequested = true;
        }
    }

    /// <summary>
    /// Preserves a stranded xeno's pool time and gives them first claim on their hive's replacement larva.
    /// </summary>
    public void CreditStrandedXeno(Entity<HiveComponent> hive, NetUserId userId)
    {
        _preserveNextDeath.Add(userId);
        RemoveStrandedXenoCredit(userId);
        _strandedXenoCredits.Insert(0, new StrandedXenoCredit(userId, hive.Owner));
        _refreshRequested = true;
    }

    public void OptInStaff(NetUserId userId)
    {
        _staffOptIns.Add(userId);
        _refreshRequested = true;
    }

    private void OnSetLarvaPoolOptIn(Entity<JoinXenoComponent> ent, ref SetLarvaPoolOptInBuiMsg args)
    {
        if (args.Actor != ent.Owner ||
            !TryComp(ent, out ActorComponent? actor) ||
            !TryGetEntity(args.Hive, out var hive) ||
            !_hiveQuery.HasComp(hive.Value))
        {
            return;
        }

        var hiveId = GetHivePreferenceId(hive.Value);
        if (!_larvaPoolPreferences.SetOptedIn(actor.PlayerSession.UserId, hiveId, args.OptedIn))
            return;

        if (!args.OptedIn &&
            _pendingOffers.TryGetValue(actor.PlayerSession.UserId, out var pending) &&
            pending.Hive == hive.Value)
        {
            CancelPendingOffer(actor.PlayerSession.UserId, LarvaPoolOfferCancellation.OptedOut);
        }

        _refreshRequested = true;
    }

    private void TryClaimForHive(Entity<HiveComponent> hive)
    {
        TryOfferForHive(hive);
    }

    private void PrioritizeStrandedXenos(EntityUid hive, List<LarvaPoolCandidate> candidates)
    {
        var insertAt = 0;
        foreach (var credit in _strandedXenoCredits)
        {
            if (credit.Hive != hive)
                continue;

            var candidateIndex = candidates.FindIndex(candidate => candidate.Session.UserId == credit.UserId);
            if (candidateIndex < 0)
                continue;

            var candidate = candidates[candidateIndex];
            candidates.RemoveAt(candidateIndex);
            candidates.Insert(insertAt, candidate);
            insertAt++;
        }
    }

    private void RemoveStrandedXenoCredit(NetUserId userId)
    {
        _strandedXenoCredits.RemoveAll(credit => credit.UserId == userId);
    }

    private bool TryGetAvailableLarva(Entity<HiveComponent> hive, out EntityUid larva)
    {
        var query = EntityQueryEnumerator<LarvaPoolAvailableComponent, HiveMemberComponent>();
        while (query.MoveNext(out var uid, out _, out var member))
        {
            if (!CanAssignLarva(uid, member, hive))
                continue;

            larva = uid;
            return true;
        }

        larva = default;
        return false;
    }

    private bool CanAssignLarva(EntityUid uid, HiveMemberComponent member, Entity<HiveComponent> hive)
    {
        if (!CanAssignBody(uid, member, hive, out var xeno) || IsReservedForParasiteClaim(uid))
            return false;

        return _tag.HasTag(uid, LarvaTag) && xeno.Role == LarvaRole;
    }

    private bool IsReservedForParasiteClaim(EntityUid uid)
    {
        if (!TryComp(uid, out BursterComponent? burster) ||
            !TryComp(burster.BurstFrom, out VictimInfectedComponent? infected) ||
            infected.SpawnedLarva != uid ||
            !(infected.InfectorWantsLarva || infected.InfectorLarvaClaimPending) ||
            infected.InfectorUser is not { } userId)
        {
            return false;
        }

        return _player.TryGetSessionById(userId, out var session) &&
               session.AttachedEntity is { } attached &&
               _ghostQuery.HasComp(attached) &&
               _mind.TryGetMind(session, out _, out _);
    }

    private bool TryGetAbandonedXeno(Entity<HiveComponent> hive, out EntityUid abandoned)
    {
        var query = EntityQueryEnumerator<AbandonedXenoPoolAvailableComponent, LarvaPoolAvailableComponent, HiveMemberComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var member))
        {
            if (!CanAssignAbandonedXeno(uid, member, hive))
                continue;

            abandoned = uid;
            return true;
        }

        abandoned = default;
        return false;
    }

    private bool CanAssignAbandonedXeno(EntityUid uid, HiveMemberComponent member, Entity<HiveComponent> hive)
    {
        return HasComp<AbandonedXenoPoolAvailableComponent>(uid) &&
               CanAssignBody(uid, member, hive, out _);
    }

    private bool CanAssignBody(EntityUid uid, HiveMemberComponent member, Entity<HiveComponent> hive, out XenoComponent xeno)
    {
        xeno = default!;
        if (!TryComp(uid, out XenoComponent? xenoComp))
            return false;

        xeno = xenoComp;
        var isQueen = xeno.Role == QueenRole;

        if (member.Hive != hive.Owner ||
            TerminatingOrDeleted(uid) ||
            MetaData(uid).EntityPaused ||
            _pendingOfferTargets.ContainsKey(uid) ||
            HasComp<XenoEvolutionTransferComponent>(uid) ||
            HasComp<LarvaPoolClaimBlockedComponent>(uid) ||
            HasComp<XenoRecentlyDevolvedComponent>(uid) ||
            HasComp<ActorComponent>(uid) ||
            _mobState.IsDead(uid) ||
            HasComp<XenoParasiteComponent>(uid) ||
            HasComp<DropshipHijackerComponent>(uid) && !isQueen ||
            TryComp(uid, out MindContainerComponent? mind) && mind.HasMind)
        {
            return false;
        }

        return xeno.Role != LesserDroneRole;
    }

    private bool AssignXeno(EntityUid uid, ICommonSession session)
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

    private List<LarvaPoolCandidate> GetCandidates()
    {
        var candidates = new List<LarvaPoolCandidate>();
        foreach (var session in _player.Sessions)
        {
            if (session.Status is SessionStatus.Disconnected or SessionStatus.Zombie ||
                session.AttachedEntity is not { } attached)
            {
                continue;
            }

            if (!_larvaPoolPreferences.IsLoaded(session.UserId))
                continue;

            GhostComponent? ghost = null;
            if (_ghostQuery.TryComp(attached, out var ghostComp))
            {
                ghost = ghostComp;
                _pool.RecordJoined(session.UserId, GetGhostDeathTime(ghostComp));
            }
            else if (!IsActivePoolCandidate(attached))
            {
                continue;
            }

            if (GetEligibility(session, attached, ghost).Eligibility != LarvaPoolEligibility.Eligible)
                continue;

            candidates.Add(new LarvaPoolCandidate(session, attached));
        }

        candidates.Sort((left, right) => _pool.Compare(left.Session.UserId, right.Session.UserId));
        return candidates;
    }

    private List<LarvaPoolCandidate> GetCandidatesForHive(EntityUid hive)
    {
        var hiveId = GetHivePreferenceId(hive);
        return GetCandidates()
            .Where(candidate =>
                _larvaPoolPreferences.TryGetOptedIn(candidate.Session.UserId, hiveId, out var optedIn) &&
                optedIn)
            .ToList();
    }

    private string GetHivePreferenceId(EntityUid hive)
    {
        return MetaData(hive).EntityPrototype?.ID ?? Name(hive);
    }

    private void OnGetLarvaPoolCandidateCount(GetLarvaPoolCandidateCountEvent args)
    {
        if (args.Hive is { } hive && _hiveQuery.HasComp(hive))
        {
            args.Count = GetCandidatesForHive(hive).Count;
            return;
        }

        var candidates = GetCandidates();
        args.Count = candidates.Count(candidate => IsOptedIntoAnyHive(candidate.Session.UserId));
    }

    private bool IsOptedIntoAnyHive(NetUserId userId)
    {
        var hives = EntityQueryEnumerator<HiveComponent>();
        while (hives.MoveNext(out var hiveId, out _))
        {
            if (_larvaPoolPreferences.TryGetOptedIn(
                    userId,
                    GetHivePreferenceId(hiveId),
                    out var optedIn) &&
                optedIn)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsActivePoolCandidate(EntityUid uid)
    {
        return !_mobState.IsDead(uid) &&
               (HasComp<XenoParasiteComponent>(uid) ||
                TryComp(uid, out XenoComponent? xeno) && xeno.Role == LesserDroneRole);
    }

    private LarvaPoolEligibilityResult GetEligibility(
        ICommonSession session,
        EntityUid attached,
        GhostComponent? ghost)
    {
        if (!_preferences.TryGetCachedPreferences(session.UserId, out var preferences) ||
            preferences.SelectedCharacter is not HumanoidCharacterProfile)
        {
            return new LarvaPoolEligibilityResult(
                LarvaPoolEligibility.Ineligible,
                LarvaPoolIneligibilityReason.CharacterProfileUnavailable);
        }

        var jobBans = _bans.GetJobBans(session.UserId);
        if (jobBans == null)
        {
            return new LarvaPoolEligibilityResult(
                LarvaPoolEligibility.Ineligible,
                LarvaPoolIneligibilityReason.PreferenceDataLoading);
        }

        if (jobBans.Contains(SelectableXenoRole))
        {
            return new LarvaPoolEligibilityResult(
                LarvaPoolEligibility.Ineligible,
                LarvaPoolIneligibilityReason.RoleBanned);
        }

        var allowed = new IsJobAllowedEvent(session, SelectableXenoRole);
        RaiseLocalEvent(ref allowed);
        if (allowed.Cancelled)
        {
            return new LarvaPoolEligibilityResult(
                LarvaPoolEligibility.Ineligible,
                LarvaPoolIneligibilityReason.RoleRequirements);
        }

        if (HasRevivableBody(session))
        {
            return new LarvaPoolEligibilityResult(
                LarvaPoolEligibility.Ineligible,
                LarvaPoolIneligibilityReason.RevivableBody);
        }

        if (_admin.HasAdminFlag(session, AdminFlags.Moderator) && !_staffOptIns.Contains(session.UserId))
        {
            return new LarvaPoolEligibilityResult(
                LarvaPoolEligibility.Ineligible,
                LarvaPoolIneligibilityReason.StaffProtected);
        }

        if (ghost != null)
        {
            var wait = TimeSpan.FromSeconds(_config.GetCVar(RMCCVars.RMCLarvaPoolWaitSeconds));
            var deathTime = _deathTimes.GetValueOrDefault(session.UserId, GetGhostDeathTime(ghost));
            if (!_admin.HasAdminFlag(session, AdminFlags.Admin) &&
                !HasComp<JoinXenoCooldownIgnoreComponent>(attached) &&
                _timing.CurTime - deathTime < wait)
            {
                return new LarvaPoolEligibilityResult(LarvaPoolEligibility.Waiting, LarvaPoolIneligibilityReason.None);
            }
        }

        return new LarvaPoolEligibilityResult(LarvaPoolEligibility.Eligible, LarvaPoolIneligibilityReason.None);
    }

    private bool HasRevivableBody(ICommonSession session)
    {
        if (!_mind.TryGetMind(session, out _, out var mind) ||
            mind.OriginalOwnedEntity is not { } originalNet ||
            !TryGetEntity(originalNet, out var original) ||
            !HasComp<RMCRevivableComponent>(original) ||
            !_mobState.IsDead(original.Value))
        {
            return false;
        }

        return !_unrevivable.IsUnrevivable(original.Value);
    }

    private void OnGetLarvaPoolStatus(GetLarvaPoolStatusEvent args)
    {
        if (!_player.TryGetSessionById(args.UserId, out var session) ||
            session.AttachedEntity is not { } attached ||
            !_ghostQuery.TryComp(attached, out var ghost))
        {
            return;
        }

        // Requesting pool status is a deliberate opt-in for protected staff.
        if (_admin.HasAdminFlag(session, AdminFlags.Moderator))
            OptInStaff(args.UserId);

        _pool.RecordJoined(args.UserId, GetGhostDeathTime(ghost));
        var eligibility = GetEligibility(session, attached, ghost);
        var candidates = GetCandidates();
        var hives = EntityQueryEnumerator<HiveComponent>();
        while (hives.MoveNext(out var hiveId, out _))
        {
            var hivePreferenceId = GetHivePreferenceId(hiveId);
            var preferenceLoaded = _larvaPoolPreferences.TryGetOptedIn(
                args.UserId,
                hivePreferenceId,
                out var optedIn);
            var activeCandidates = candidates
                .Where(candidate =>
                    _larvaPoolPreferences.TryGetOptedIn(
                        candidate.Session.UserId,
                        hivePreferenceId,
                        out var candidateOptedIn) &&
                    candidateOptedIn)
                .Select(candidate => candidate.Session.UserId);
            var position = _pool.GetPosition(args.UserId, activeCandidates);

            var status = eligibility.Eligibility switch
            {
                LarvaPoolEligibility.Eligible => LarvaPoolStatus.Eligible,
                LarvaPoolEligibility.Waiting => LarvaPoolStatus.Waiting,
                _ => LarvaPoolStatus.Ineligible,
            };
            var reason = eligibility.IneligibilityReason;

            if (!preferenceLoaded)
            {
                status = LarvaPoolStatus.Ineligible;
                reason = LarvaPoolIneligibilityReason.PreferenceDataLoading;
            }
            else if (!optedIn)
            {
                status = LarvaPoolStatus.Ineligible;
                reason = LarvaPoolIneligibilityReason.OptedOut;
            }

            args.Pools[hiveId] = new LarvaPoolUserStatus(
                status,
                position,
                reason,
                preferenceLoaded,
                optedIn);
        }
    }

    private void NotifyPoolPositions()
    {
        var hives = EntityQueryEnumerator<HiveComponent>();
        while (hives.MoveNext(out var hiveId, out _))
        {
            var candidates = GetCandidatesForHive(hiveId);
            PrioritizeStrandedXenos(hiveId, candidates);
            for (var i = 0; i < candidates.Count; i++)
            {
                _popup.PopupEntity(
                    Loc.GetString(
                        "rmc-xeno-larva-pool-position-hive",
                        ("position", i + 1),
                        ("hive", Name(hiveId))),
                    candidates[i].OfferEntity,
                    candidates[i].OfferEntity);
            }
        }
    }

    private TimeSpan GetGhostDeathTime(GhostComponent ghost)
    {
        var elapsed = _timing.RealTime - ghost.TimeOfDeath;
        return elapsed > TimeSpan.Zero ? _timing.CurTime - elapsed : _timing.CurTime;
    }

    private enum LarvaPoolEligibility : byte
    {
        Ineligible,
        Waiting,
        Eligible,
    }

    private readonly record struct LarvaPoolEligibilityResult(
        LarvaPoolEligibility Eligibility,
        LarvaPoolIneligibilityReason IneligibilityReason);

    private readonly record struct DeathTransition(bool PreservePoolTime, bool BypassDeathTimer);

    private readonly record struct StrandedXenoCredit(NetUserId UserId, EntityUid Hive);

    private readonly record struct LarvaPoolCandidate(ICommonSession Session, EntityUid OfferEntity);
}
