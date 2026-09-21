using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Dropship;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedDropshipSystem))]
public sealed partial class DropshipDestinationComponent : Component
{
    [DataField]
    public EntityUid? Ship;

    [DataField]
    public bool AutoRecall;

    [DataField]
    public bool CanBePrimary = true;

    [DataField]
    public int LightSearchRadius = 14;

    [DataField]
    public EntityUid? ArrivalSoundEntity;

    [DataField("FactionControlling", required: false)]
    public string FactionController = String.Empty;


    [DataField("destinationtype")]
    public  DestinationType Destinationtype = DestinationType.Dropship;

    [DataField("Home")]
    public bool Home = false;

    // CMU14: Large multi-deck hulls can need a different center on the same pad.
    // This is expressed in the destination grid's coordinates; ordinary ships
    // keep using the marker itself.
    [DataField]
    public Vector2 MultiDeckOffset;

    public enum DestinationType
    {
        Figher,
        Dropship,
        Bigship
    }
}
