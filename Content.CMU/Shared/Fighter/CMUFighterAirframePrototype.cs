using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Fighter;

/// <summary>Artwork and sockets shared by an airframe's ground, cockpit and flyby representations.</summary>
[Prototype("cmuFighterAirframe")]
public sealed partial class CMUFighterAirframePrototype : IPrototype
{
    public static readonly ProtoId<CMUFighterAirframePrototype> Default = "CMUFighter";

    [IdDataField] public string ID { get; private set; } = default!;
    [DataField] public EntProtoId Hull = "CMUFighterHull";
    [DataField] public EntProtoId Canopy = "CMUFighterCanopy";
    [DataField] public EntProtoId PilotSeat = "CMUFighterPilotSeat";
    [DataField] public EntProtoId ObserverSeat = "CMUFighterObserverSeat";
    [DataField] public EntProtoId Flyby = "CMUFighterFlyby";
    [DataField] public ResPath Sprite = new("/Textures/CMU14/Vehicles/Fighter/jetfighter.rsi");
    [DataField] public string GroundState = "folded";
    [DataField] public string VtolState = "vtolmode";
    [DataField] public string FlightState = "jetfighter";
    [DataField] public string SoundSet = "original";
    [DataField] public float TakeoffRoll = 1.5f;
    [DataField] public float LandingRoll = 1f;
    [DataField] public float LiftPoint = .5f;
    [DataField] public float TouchdownPoint = .6f;
    /// <summary>Exhaust exits in north-facing sprite pixels, with positive Y toward the nose.</summary>
    [DataField] public Vector2[] EngineOffsets = [new(-16, -140), new(16, -140)];
    [DataField] public Vector2[] WheelOffsets = [new(-25, -23), new(25, -23)];
    [DataField] public Color ExhaustColor = Color.FromHex("#80C9FF");
    [DataField] public float ExhaustLength = 1.1f;
    [DataField] public float ExhaustWidth = .12f;
    [DataField] public int ExhaustCells = 2;
    [DataField] public float LiftWash = .55f;
    /// <summary>Rotation of the source art into the existing south-facing ground frame.</summary>
    [DataField] public Angle SpriteRotation;
    [DataField] public bool FoldWings = true;
    [DataField] public string[] ExtendedWingFixtures = ["wings"];
    [DataField] public string[] FoldedWingFixtures = ["foldedWings"];
    /// <summary>Cockpit coordinates relative to the hull center, facing north, at scale 2.</summary>
    [DataField] public Vector2 PilotOffset = new(0, 4.2f);
    [DataField] public Vector2 ObserverOffset = new(0, 2.1f);
    [DataField] public Vector2 GauOffset = new(0, 5.5f);
    [DataField] public Vector2[] PylonOffsets =
    [
        new(-2.5f, -1.5f), new(-4.3f, -2.2f), new(-6.1f, -2.9f),
        new(2.5f, -1.5f), new(4.3f, -2.2f), new(6.1f, -2.9f),
    ];
    /// <summary>Source-pixel apertures, also supplied to the canopy frame shader.</summary>
    [DataField] public Vector2 SpriteSize = new(266, 335);
    [DataField] public Vector2 PilotWindowCenter = new(133, 228.5f);
    [DataField] public Vector2 PilotWindowHalfSize = new(8.5f, 12.5f);
    [DataField] public Vector2 ObserverWindowCenter = new(133, 193.5f);
    [DataField] public Vector2 ObserverWindowHalfSize = new(9.5f, 16.5f);
}

/// <summary>Retains the selected airframe when its parts move between maps and leave each other's PVS.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUFighterAppearanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<CMUFighterAirframePrototype> Airframe = CMUFighterAirframePrototype.Default;
}
