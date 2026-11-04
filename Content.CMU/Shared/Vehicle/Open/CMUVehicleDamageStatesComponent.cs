using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Damage drawn on an open vehicle's parts by damage type, in levels: bullet holes in the doors,
/// a scorched hood. Each stage is a sprite layer over its part (on the vehicle and on its overlay),
/// showing the highest of its levels the part has reached in that type of damage: how much of its
/// integrity it has lost, times that type's share of the damage the vehicle has taken.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleDamageStatesComponent : Component
{
    [DataField]
    public List<CMUVehicleDamageStage> Stages = new();

    /// <summary>
    /// Damage the vehicle has taken by type, after its resistances. Only the shares between types
    /// matter: how much each part shows comes from its integrity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> Taken = new();
}

[DataDefinition]
public sealed partial class CMUVehicleDamageStage
{
    /// <summary>
    /// The stage's layer key on the vehicle's sprite and its overlay's. Its states are
    /// <c>layer_level</c> and, over the riders, <c>layer_overlay_level</c>.
    /// </summary>
    [DataField(required: true)]
    public string Layer = string.Empty;

    /// <summary>
    /// The part's layer on the vehicle's sprite: the stage only shows while it does.
    /// </summary>
    [DataField(required: true)]
    public string Part = string.Empty;

    /// <summary>
    /// The part's layer on the overlay, if it has another key there (the body is <c>body</c>).
    /// </summary>
    [DataField]
    public string? OverlayPart;

    /// <summary>
    /// The stage was drawn on the part at rest: it only shows while the part's layer shows a state
    /// starting with one of these (a shut panel, a raised windshield). Any state, when empty.
    /// </summary>
    [DataField]
    public List<string> RestStates = new();

    /// <summary>
    /// What wears the part: the hull, the engine, or the item in <see cref="Slot"/>.
    /// </summary>
    [DataField]
    public CMUVehicleDamageSource Source = CMUVehicleDamageSource.Hull;

    [DataField]
    public string? Slot;

    /// <summary>
    /// The damage type (or group) the stage shows.
    /// </summary>
    [DataField(required: true)]
    public string DamageType = string.Empty;

    /// <summary>
    /// The levels it is drawn at, in percent of the part's integrity lost to that type, lowest first.
    /// </summary>
    [DataField(required: true)]
    public List<int> Levels = new();
}

public enum CMUVehicleDamageSource : byte
{
    /// <summary>
    /// The vehicle's hull integrity: the body, panels and stowed kit.
    /// </summary>
    Hull,

    /// <summary>
    /// The open vehicle's engine integrity.
    /// </summary>
    Engine,

    /// <summary>
    /// The <see cref="CMUVehiclePartIntegrityComponent"/> of the item in the stage's slot: a lamp or
    /// the windshield.
    /// </summary>
    Slot,
}
