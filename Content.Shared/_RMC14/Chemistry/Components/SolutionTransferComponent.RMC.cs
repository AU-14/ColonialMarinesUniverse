using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Chemistry.Components;

public sealed partial class SolutionTransferComponent
{
    [DataField, AutoNetworkedField]
    public FixedPoint2[]? TransferAmounts;
}
