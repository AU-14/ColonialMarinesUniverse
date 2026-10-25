using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Vehicle;

/// <summary>
/// A kind of tyre a vehicle's wheel set can have, and how it takes to the ground under it.
/// </summary>
[Prototype("cmuVehicleTyre")]
public sealed partial class CMUVehicleTyrePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Top speed and acceleration on paved ground: floors, roads, concrete, anything a shovel can't dig.
    /// </summary>
    [DataField]
    public float PavedSpeed = 1f;

    /// <summary>
    /// Top speed and acceleration on rough ground: dirt, sand, snow, anything a shovel can dig.
    /// </summary>
    [DataField]
    public float RoughSpeed = 1f;

    /// <summary>
    /// How hard water drags the vehicle back: 1 is the water's own drag, more slows it faster.
    /// </summary>
    [DataField]
    public float WaterDrag = 1f;

    /// <summary>
    /// Put before the wheel, spare and wheel set item states for this tyre's art.
    /// </summary>
    [DataField]
    public string StatePrefix = string.Empty;

    /// <summary>
    /// What examining the wheel set, or a vehicle on it, says of these tyres.
    /// </summary>
    [DataField(required: true)]
    public LocId Examine;
}

/// <summary>
/// The tyres on a wheel set.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleTyreComponent : Component
{
    /// <summary>
    /// The kind of tyre.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<CMUVehicleTyrePrototype> Tyre;
}

/// <summary>
/// The tyres of the wheel set fitted to a vehicle, kept from its wheel slots.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUVehicleTyreSystem))]
public sealed partial class CMUVehicleTyresComponent : Component
{
    /// <summary>
    /// The fitted wheel set's tyres, or null with none on.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<CMUVehicleTyrePrototype>? Tyre;

    /// <summary>
    /// The tick <see cref="Rough"/> was read for.
    /// </summary>
    [ViewVariables]
    public GameTick GroundTick;

    /// <summary>
    /// Whether the ground under the vehicle was rough at <see cref="GroundTick"/>.
    /// </summary>
    [ViewVariables]
    public bool Rough;
}
