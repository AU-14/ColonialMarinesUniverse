using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;

namespace Content.Shared.Chemistry.EntitySystems;

public sealed partial class SolutionTransferSystem
{
    private static IEnumerable<FixedPoint2> RMCGetTransferAmounts(SolutionTransferComponent component)
    {
        return component.TransferAmounts ?? DefaultTransferAmounts;
    }
}
