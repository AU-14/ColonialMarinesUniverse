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

    [DataField]
    public Entity<SolutionComponent>? Solution;
}
