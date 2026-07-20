using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;

namespace Content.Shared.Chemistry.EntitySystems;

public sealed partial class SolutionTransferSystem
{
    /// <summary>
    /// Compatibility overload for callers that pass transfer data as individual arguments.
    /// </summary>
    public FixedPoint2 Transfer(
        EntityUid? user,
        EntityUid sourceEntity,
        Entity<SolutionComponent> source,
        EntityUid targetEntity,
        Entity<SolutionComponent> target,
        FixedPoint2 amount)
    {
        return Transfer(new SolutionTransferData(user, sourceEntity, source, targetEntity, target, amount));
    }
}
