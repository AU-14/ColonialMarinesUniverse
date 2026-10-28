using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Xenonids.Evolution;

/// <summary>
///     Per-hive evolution tier queue. Lives on the hive entity; server-side state only.
///     Xenos without enough evolution points can reserve a spot for a tier, and contested
///     tier slots are handed out in queue order.
/// </summary>
[RegisterComponent]
[Access(typeof(CMUEvolutionQueueSystem))]
public sealed partial class CMUEvolutionQueueComponent : Component
{
    /// <summary>
    ///     Xenos waiting for a slot in each tier, in priority order (front evolves first).
    /// </summary>
    [ViewVariables]
    public Dictionary<int, List<CMUEvolutionQueueEntry>> Queues = new();
}

/// <summary>
///     A xeno waiting in a tier queue and the caste it wants to evolve into.
/// </summary>
public record struct CMUEvolutionQueueEntry(EntityUid Xeno, EntProtoId Choice);
