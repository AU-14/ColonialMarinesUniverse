using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Dropship;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedDropshipSystem))]
public sealed partial class DropshipDestinationComponent : Component
{
    [DataField]
    public ResPath? Spawn;

    [DataField, AutoNetworkedField]
    public EntityUid? Ship;

    [DataField, AutoNetworkedField]
    public bool AutoRecall;

    [DataField, AutoNetworkedField]
    public int LightSearchRadius = 14;

    [DataField, AutoNetworkedField]
    public EntityUid? ArrivalSoundEntity;

    [DataField("FactionControlling"), AutoNetworkedField]
    public string FactionController = string.Empty;

    [DataField("destinationtype")]
    public DestinationType Destinationtype = DestinationType.Dropship;

    [DataField("Home")]
    public bool Home;

    public enum DestinationType
    {
        Figher,
        Dropship,
        Bigship,
    }
}
