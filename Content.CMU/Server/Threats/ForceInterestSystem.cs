using System.Linq;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.CMU14.Threats;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Threats;

/// <summary>Holds force deployments until enough eligible observers volunteer.</summary>
public sealed partial class ForceInterestSystem : EntitySystem
{
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private GhostRoleSystem _ghostRole = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan ClaimDuration = TimeSpan.FromSeconds(240);
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(30);
    private readonly Dictionary<uint, PendingForce> _forces = new();
    private readonly Dictionary<EntityUid, TimeSpan> _unclaimed = new();
    private readonly ISawmill _sawmill = Logger.GetSawmill("force-interest");
    private uint _nextIdentifier;
    private TimeSpan _nextRefresh;
    private bool _roundEnded;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<UnclaimedForceRoleComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _forces.Clear();
        _unclaimed.Clear();
        _nextRefresh = TimeSpan.Zero;
        _roundEnded = false;
        // Identifiers remain unique so a message from an old menu cannot target a new round's force.
        _ghostRole.UpdateAllEui();
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.PostRound)
            return;

        _roundEnded = true;
        _forces.Clear();
        _ghostRole.UpdateAllEui();
    }

    /// <summary>Counts playable roles, excluding equipment and other non-player entities.</summary>
    public Dictionary<string, int> GetPlayableBodies(IReadOnlyDictionary<string, int> bodies, bool threatBodies = false)
    {
        var result = new Dictionary<string, int>();
        foreach (var (id, count) in bodies)
        {
            if (count <= 0 || !_prototypes.TryIndex<EntityPrototype>(id, out var prototype))
                continue;

            if (!threatBodies && !prototype.TryComp<GhostRoleComponent>(out _, _factory))
                continue;

            result[id] = count;
        }

        return result;
    }

    /// <summary>Registers a deployment. A successful request means queued, not yet spawned.</summary>
    public uint QueueForce(string name, IReadOnlyDictionary<string, int> bodies, Func<IReadOnlySet<NetUserId>, bool> spawn,
        bool ready = true, IEnumerable<NetUserId>? interested = null, IReadOnlyDictionary<string, ProtoId<JobPrototype>>? fallbackJobs = null)
    {
        var id = ++_nextIdentifier;
        var pending = new PendingForce(name, bodies, spawn, ready, fallbackJobs);
        _forces.Add(id, pending);
        if (interested != null)
        {
            foreach (var player in interested)
            {
                if (CanJoin(pending, player))
                    pending.Interested.Add(player);
            }
        }

        _ghostRole.UpdateAllEui();
        return id;
    }

    public void SetReady(uint id)
    {
        if (!_forces.TryGetValue(id, out var force))
            return;

        force.Ready = true;
        _ghostRole.UpdateAllEui();
    }

    public bool IsPending(uint id) => _forces.ContainsKey(id);

    public ForceInterestInfo[] GetForces(ICommonSession player)
    {
        return _forces.Where(pair => pair.Value.Ready).Select(pair => new ForceInterestInfo(pair.Key, pair.Value.Name,
            pair.Value.TotalRoles, pair.Value.Interested.Count, ForceInterest.RequiredPlayers(pair.Value.TotalRoles),
            pair.Value.Ready, pair.Value.Interested.Contains(player.UserId), CanJoin(pair.Value, player.UserId))).ToArray();
    }

    public void SetInterest(ICommonSession player, uint id, bool interested)
    {
        if (!_forces.TryGetValue(id, out var force))
            return;

        if (!interested)
            force.Interested.Remove(player.UserId);
        else if (force.Ready && CanJoin(force, player.UserId))
            force.Interested.Add(player.UserId);

        _ghostRole.UpdateAllEui();
    }

    private bool CanJoin(PendingForce force, NetUserId player)
    {
        return _players.TryGetSessionById(player, out var session) &&
            force.Bodies.Keys.Any(id => _ghostRole.CanJoinForceRole(session, id,
                force.FallbackJobs != null && force.FallbackJobs.TryGetValue(id, out var job) ? job : (ProtoId<JobPrototype>?) null));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_roundEnded || _timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + RefreshInterval;
        var changed = false;
        foreach (var (id, force) in _forces.ToArray())
        {
            changed |= force.Interested.RemoveWhere(player => !CanJoin(force, player)) > 0;
            if (!force.Ready || force.RetryAt > _timing.CurTime ||
                force.Interested.Count < ForceInterest.RequiredPlayers(force.TotalRoles))
                continue;

            // Remove before invoking game code, which can queue additional forces.
            _forces.Remove(id);
            try
            {
                if (!force.Spawn(force.Interested))
                {
                    force.RetryAt = _timing.CurTime + RetryInterval;
                    _forces.Add(id, force);
                }
            }
            catch (Exception ex)
            {
                // Do not retry an exception: a partially completed deployment must not be duplicated.
                _sawmill.Error($"Force deployment '{force.Name}' failed: {ex}");
            }

            changed = true;
        }

        foreach (var (entity, deadline) in _unclaimed.ToArray())
        {
            if (Deleted(entity) || IsClaimed(entity))
            {
                StopTracking(entity);
                continue;
            }

            if (_timing.CurTime < deadline)
                continue;

            _unclaimed.Remove(entity);
            QueueDel(entity);
        }

        if (changed)
            _ghostRole.UpdateAllEui();
    }

    /// <summary>Starts the claim window only for a newly deployed, unoccupied ghost role.</summary>
    public void TrackRole(EntityUid entity)
    {
        if (!HasComp<GhostRoleComponent>(entity) || IsClaimed(entity))
            return;

        EnsureComp<UnclaimedForceRoleComponent>(entity);
        _unclaimed.TryAdd(entity, _timing.CurTime + ClaimDuration);
    }

    private bool IsClaimed(EntityUid entity)
    {
        return HasComp<ActorComponent>(entity) ||
            TryComp<MindContainerComponent>(entity, out var mind) && (mind.HasMind || mind.LastMind != null) ||
            TryComp<GhostRoleComponent>(entity, out var role) && role.Taken ||
            TryComp<GhostRoleMobSpawnerComponent>(entity, out var spawner) && spawner.CurrentTakeovers > 0;
    }

    private void OnMindAdded(EntityUid uid, UnclaimedForceRoleComponent comp, MindAddedMessage args)
        => StopTracking(uid);

    private void StopTracking(EntityUid entity)
    {
        _unclaimed.Remove(entity);
        if (!Deleted(entity))
            RemComp<UnclaimedForceRoleComponent>(entity);
    }

    private sealed class PendingForce(string name, IReadOnlyDictionary<string, int> bodies,
        Func<IReadOnlySet<NetUserId>, bool> spawn, bool ready, IReadOnlyDictionary<string, ProtoId<JobPrototype>>? fallbackJobs)
    {
        public readonly string Name = name;
        public readonly IReadOnlyDictionary<string, int> Bodies = bodies;
        public readonly int TotalRoles = bodies.Values.Sum();
        public readonly Func<IReadOnlySet<NetUserId>, bool> Spawn = spawn;
        public readonly IReadOnlyDictionary<string, ProtoId<JobPrototype>>? FallbackJobs = fallbackJobs;
        public readonly HashSet<NetUserId> Interested = new();
        public bool Ready = ready;
        public TimeSpan RetryAt;
    }
}
