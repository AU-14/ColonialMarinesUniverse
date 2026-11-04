using System.Numerics;
using Content.Shared.Tools;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// An open vehicle's moving bodywork and the clickable parts spawned on it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUOpenVehicleComponent : Component
{
    /// <summary>
    /// The hinged panels standing open, by part id.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> OpenPanels = new();

    [DataField, AutoNetworkedField]
    public bool WindshieldDown;

    [DataField]
    public SoundSpecifier? WindshieldSound;

    [DataField, AutoNetworkedField]
    public float EngineIntegrity = 150f;

    [DataField]
    public float EngineMaxIntegrity = 150f;

    /// <summary>
    /// Share of a head on crash's hull damage that also wears the engine. Other crashes spare it.
    /// </summary>
    [DataField]
    public float EngineCrashShare = 1f;

    /// <summary>
    /// Where the engine sits and the hull around it, in vehicle space (facing south, front is -y).
    /// A hit landing on the engine bay is a direct hit.
    /// </summary>
    [DataField]
    public Box2 EngineBay = new(-0.45f, -1.05f, 0.45f, -0.3f);

    [DataField]
    public Box2 HullBounds = new(-0.45f, -1.05f, 0.45f, 1.05f);

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
    public float EngineRepairAmount = 75f;

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
    /// Fitted moving parts that are not fully fastened, by item slot: <see cref="CMUOpenVehicleSystem.Bolted"/>
    /// or <see cref="CMUOpenVehicleSystem.Loose"/>. A part not listed is screwed down.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, int> Fastening = new();

    /// <summary>
    /// Share of each hit taken by the engine, of a direct hit on the engine bay, and by the fitted
    /// windshield. The lamps take theirs as <see cref="CMUVehicleLampData"/> says.
    /// </summary>
    [DataField]
    public float EngineDamageShare = 0.2f;

    [DataField]
    public float EngineDirectHitShare = 1f;

    /// <summary>
    /// The engine's share of hits taken past its last integrity: what kills it beyond killing it, and
    /// any hit on it once dead. Enough of it and the burning engine goes up at once
    /// (<see cref="CMUVehicleWreckComponent.OverkillLimit"/>). Cleared while the engine has integrity.
    /// </summary>
    [ViewVariables]
    public float EngineOverkill;

    [DataField]
    public float WindshieldDamageShare = 0.25f;

    /// <summary>
    /// The share of a hit worn off the panel on the side of the hull it lands on.
    /// </summary>
    [DataField]
    public float PanelDamageShare = 0.5f;

    /// <summary>
    /// The share of a crash's hull damage worn off the panel on the side it hit.
    /// </summary>
    [DataField]
    public float PanelCrashShare = 1f;

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
    /// The engine gave out and is on fire: flames lick out from under the hood until the jeep goes up
    /// or the fire is put out.
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
    /// Part id, matching its sprite layer and its states: click_* mask, *_outline, and a panel's
    /// *_closed, *_open, *_opening and *_closing.
    /// </summary>
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public CMUVehiclePartKind Kind = CMUVehiclePartKind.Item;

    /// <summary>
    /// Item slot on the vehicle holding the part, when an empty hand can take it.
    /// </summary>
    [DataField]
    public string? Slot;

    /// <summary>
    /// Where the part's click mask sits, in vehicle space (facing south). Reach is measured to the
    /// mask, so a part at the vehicle's far end is spawned there.
    /// </summary>
    [DataField]
    public Vector2 Offset;

    /// <summary>
    /// The directions the vehicle's overlay draws the part in, over the riders.
    /// </summary>
    [DataField]
    public List<Direction> OverRiders = new();

    [DataField]
    public SoundSpecifier? OpenSound;

    [DataField]
    public SoundSpecifier? CloseSound;

    /// <summary>
    /// A panel's parts that can only be reached while it is open or off, like the engine under the hood.
    /// </summary>
    [DataField]
    public List<string> Exposes = new();

    /// <summary>
    /// Kit hung on this panel can't be reached while it stands open, like the spare on a lowered tailgate.
    /// </summary>
    [DataField]
    public bool BlocksKitWhileOpen;

    /// <summary>
    /// Out of its shut position the overlay draws the panel from this layer instead, like the raised hood
    /// standing in front of the windshield.
    /// </summary>
    [DataField]
    public string? RaisedLayer;

    /// <summary>
    /// The way a panel faces, in vehicle space (facing south): hits landing on that side of the hull
    /// wear it. Unset, hits don't wear it.
    /// </summary>
    [DataField]
    public Vector2? WearFacing;

    /// <summary>
    /// A wrecked hull pops the shut panel ajar.
    /// </summary>
    [DataField]
    public bool AjarWhenWrecked;

    /// <summary>
    /// The panels a windshield folds onto: they can't open while it is down, and it can't fold while
    /// they are open.
    /// </summary>
    [DataField]
    public List<string> Interlock = new();

    /// <summary>
    /// The panel the kit hangs on and swings with; it can't be fitted or taken while the panel is off.
    /// </summary>
    [DataField]
    public string? Host;

    /// <summary>
    /// The slot is only filled by clicking this part's own mask, never from a click on the vehicle.
    /// </summary>
    [DataField]
    public bool MaskOnly;
}

public enum CMUVehiclePartKind : byte
{
    /// <summary>
    /// Kit in an item slot, or a fixed fitting.
    /// </summary>
    Item,

    /// <summary>
    /// A hinged door, hood, boot lid, tailgate or fuel door.
    /// </summary>
    Panel,

    /// <summary>
    /// A windshield that folds down.
    /// </summary>
    Windshield,

    Engine,

    Wheels,
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
