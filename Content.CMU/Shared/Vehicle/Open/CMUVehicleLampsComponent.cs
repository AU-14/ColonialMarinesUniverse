using System.Numerics;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// A jeep's lamps, each fitted on its own in an item slot this component adds, with a clickable part
/// over it: the headlights, the turn signals on the front fenders, and the tail lights, which carry
/// the brake lights and the rear turn signals. A screwdriver takes one off; one is put back by
/// clicking its place with it in hand.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleLampsComponent : Component
{
    [DataField(required: true)]
    public List<CMUVehicleLampData> Lamps = new();

    /// <summary>
    /// Whether the jeep comes with its lamps in; a kit chassis comes without.
    /// </summary>
    [DataField]
    public bool Fitted = true;

    /// <summary>
    /// Lamps too damaged to light, by part id.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> Broken = new();

    /// <summary>
    /// The slowest crash, as a share of top speed, that can break a lamp.
    /// </summary>
    [DataField]
    public float CrashMinSpeed = 0.25f;

    /// <summary>
    /// The crash speed, as a share of top speed, that surely breaks a lamp right where it hits.
    /// </summary>
    [DataField]
    public float CrashSureSpeed = 0.75f;

    /// <summary>
    /// How far from where a crash hits, in tiles, it can still break a lamp; the nearer, the likelier.
    /// </summary>
    [DataField]
    public float CrashReach = 0.9f;

    /// <summary>
    /// How near a lamp, in tiles, a shot has to land on the hull to shatter it outright.
    /// </summary>
    [DataField]
    public float ShotReach = 0.3f;
}

[DataDefinition]
public sealed partial class CMUVehicleLampData
{
    /// <summary>
    /// Part id, matching its sprite layer, click_* mask and *_outline states: headlight_left,
    /// turn_signal_right, taillight_left... Left is the driver's side.
    /// </summary>
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// The lamp's slot, added to the jeep as <see cref="SlotId"/>.
    /// </summary>
    [DataField(required: true)]
    public ItemSlot Slot = new();

    /// <summary>
    /// Where the lamp's click mask sits, in vehicle space facing south, so reach is measured to it.
    /// </summary>
    [DataField]
    public Vector2 Offset;

    /// <summary>
    /// Chance per hit on the jeep that this lamp takes some of it, and the share it takes.
    /// </summary>
    [DataField]
    public float HitChance = 0.3f;

    [DataField]
    public float DamageShare = 0.5f;

    /// <summary>
    /// The directions the vehicle's overlay draws the lamp in, over the riders.
    /// </summary>
    [DataField]
    public List<Direction> OverRiders = new();

    [DataField]
    public CMUVehicleLampRole Role;

    /// <summary>
    /// The side it blinks with as a turn signal; a headlight's side also names its beams.
    /// </summary>
    [DataField]
    public CMUVehicleLampSide Side;

    /// <summary>
    /// The overlay layer shown while the lights are on.
    /// </summary>
    [DataField]
    public string? LitLayer;

    /// <summary>
    /// The overlay layer shown while braking.
    /// </summary>
    [DataField]
    public string? BrakeLayer;

    /// <summary>
    /// The overlay layer blinking while its side's turn signal is on.
    /// </summary>
    [DataField]
    public string? SignalLayer;

    /// <summary>
    /// A headlight's lens in each direction's art, in pixels from the vehicle origin (x right, y up):
    /// where its beams start. Without them, the vehicle's driver or passenger anchors by side.
    /// </summary>
    [DataField]
    public Dictionary<Direction, Vector2> BeamAnchors = new();

    public string SlotId => $"vehicle-{Id.Replace('_', '-')}";
}

public enum CMUVehicleLampRole : byte
{
    /// <summary>
    /// Throws the beams the headlight switch picks.
    /// </summary>
    Headlight,

    /// <summary>
    /// Glows red with the lights on, brighter braking.
    /// </summary>
    TailLight,

    TurnSignal,

    /// <summary>
    /// Lit with the lights on and nothing more.
    /// </summary>
    Marker,

    /// <summary>
    /// A mount for the searchlight, lit and aimed by hand (<see cref="CMUVehicleSearchlightComponent"/>).
    /// </summary>
    Searchlight,
}

public enum CMUVehicleLampSide : byte
{
    Left,
    Right,
}
