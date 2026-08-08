using Content.Server._CMU14.Spawners;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;

namespace Content.Server.Spawners.EntitySystems;

public sealed partial class SpawnPointSystem
{
    [Dependency] private CMURoundSpawnPointSnapshotSystem _roundSpawnPoints = default!;

    private bool TrySpawnAtCachedPoint(PlayerSpawningEvent args)
    {
        if (!_roundSpawnPoints.Active)
            return false;

        var selection = _roundSpawnPoints.PickGeneric(
            args.Station,
            args.Job,
            _gameTicker.RunLevel == GameRunLevel.InRound,
            _random,
            out var cachedPoint);
        if (selection == CMUGenericSpawnSelection.None ||
            !TryComp(cachedPoint, out TransformComponent? cachedTransform))
        {
            return false;
        }

        if (selection == CMUGenericSpawnSelection.Fallback)
            LogFallbackSpawnPoint(args);

        SpawnAt(cachedTransform.Coordinates, args);
        return true;
    }
}
