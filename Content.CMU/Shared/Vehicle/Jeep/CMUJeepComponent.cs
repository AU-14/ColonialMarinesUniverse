using System.Numerics;
using Content.Shared.Tools;
using Robust.Shared.Audio;
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
    public bool DriverDoorOpen;

    [DataField, AutoNetworkedField]
    public bool PassengerDoorOpen;

    /// <summary>
    /// The cargo and gun jeeps' fold-down tailgate. Loads and unloads the bed, and the gunner
    /// climbs in over it; the spare and the jerry can hang on it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool TailgateOpen;

    [DataField]
    public SoundSpecifier? HoodOpenSound;

    [DataField]
    public SoundSpecifier? HoodCloseSound;

    [DataField]
    public SoundSpecifier? WindshieldSound;

    [DataField]
    public SoundSpecifier? DoorOpenSound;

    [DataField]
    public SoundSpecifier? DoorCloseSound;

    [DataField]
    public SoundSpecifier? TailgateOpenSound;

    [DataField]
    public SoundSpecifier? TailgateCloseSound;

    [DataField, AutoNetworkedField]
    public float EngineIntegrity = 50f;

    [DataField]
    public float EngineMaxIntegrity = 50f;

    /// <summary>
    /// Engine integrity fractions below which it smokes lightly and heavily.
    /// </summary>
    [DataField]
    public float EngineSmokeFraction = 0.5f;

    [DataField]
    public float EngineHeavySmokeFraction = 0.2f;

    /// <summary>
    /// Integrity each welding pass restores to the engine: two passes from dead to whole.
    /// </summary>
    [DataField]
    public float EngineRepairAmount = 25f;

    [DataField]
    public TimeSpan EngineRepairDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public ProtoId<ToolQualityPrototype> EngineRepairQuality = "Welding";

    /// <summary>
    /// Integrity each welding pass restores to the fitted windshield or a lamp.
    /// </summary>
    [DataField]
    public float PartRepairAmount = 10f;

    [DataField]
    public TimeSpan PartRepairDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public ProtoId<ToolQualityPrototype> PartRepairQuality = "Welding";

    [DataField]
    public ProtoId<ToolQualityPrototype> PartRemoveQuality = "Screwing";

    [DataField]
    public TimeSpan PartRemoveDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The moving parts (windshield, hood, doors, tailgate) come off in steps: their screws out
    /// (<see cref="PartRemoveQuality"/>), their bolts out with this tool, then lifted off by hand.
    /// One goes back on the other way round: hung on by hand, bolted, then screwed down.
    /// </summary>
    [DataField]
    public ProtoId<ToolQualityPrototype> PanelBoltQuality = "Anchoring";

    /// <summary>
    /// Fitted moving parts that are not fully fastened, by item slot: <see cref="CMUJeepSystem.Bolted"/>
    /// or <see cref="CMUJeepSystem.Loose"/>. A part not listed is screwed down.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, int> Fastening = new();

    /// <summary>
    /// Share of each hit on the hull taken by the engine and the fitted windshield. The lamps take
    /// theirs as <see cref="CMUJeepLampData"/> says.
    /// </summary>
    [DataField]
    public float EngineDamageShare = 0.3f;

    [DataField]
    public float WindshieldDamageShare = 0.25f;

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
    public bool JerryCanLeaking;

    /// <summary>
    /// The hull gave out and the engine is on fire: flames lick out from under the hood until the jeep
    /// goes up.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Burning;

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

    /// <summary>
    /// Where the part's click mask sits, in vehicle space (facing south). Reach is measured to the
    /// mask, so a part at the jeep's far end is spawned there.
    /// </summary>
    [DataField]
    public Vector2 Offset;
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
