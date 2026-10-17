using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// A crate holding a whole vehicle. Unlatched by hand, its walls fall flat and the vehicle stands
/// where the crate was, assembled with its key in, its axle bare: the wheel set rides in the rear hatch.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleCrateComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Vehicle;

    /// <summary>
    /// Colours the vehicle comes painted in, one picked at random. Empty keeps the factory olive.
    /// </summary>
    [DataField]
    public List<Color> Paints = new();

    /// <summary>
    /// The wheel set packed in place of the vehicle's own, if it comes on other tyres.
    /// </summary>
    [DataField]
    public EntProtoId? Wheels;

    /// <summary>
    /// A slot whose fitted item is left out of the crate, if any.
    /// </summary>
    [DataField]
    public string? BareSlot;

    [DataField]
    public EntProtoId Remains = "CMUJeepCrateRemains";

    [DataField]
    public EntProtoId Walls = "CMUJeepCrateWalls";

    [DataField]
    public TimeSpan OpenDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public SoundSpecifier? UnlatchSound = new SoundPathSpecifier("/Audio/Effects/closetopen.ogg");
}

/// <summary>
/// What an opened vehicle crate leaves: its floor under the vehicle, the walls lying flat round it once
/// they are down; or, with <see cref="Falling"/>, the walls falling over the vehicle, hidden once down.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleCrateRemainsComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan OpenedAt;

    [DataField]
    public bool Falling;

    /// <summary>
    /// How long the walls take to fall, the length of the opening animation.
    /// </summary>
    [DataField]
    public TimeSpan Fall = TimeSpan.FromSeconds(0.64);

    [DataField]
    public SoundSpecifier? FallSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");
}

[Serializable, NetSerializable]
public sealed partial class CMUVehicleCrateOpenDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum CMUVehicleCrateVisualLayers : byte
{
    Walls,
}
