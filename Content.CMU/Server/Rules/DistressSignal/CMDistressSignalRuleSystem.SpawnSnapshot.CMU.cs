using Content.Server._CMU14.Spawners;
using Content.Server.GameTicking;
using Robust.Shared.Profiling;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Rules.DistressSignal;

public sealed partial class CMDistressSignalRuleSystem
{
    [Dependency] private ProfManager _prof = default!;
    [Dependency] private CMURoundSpawnPointSnapshotSystem _roundSpawnPoints = default!;

    private readonly List<EntityUid> _spawnPointCandidates = [];
    private readonly List<(EntProtoId SquadId, EntityUid Squad, int Players)> _squadCandidates = [];

    private Spawners? _roundStartSpawners;

    private void InitializeCmuSpawnSnapshot()
    {
        SubscribeLocalEvent<RoundStartPlayerSpawnBatchEvent>(OnRoundStartPlayerSpawnBatch);
        SubscribeLocalEvent<RoundStartPlayerSpawnBatchFinishedEvent>(OnRoundStartPlayerSpawnBatchFinished);
    }

    private void OnRoundStartPlayerSpawnBatch(ref RoundStartPlayerSpawnBatchEvent ev)
    {
        if (TryGetActiveRule() == null)
            return;

        using var profile = _prof.Group("CMU Distress Spawn Snapshot");
        _roundSpawnPoints.Prepare();
        _roundStartSpawners = GetSpawners(_roundSpawnPoints.Entries);
    }

    private void OnRoundStartPlayerSpawnBatchFinished(ref RoundStartPlayerSpawnBatchFinishedEvent ev)
    {
        ClearCmuSpawnSnapshot();
    }

    private Spawners GetSpawners(IReadOnlyList<CMURoundSpawnPointEntry> entries)
    {
        var spawners = new Spawners();
        CollectSquadSpawners(spawners);
        foreach (var entry in entries)
        {
            spawners.AddSpawnPoint(entry.Uid, entry.Job, entry.SpawnType);
        }

        return spawners;
    }

    private void ClearCmuSpawnSnapshot()
    {
        _roundStartSpawners = null;
        _spawnPointCandidates.Clear();
    }
}
