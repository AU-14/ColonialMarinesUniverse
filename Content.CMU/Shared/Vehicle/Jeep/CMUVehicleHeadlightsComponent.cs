using System.Numerics;
using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Jeep;

[Serializable, NetSerializable]
public enum CMUHeadlightMode : byte
{
    Off,
    Low,
    High,
}

/// <summary>
/// A vehicle's headlight switch: off, low beam or high beam. Each headlight has a child entity per
/// beam, with its own cone-masked light and the faint haze drawn on the road, starting at that lamp
/// and lit only through fitted, intact headlights. The switch also drives RMC's spotlight flag,
/// which lights the tail and marker lamps.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleHeadlightsComponent : Component
{
    [DataField, AutoNetworkedField]
    public CMUHeadlightMode Mode;

    [DataField]
    public EntProtoId LowBeamPrototype = "CMUJeepLowBeam";

    [DataField]
    public EntProtoId HighBeamPrototype = "CMUJeepHighBeam";

    [DataField, AutoNetworkedField]
    public List<EntityUid> Beams = new();

    /// <summary>
    /// Where each direction's art draws the driver's headlight lens, in pixels from the vehicle
    /// origin (x right, y up): where its beams start. From the generator's seat_offsets.json.
    /// </summary>
    [DataField]
    public Dictionary<Direction, Vector2> DriverLampAnchors = new();

    [DataField]
    public Dictionary<Direction, Vector2> PassengerLampAnchors = new();

    /// <summary>
    /// Tiles from the headlights to the middle of the haze sprite, whose cone starts at its top edge.
    /// </summary>
    [DataField]
    public float BeamHalfLength = 5f;

    [DataField]
    public SoundSpecifier? SwitchSound;
}

/// <summary>
/// One headlight's low or high beam.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleHeadlightBeamComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;

    [DataField, AutoNetworkedField]
    public CMUHeadlightMode Mode;

    /// <summary>
    /// The driver's headlight's beam, else the passenger's.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Driver;

    /// <summary>
    /// How strongly the haze shows where its own light lands.
    /// </summary>
    [DataField]
    public float Strength = 0.55f;
}

public sealed partial class CMUVehicleHeadlightsActionEvent : InstantActionEvent;
