using Content.Shared.FixedPoint;

namespace Content.Shared._RMC14.Xenonids.Pheromones;

public abstract partial class SharedXenoPheromonesSystem
{
    // Rebuilt each update, so removed receivers and changing emitters cannot leave stale strengths.
    private readonly Dictionary<EntityUid, FixedPoint2> _cmuPreviousFrenzy = new();
}
