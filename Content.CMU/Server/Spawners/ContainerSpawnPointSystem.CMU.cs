using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Robust.Shared.Containers;
using Robust.Shared.Profiling;

namespace Content.Server.Spawners.EntitySystems;

public sealed partial class ContainerSpawnPointSystem
{
    [Dependency] private EntityQuery<MetaDataComponent> _cmuMetaQuery = default!;
    [Dependency] private ProfManager _cmuProf = default!;

    private readonly List<CMUContainerSpawnPointEntry> _cmuRoundStartCandidates = [];
    private readonly CMUContainerSpawnPointBatchSnapshot _cmuRoundStartSnapshot = new();

    internal bool CmuRoundStartSnapshotActive { get; private set; }

    private void InitializeCmuRoundStartSnapshot()
    {
        SubscribeLocalEvent<RoundStartPlayerSpawnBatchEvent>(OnCmuRoundStartPlayerSpawnBatch);
        SubscribeLocalEvent<RoundStartPlayerSpawnBatchFinishedEvent>(OnCmuRoundStartPlayerSpawnBatchFinished);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCmuRoundRestartCleanup);
    }

    private void OnCmuRoundStartPlayerSpawnBatch(ref RoundStartPlayerSpawnBatchEvent ev)
    {
        PrepareCmuRoundStartSnapshot();
    }

    private void OnCmuRoundStartPlayerSpawnBatchFinished(ref RoundStartPlayerSpawnBatchFinishedEvent ev)
    {
        ClearCmuRoundStartSnapshot();
    }

    private void OnCmuRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        ClearCmuRoundStartSnapshot();
    }

    private void PrepareCmuRoundStartSnapshot()
    {
        if (CmuRoundStartSnapshotActive)
            return;

        using var profile = _cmuProf.Group("CMU Round Container Spawn Snapshot");
        _cmuRoundStartSnapshot.Clear();
        var query = EntityQueryEnumerator<ContainerSpawnPointComponent, ContainerManagerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spawnPoint, out _, out var transform))
        {
            _cmuRoundStartSnapshot.Add(new CMUContainerSpawnPointEntry(
                uid,
                _station.GetOwningStation(uid, transform),
                spawnPoint.Job,
                spawnPoint.SpawnType,
                spawnPoint.ContainerId));
        }

        CmuRoundStartSnapshotActive = true;
        if (_cmuProf.IsEnabled)
            _cmuProf.WriteValue("CMU Round Container Spawn Entries", _cmuRoundStartSnapshot.Count);
    }

    private bool TryCollectCmuRoundStartContainers(
        PlayerSpawningEvent args,
        List<Entity<ContainerSpawnPointComponent, ContainerManagerComponent, TransformComponent>> possibleContainers)
    {
        if (!CmuRoundStartSnapshotActive)
            return false;

        var inRound = _gameTicker.RunLevel == GameRunLevel.InRound;
        _cmuRoundStartSnapshot.CopyCandidates(args.Station, args.Job, inRound, _cmuRoundStartCandidates);
        for (var i = 0; i < _cmuRoundStartCandidates.Count; i++)
        {
            var entry = _cmuRoundStartCandidates[i];
            if (!TryResolveCmuRoundStartEntry(entry, out var resolved))
            {
                possibleContainers.Clear();
                ClearCmuRoundStartSnapshot();
                return false;
            }

            possibleContainers.Add(resolved);
        }

        return true;
    }

    private bool TryResolveCmuRoundStartEntry(
        CMUContainerSpawnPointEntry entry,
        out Entity<ContainerSpawnPointComponent, ContainerManagerComponent, TransformComponent> resolved)
    {
        if (TerminatingOrDeleted(entry.Uid) ||
            !_cmuMetaQuery.TryGetComponent(entry.Uid, out var meta) ||
            meta.EntityPaused ||
            !TryComp(entry.Uid, out ContainerSpawnPointComponent? spawnPoint) ||
            !TryComp(entry.Uid, out ContainerManagerComponent? manager) ||
            !TryComp(entry.Uid, out TransformComponent? transform) ||
            _station.GetOwningStation(entry.Uid, transform) != entry.Station ||
            spawnPoint.Job != entry.Job ||
            spawnPoint.SpawnType != entry.SpawnType ||
            spawnPoint.ContainerId != entry.ContainerId)
        {
            resolved = default;
            return false;
        }

        resolved = (entry.Uid, spawnPoint, manager, transform);
        return true;
    }

    private void ClearCmuRoundStartSnapshot()
    {
        CmuRoundStartSnapshotActive = false;
        _cmuRoundStartCandidates.Clear();
        _cmuRoundStartSnapshot.Clear();
    }
}
