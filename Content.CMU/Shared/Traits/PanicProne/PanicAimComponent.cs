using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Traits.PanicProne;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PanicAimComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [DataField, AutoNetworkedField]
    public float SpreadMultiplier = 1.0f;
}
