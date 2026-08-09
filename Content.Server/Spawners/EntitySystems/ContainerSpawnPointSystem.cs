using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.Preferences;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server.Spawners.EntitySystems;

public sealed partial class ContainerSpawnPointSystem : EntitySystem
{
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeCmuRoundStartSnapshot(); // CMU14
        SubscribeLocalEvent<PlayerSpawningEvent>(HandlePlayerSpawning, before: new []{ typeof(SpawnPointSystem) });
    }

    public void HandlePlayerSpawning(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null)
            return;

        // If it's just a spawn pref check if it's for cryo (silly).
        if (args.HumanoidCharacterProfile?.SpawnPriority != SpawnPriorityPreference.Cryosleep &&
            (!ProtoMan.Resolve(args.Job, out var jobProto) || jobProto.JobEntity == null))
        {
            return;
        }

        var possibleContainers = new List<Entity<ContainerSpawnPointComponent, ContainerManagerComponent, TransformComponent>>();

        // CMU14: The synchronous round-start batch uses a validated snapshot instead of scanning the component stores
        // per player.
        if (!TryCollectCmuRoundStartContainers(args, possibleContainers))
        {
            var inRound = _gameTicker.RunLevel == GameRunLevel.InRound;
            var query = EntityQueryEnumerator<ContainerSpawnPointComponent, ContainerManagerComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var spawnPoint, out var container, out var xform))
            {
                var owningStation = args.Station == null
                    ? null
                    : _station.GetOwningStation(uid, xform);
                if (!CMUContainerSpawnPointBatchSnapshot.Matches(
                        owningStation,
                        spawnPoint.Job,
                        spawnPoint.SpawnType,
                        args.Station,
                        args.Job,
                        inRound))
                    continue;

                possibleContainers.Add((uid, spawnPoint, container, xform));
            }
        }

        if (possibleContainers.Count == 0)
            return;
        // we just need some default coords so we can spawn the player entity.
        var baseCoords = possibleContainers[0].Comp3.Coordinates;

        args.SpawnResult = _stationSpawning.SpawnPlayerMob(
            baseCoords,
            args.Job,
            args.HumanoidCharacterProfile,
            args.Station);

        _random.Shuffle(possibleContainers);
        foreach (var (uid, spawnPoint, manager, xform) in possibleContainers)
        {
            if (!_container.TryGetContainer(uid, spawnPoint.ContainerId, out var container, manager))
                continue;

            if (!_container.Insert(args.SpawnResult.Value, container, containerXform: xform))
                continue;

            var ev = new ContainerSpawnEvent(args.SpawnResult.Value);
            RaiseLocalEvent(uid, ref ev);

            return;
        }

        Del(args.SpawnResult);
        args.SpawnResult = null;
    }
}

/// <summary>
/// Raised on a container when a player is spawned into it.
/// </summary>
[ByRefEvent]
public record struct ContainerSpawnEvent(EntityUid Player);
