using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Dropship;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedDropshipSystem))]
public sealed partial class DropshipDestinationComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Ship;

    [DataField, AutoNetworkedField]
    public bool AutoRecall;

    [DataField, AutoNetworkedField]
    public bool CanBePrimary = true;

    [DataField, AutoNetworkedField]
    public int LightSearchRadius = 14;

    [DataField, AutoNetworkedField]
    public EntityUid? ArrivalSoundEntity;

    [DataField("FactionControlling", required: false), AutoNetworkedField]
    public string FactionController = String.Empty;


    [DataField("destinationtype")]
    public  DestinationType Destinationtype = DestinationType.Dropship;

    [DataField("Home")]
    public bool Home = false;
    public enum DestinationType
    {
        Figher,
        Dropship,
        Bigship
    }
}
