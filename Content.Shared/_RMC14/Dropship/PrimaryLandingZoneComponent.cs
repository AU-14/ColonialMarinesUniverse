using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Dropship;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedDropshipSystem))]
public sealed partial class PrimaryLandingZoneComponent : Component
{
    [DataField("faction"), AutoNetworkedField]
    public string? Faction;
}
