using Content.Shared.Chemistry.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.FootPrint;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FootPrintComponent : Component
{
    [AutoNetworkedField]
    public EntityUid PrintOwner;

    [DataField]
    public string SolutionName = "step";

    [ViewVariables(VVAccess.ReadOnly)]
    public Entity<SolutionComponent>? Solution;

    /// <summary>
    /// Set when the footprint's alpha has already been reduced because a weed exists on its tile.
    /// Prevents stacking the dim effect each time a weed spreads onto the same tile.
    /// </summary>
    [ViewVariables]
    public bool DimmedByWeeds;
}
