using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.CMU14.Diagnostics.Performance;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.Database;
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
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private GhostRoleSystem _ghostRole = default!;
    [Dependency] private ICMUServerPerformanceDiagnostics _performance = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan ClaimDuration = TimeSpan.FromSeconds(240);
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FallbackDeployDelay = TimeSpan.FromMinutes(10);
    private readonly Dictionary<uint, PendingForce> _forces = new();
    private readonly Dictionary<EntityUid, TimeSpan> _unclaimed = new();
    private readonly ISawmill _sawmill = Logger.GetSawmill("force-interest");
    private uint _nextIdentifier;
    private TimeSpan _nextRefresh;
    private bool _roundEnded;
    private (uint Id, PendingForce Force, IEnumerator<ForceDeploymentStep> Steps)? _deployment;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<UnclaimedForceRoleComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        CancelDeployment();
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
        CancelDeployment();
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
        => QueueDeployment(name, bodies, players => SpawnImmediately(spawn, players), ready, interested, fallbackJobs);

    /// <summary>
    /// Queues a deployment whose iterator yields after preparation and each body. Only one step runs per update,
    /// across all forces. An empty iterator means preparation failed and may be retried; exceptions are never retried.
    /// </summary>
    public uint QueueDeployment(string name, IReadOnlyDictionary<string, int> bodies,
        Func<IReadOnlySet<NetUserId>, IEnumerator<ForceDeploymentStep>> spawn,
        bool ready = true, IEnumerable<NetUserId>? interested = null,
        IReadOnlyDictionary<string, ProtoId<JobPrototype>>? fallbackJobs = null)
    {
        var id = ++_nextIdentifier;
        var pending = new PendingForce(name, bodies, spawn, ready, fallbackJobs);
        if (ready)
            pending.ReadyAt = _timing.CurTime;
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
        force.ReadyAt = _timing.CurTime;
        _ghostRole.UpdateAllEui();
    }

    public bool IsPending(uint id) => _forces.ContainsKey(id);

    public ForceInterestInfo[] GetForces(ICommonSession player)
    {
        var canComplete = _admins.HasAdminFlag(player, AdminFlags.Admin);
        return _forces.Where(pair => pair.Value.Ready).Select(pair => new ForceInterestInfo(pair.Key, pair.Value.Name,
            pair.Value.TotalRoles, pair.Value.Interested.Count, ForceInterest.RequiredPlayers(pair.Value.TotalRoles),
            pair.Value.Ready, pair.Value.Interested.Contains(player.UserId), CanJoin(pair.Value, player.UserId), canComplete)).ToArray();
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

    /// <summary>Admin override: deploys now with whoever is interested, skipping the interest wait.</summary>
    public void CompleteForce(ICommonSession player, uint id)
    {
        if (!_admins.HasAdminFlag(player, AdminFlags.Admin) || !_forces.TryGetValue(id, out var force))
            return;

        _adminLog.Add(LogType.AdminCommands, LogImpact.Medium,
            $"{player.Name} force-completed '{force.Name}' skipping the interest wait");
        Deploy(id, force);
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
        if (_roundEnded)
            return;

        if (_timing.CurTime < _nextRefresh)
        {
            AdvanceDeployment();
            return;
        }

        _nextRefresh = _timing.CurTime + RefreshInterval;
        var changed = false;
        foreach (var (id, force) in _forces.ToArray())
        {
            changed |= force.Interested.RemoveWhere(player => !CanJoin(force, player)) > 0;
            if (_deployment != null || !force.Ready || force.RetryAt > _timing.CurTime)
                continue;

            // Interest gets first crack at filling the force. Once the wait expires it deploys
            // with whoever signed up rather than sitting in the menu for the rest of the round.
            if (force.Interested.Count < ForceInterest.RequiredPlayers(force.TotalRoles)
                && _timing.CurTime < force.ReadyAt + FallbackDeployDelay)
                continue;

            Deploy(id, force);
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

        AdvanceDeployment();
    }

    private void AdvanceDeployment()
    {
        if (_deployment is not { } deployment)
            return;

        try
        {
            using var cost = _performance.MeasureOperation("force-deployment-step");
            if (deployment.Steps.MoveNext())
            {
                deployment.Force.Started = true;
                if (deployment.Steps.Current != ForceDeploymentStep.Completed)
                    return;
            }
            else if (!deployment.Force.Started)
            {
                deployment.Force.RetryAt = _timing.CurTime + RetryInterval;
                _forces.Add(deployment.Id, deployment.Force);
                _ghostRole.UpdateAllEui();
            }
            else
            {
                _sawmill.Warning($"Force deployment '{deployment.Force.Name}' stopped before completion; not retrying partial work.");
            }
        }
        catch (Exception ex)
        {
            // A failed step may already have created entities. Retrying would duplicate them.
            _sawmill.Error($"Force deployment '{deployment.Force.Name}' failed: {ex}");
        }

        CancelDeployment();
    }

    private void CancelDeployment()
    {
        var deployment = _deployment;
        _deployment = null;
        deployment?.Steps.Dispose();
    }

    private static IEnumerator<ForceDeploymentStep> SpawnImmediately(
        Func<IReadOnlySet<NetUserId>, bool> spawn, IReadOnlySet<NetUserId> players)
    {
        if (spawn(players))
            yield return ForceDeploymentStep.Completed;
    }

    private void Deploy(uint id, PendingForce force)
    {
        if (_deployment != null)
        {
            // One deployment at a time: starting a second orphans the running iterator and leaks its shuttle load.
            _sawmill.Warning($"Force deployment '{force.Name}' requested while another deployment is active; refused.");
            return;
        }

        // Remove before invoking game code, which can queue additional forces.
        _forces.Remove(id);
        try
        {
            force.Started = false;
            _deployment = (id, force, force.Spawn(force.Interested));
        }
        catch (Exception ex)
        {
            // Do not retry an exception: a partially completed deployment must not be duplicated.
            _sawmill.Error($"Force deployment '{force.Name}' failed: {ex}");
        }
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
        Func<IReadOnlySet<NetUserId>, IEnumerator<ForceDeploymentStep>> spawn, bool ready, IReadOnlyDictionary<string, ProtoId<JobPrototype>>? fallbackJobs)
    {
        public readonly string Name = name;
        public readonly IReadOnlyDictionary<string, int> Bodies = bodies;
        public readonly int TotalRoles = bodies.Values.Sum();
        public readonly Func<IReadOnlySet<NetUserId>, IEnumerator<ForceDeploymentStep>> Spawn = spawn;
        public readonly IReadOnlyDictionary<string, ProtoId<JobPrototype>>? FallbackJobs = fallbackJobs;
        public readonly HashSet<NetUserId> Interested = new();
        public bool Ready = ready;
        public bool Started;
        public TimeSpan ReadyAt;
        public TimeSpan RetryAt;
    }
}
