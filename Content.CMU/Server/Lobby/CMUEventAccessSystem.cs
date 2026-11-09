using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Station.Events;
using Content.Shared.CMU14.Lobby;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Lobby;

/// <summary>Round-local invitation gates, checked independently of job bans and whitelists.</summary>
public sealed partial class CMUEventAccessSystem : EntitySystem
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private CMUEventJobTargetsSystem _targets = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;

    private sealed record AccessGroup(string Code, HashSet<ProtoId<JobPrototype>> Jobs)
    {
        public readonly HashSet<NetUserId> Players = new();
    }

    private readonly Dictionary<string, AccessGroup> _groups = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<NetUserId, TimeSpan> _nextAttempt = new();
    private static readonly TimeSpan AttemptDelay = TimeSpan.FromSeconds(2);

    public IEnumerable<string> GroupNames => _groups.Keys;
    public bool Enabled { get; private set; }
    public event Action? Changed;

    public void SetEnabled(bool enabled)
    {
        if (Enabled == enabled)
            return;

        Enabled = enabled;
        SendStates();
        Changed?.Invoke();
    }

    public bool TryConfigureGroup(string name, string code, string selector, IReadOnlyList<string> roles, out string message)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code) ||
            name.Length > CMUEventCodeRequestEvent.MaxCodeLength || code.Length > CMUEventCodeRequestEvent.MaxCodeLength)
        {
            message = Loc.GetString("cmu-event-access-invalid-code");
            return false;
        }
        if (!_targets.TryResolveSide(selector, out var side))
        {
            message = Loc.GetString("cmu-event-invalid-side", ("side", selector));
            return false;
        }

        var jobs = new HashSet<ProtoId<JobPrototype>>();
        if (roles.Count is 0 or > 256)
        {
            message = Loc.GetString("cmu-event-setup-select-roles");
            return false;
        }
        foreach (var role in roles)
        {
            var matches = _targets.GetJobs(side, role);
            if (matches.Count == 0)
            {
                message = Loc.GetString("cmu-event-invalid-role", ("side", selector), ("role", role));
                return false;
            }
            jobs.UnionWith(matches.Select(job => new ProtoId<JobPrototype>(job.ID)));
        }
        SetGroup(name, code, jobs);
        message = Loc.GetString("cmu-event-access-set", ("group", name), ("jobs", jobs.Count));
        return true;
    }

    public List<CMUEventGroupInfo> GetGroups() => _groups.Select(pair =>
        new CMUEventGroupInfo(pair.Key, pair.Value.Jobs.Select(job => job.Id).OrderBy(id => id).ToList(),
            pair.Value.Players.Count)).ToList();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CMUEventCodeRequestEvent>(OnCodeRequest);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<StationJobsGetCandidatesEvent>(OnGetCandidates);
        SubscribeLocalEvent<GetDisallowedJobsEvent>(OnGetDisallowedJobs);
        SubscribeLocalEvent<IsRoleAllowedEvent>(OnIsRoleAllowed);
    }

    public void SetGroup(string name, string code, HashSet<ProtoId<JobPrototype>> jobs)
    {
        // Replacing a group also revokes its previous invitations, including on code rotation.
        _groups[name] = new AccessGroup(code, jobs);
        SendStates();
        Changed?.Invoke();
    }

    public bool RemoveGroup(string name)
    {
        if (!_groups.Remove(name))
            return false;

        SendStates();
        Changed?.Invoke();
        return true;
    }

    public IEnumerable<(string Name, int Jobs, int Players)> DescribeGroups() =>
        _groups.Select(pair => (pair.Key, pair.Value.Jobs.Count, pair.Value.Players.Count));

    public bool CanAccessJob(NetUserId player, ProtoId<JobPrototype> job) =>
        !Enabled || _groups.Values.All(group => !group.Jobs.Contains(job) || group.Players.Contains(player));

    public HumanoidCharacterProfile GetEffectiveProfile(NetUserId player, HumanoidCharacterProfile profile)
    {
        if (!Enabled)
            return profile;

        var jobs = CMUEventJobCatalog.GetJobs(_prototypes);
        var govfor = jobs.Any(job => job.RoundSide == RoundJobSide.Govfor && CanAccessJob(player, job.ID));
        var opfor = jobs.Any(job => job.RoundSide == RoundJobSide.Opfor && CanAccessJob(player, job.ID));
        if (govfor == opfor)
            return profile;

        // Apply the round's invitation side without overwriting the player's saved preference.
        return profile.WithForceOnForcePreferences(govfor ? ForceOnForceSide.Govfor : ForceOnForceSide.Opfor,
            profile.FoFFallback);
    }

    public bool TryRedeem(ICommonSession player, string code, out string message)
    {
        if (!Enabled)
        {
            message = Loc.GetString("cmu-event-disabled");
            return false;
        }
        if (!_ticker.PlayerGameStatuses.TryGetValue(player.UserId, out var status) ||
            status == PlayerGameStatus.JoinedGame)
        {
            message = Loc.GetString("cmu-event-code-lobby-only");
            return false;
        }

        if (_timing.RealTime < _nextAttempt.GetValueOrDefault(player.UserId))
        {
            message = Loc.GetString("cmu-event-code-cooldown");
            return false;
        }

        _nextAttempt[player.UserId] = _timing.RealTime + AttemptDelay;
        if (string.IsNullOrWhiteSpace(code) || code.Length > CMUEventCodeRequestEvent.MaxCodeLength)
        {
            message = Loc.GetString("cmu-event-code-invalid");
            return false;
        }

        var matched = new List<string>();
        foreach (var (name, group) in _groups)
        {
            if (!string.Equals(group.Code, code, StringComparison.Ordinal))
                continue;

            group.Players.Add(player.UserId);
            matched.Add(name);
        }

        if (matched.Count == 0)
        {
            message = Loc.GetString("cmu-event-code-invalid");
            return false;
        }

        SendState(player);
        Changed?.Invoke();
        message = Loc.GetString("cmu-event-code-accepted", ("groups", string.Join(", ", matched)));
        return true;
    }

    private void OnCodeRequest(CMUEventCodeRequestEvent ev, EntitySessionEventArgs args)
    {
        TryRedeem(args.SenderSession, ev.Code, out var message);
        RaiseNetworkEvent(new CMUEventCodeResultEvent(message), args.SenderSession.Channel);
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev) => SendState(ev.PlayerSession);

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _groups.Clear();
        _nextAttempt.Clear();
        Enabled = false;
        SendStates();
        Changed?.Invoke();
    }

    private void OnGetCandidates(ref StationJobsGetCandidatesEvent ev)
    {
        var player = ev.Player;
        ev.Jobs.RemoveAll(job => !CanAccessJob(player, job));
    }

    private void OnGetDisallowedJobs(ref GetDisallowedJobsEvent ev)
    {
        ev.Jobs.UnionWith(GetLockedJobs(ev.Player.UserId));
    }

    private void OnIsRoleAllowed(ref IsRoleAllowedEvent ev)
    {
        if (ev.Jobs == null)
            return;

        foreach (var job in ev.Jobs)
        {
            if (!CanAccessJob(ev.Player.UserId, job))
                ev.Cancelled = true;
        }
    }

    public HashSet<ProtoId<JobPrototype>> GetLockedJobs(NetUserId player) => !Enabled ? [] : _groups.Values
        .Where(group => !group.Players.Contains(player)).SelectMany(group => group.Jobs).ToHashSet();

    private void SendState(ICommonSession player) =>
        RaiseNetworkEvent(new CMUEventAccessStateEvent(Enabled, GetLockedJobs(player.UserId)), player.Channel);

    private void SendStates()
    {
        foreach (var player in _players.Sessions)
            SendState(player);
    }
}
