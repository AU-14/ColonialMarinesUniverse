using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// An open jeep's moving bodywork and the clickable parts spawned on it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUJeepComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool HoodOpen;

    [DataField, AutoNetworkedField]
    public bool WindshieldDown;

    [DataField, AutoNetworkedField]
    public bool FuelDoorOpen;

    [DataField, AutoNetworkedField]
    public float EngineIntegrity = 100f;

    [DataField]
    public float EngineMaxIntegrity = 100f;

    /// <summary>
    /// Engine integrity fractions below which it smokes lightly and heavily.
    /// </summary>
    [DataField]
    public float EngineSmokeFraction = 0.5f;

    [DataField]
    public float EngineHeavySmokeFraction = 0.2f;

    [DataField]
    public float EngineRepairAmount = 50f;

    [DataField]
    public TimeSpan EngineRepairDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public ProtoId<ToolQualityPrototype> EngineRepairQuality = "Welding";

    [DataField]
    public ProtoId<ToolQualityPrototype> PartRemoveQuality = "Screwing";

    [DataField]
    public TimeSpan PartRemoveDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Share of each hit on the hull taken by the engine, the fitted windshield and the headlights.
    /// </summary>
    [DataField]
    public float EngineDamageShare = 0.3f;

    [DataField]
    public float WindshieldDamageShare = 0.25f;

    [DataField]
    public float HeadlightDamageShare = 0.15f;

    /// <summary>
    /// Chance per hit of at least <see cref="PunctureMinDamage"/> to hole the hanging jerry can.
    /// </summary>
    [DataField]
    public float JerryCanPunctureChance = 0.2f;

    [DataField]
    public float PunctureMinDamage = 5f;

    /// <summary>
    /// Share of a stray projectile's damage the jeep takes as it passes through to whoever is behind.
    /// </summary>
    [DataField]
    public float PassThroughDamage = 0.5f;

    [DataField, AutoNetworkedField]
    public bool WindshieldDamaged;

    [DataField, AutoNetworkedField]
    public bool HeadlightsBroken;

    [DataField, AutoNetworkedField]
    public bool JerryCanLeaking;

    [DataField]
    public List<CMUVehiclePartData> Parts = new();

    [DataField]
    public EntProtoId PartPrototype = "CMUVehiclePart";

    [DataField, AutoNetworkedField]
    public List<EntityUid> PartEntities = new();
}

[DataDefinition]
public sealed partial class CMUVehiclePartData
{
    /// <summary>
    /// Part id, matching its click_* mask and *_outline states.
    /// </summary>
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Item slot on the vehicle holding the part, when an empty hand can take it.
    /// </summary>
    [DataField]
    public string? Slot;
}

/// <summary>
/// A clickable piece of a vehicle: an invisible mask over the part that routes clicks to it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehiclePartComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;

    [DataField, AutoNetworkedField]
    public string Part = string.Empty;

    [DataField, AutoNetworkedField]
    public string? Slot;
}
