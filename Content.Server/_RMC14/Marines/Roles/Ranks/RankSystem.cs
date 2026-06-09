using Content.Server.Players.PlayTimeTracking;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Marines.Roles.Ranks;

public sealed partial class RankSystem : SharedRankSystem
{
    [Dependency] private PlayTimeTrackingManager _tracking = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    // Store mob -> (player, jobId, profile) on spawn so we can reapply later
    private readonly Dictionary<EntityUid, PlayerSpawnCompleteEvent> _spawnData = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RankComponent, TransformSpeakerNameEvent>(OnSpeakerNameTransform);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
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
            Log.Error($"Playtimes weren't ready yet for {ev.Player} on roundstart!");
            playTimes ??= new Dictionary<string, TimeSpan>();
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