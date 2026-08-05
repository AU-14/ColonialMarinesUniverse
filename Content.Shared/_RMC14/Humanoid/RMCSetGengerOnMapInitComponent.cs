using Content.Shared.Whitelist;
using Robust.Shared.Enums;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Humanoid;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCSetGenderOnMapInitComponent : Component
{
    [DataField, AutoNetworkedField]
    public Gender Gender = Gender.Epicene;
}
