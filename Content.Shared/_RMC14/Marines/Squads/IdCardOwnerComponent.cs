using Content.Shared._RMC14.Access;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Marines.Squads;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SquadSystem), typeof(IdCardSystem))]
public sealed partial class IdCardOwnerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Id;
}
