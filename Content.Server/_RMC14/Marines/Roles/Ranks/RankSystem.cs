using System.Linq;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Marines.Roles.Ranks;

public sealed partial class RankSystem : SharedRankSystem
{
    [Dependency] private PlayTimeTrackingManager _tracking = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    // Store mob -> (player, jobId, profile) on spawn so we can reapply later
    private readonly Dictionary<EntityUid, PlayerSpawnCompleteEvent> _spawnData = new();
    private readonly HashSet<EntityUid> _pendingRanks = new();
    private TimeSpan _nextRankRetry;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RankComponent, TransformSpeakerNameEvent>(OnSpeakerNameTransform);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _spawnData.Clear();
        _pendingRanks.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_pendingRanks.Count == 0 || _timing.CurTime < _nextRankRetry)
            return;

        _nextRankRetry = _timing.CurTime + TimeSpan.FromSeconds(1);
        foreach (var mob in _pendingRanks.ToArray())
        {
            _pendingRanks.Remove(mob);
            if (TerminatingOrDeleted(mob) || !_spawnData.TryGetValue(mob, out var spawn)
                || spawn.Player.Status == SessionStatus.Disconnected)
            {
                _spawnData.Remove(mob);
                continue;
            }

            ApplyJobRank(mob);
        }
    }

    private void OnSpeakerNameTransform(Entity<RankComponent> ent, ref TransformSpeakerNameEvent args)
    {
        var name = GetSpeakerRankName(ent);
        if (name == null)
            return;

        args.VoiceName = name;
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null)
            return;

        _spawnData[ev.Mob] = ev;

        ApplyJobRank(ev.Mob);
    }

    public ProtoId<JobPrototype>? GetJobId(EntityUid mob) => _spawnData.TryGetValue(mob, out var ev) ? ev.JobId : null;

    public void ReapplyJobRank(EntityUid mob)
    {
        if (_spawnData.TryGetValue(mob, out var ev))
            ApplyJobRank(mob);
    }

    private void ApplyJobRank(EntityUid mob)
    {
        if (!_spawnData.TryGetValue(mob, out var ev))
            return;

        if (ev.JobId == null)
            return;

        if (!_prototypes.TryIndex<JobPrototype>(ev.JobId, out var jobPrototype))
            return;

        if (jobPrototype.Ranks == null)
            return;

        if (!_tracking.TryGetTrackerTimes(ev.Player, out var playTimes))
        {
            _pendingRanks.Add(mob);
            return;
        }

        foreach (var rank in jobPrototype.Ranks)
        {
            var failed = false;

            if (_prototypes.TryIndex<RankPrototype>(rank.Key, out var rankPrototype) && rankPrototype != null)
            {
                if (rank.Value != null)
                {
                    foreach (var req in rank.Value)
                    {
                        if (!req.Check(_entityManager, _prototypes, ev.Profile, playTimes, out _))
                            failed = true;
                    }
                }

                if (!failed)
                {
                    SetRank(mob, rankPrototype);
                    return;
                }
            }
        }
    }
}
