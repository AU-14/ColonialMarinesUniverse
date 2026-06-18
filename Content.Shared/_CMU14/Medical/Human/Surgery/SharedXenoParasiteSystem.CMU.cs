using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Parasite;

public abstract partial class SharedXenoParasiteSystem
{
    public bool TryCutLarvaRootsForCmuSurgery(EntityUid victim)
    {
        if (!TryComp<VictimInfectedComponent>(victim, out var infected) ||
            infected.IsBursting)
        {
            return false;
        }

        infected.RootsCut = true;
        Dirty(victim, infected);
        return true;
    }

    public bool TryRemoveLarvaForCmuSurgery(
        EntityUid victim,
        EntProtoId deadLarvaItem,
        out EntityUid? removedLarva)
    {
        removedLarva = null;
        if (!TryComp<VictimInfectedComponent>(victim, out var infected) ||
            infected.IsBursting ||
            !infected.RootsCut ||
            !TryComp(victim, out TransformComponent? transform))
        {
            return false;
        }

        var coords = transform.Coordinates;
        if (infected.SpawnedLarva != null &&
            _container.TryGetContainer(victim, infected.LarvaContainerId, out var container))
        {
            foreach (var larva in container.ContainedEntities)
            {
                RemCompDeferred<BursterComponent>(larva);
                removedLarva ??= larva;
            }

            _container.EmptyContainer(container, destination: coords);
        }
        else
        {
            removedLarva = SpawnAtPosition(deadLarvaItem, coords);
        }

        RemCompDeferred<VictimInfectedComponent>(victim);
        return true;
    }
}
