using Content.Shared._RMC14.Language.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Round.Antags.Rider;

public enum RiderFlavor : byte
{
    Hitchhiker,
    Leapfrog,
    Puppeteer,
}

/// <summary>
/// The hatchling: the antag entity itself. One player rides colonists from inside
/// this body. All tuning lives here as data fields.
/// </summary>
[RegisterComponent]
public sealed partial class RiderComponent : Component
{
    [DataField]
    public float GripMax = 100;

    [DataField]
    public float GripTightThreshold = 70;

    [DataField]
    public float GripStart = 45;

    [DataField]
    public float GripRegenPerMinute = 10;

    [DataField]
    public float GripCoopRegenPerMinute = 18;

    [DataField]
    public float GripDisableBelow = 15;

    [DataField]
    public float SootheThreshold = 70;

    [DataField]
    public float PunishCost = 15;

    // Damage per punish scales up with the grip pool, so a deep grip can
    // press a host into crit over a few presses
    [DataField]
    public float PunishGripDamageScale = 0.5f;

    [DataField]
    public float SpeakCost = 5;

    [DataField]
    public float SurgeCost = 10;

    [DataField]
    public float CoaxCost = 12;

    [DataField]
    public float SustainCost = 20;

    [DataField]
    public float SeizeCost = 45;

    /// <summary>
    /// Seize requires grip at or above this; the cost alone can never drop
    /// grip below the floor. Without the floor the signature play self-destructs.
    /// </summary>
    [DataField]
    public float SeizeGate = 50;

    [DataField]
    public float SeizeFloor = 20;

    /// <summary>
    /// How far the host's spectator shape may drift from the body during a burst.
    /// </summary>
    [DataField]
    public float SeizeProxyLeash = 12.5f;

    /// <summary>
    /// The soothe's chemical quiet: a mild additive pain profile kept alive
    /// while grip is high or the host is willing. Refreshed before expiry.
    /// </summary>
    [DataField]
    public TimeSpan SoothePainRefresh = TimeSpan.FromSeconds(30);

    [DataField]
    public float SoothePainAccumulation = 0.25f;

    [DataField]
    public int SoothePainTier = 1;

    [DataField]
    public float SoothePainDecayBonus = 0.25f;

    [DataField]
    public TimeSpan SeizeDuration = TimeSpan.FromSeconds(25);

    [DataField]
    public float ResistDrainPerSecond = 1;

    [DataField]
    public TimeSpan LatchDuration = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How far the phantom may drift from the host while manifested.
    /// </summary>
    [DataField]
    public float ManifestLeash = 8;

    public EntityUid? Host;
    public float Grip;

    // Languages borrowed from the current host; returned when the ride ends
    public List<ProtoId<LanguagePrototype>> InheritedLanguages = new();

    /// <summary>
    /// Partial-tick accumulator; regen is per-minute, updates are per-frame.
    /// </summary>
    public float GripAccumulator;

    public bool SeizeActive;
    public EntityUid? SeizeProxy;
    public EntityUid? SeizeExitAction;
    public TimeSpan SeizeEndsAt;
    public bool Soothing;

    /// <summary>
    /// The phantom projected into the host's mind's eye; null while the rider
    /// is wholly inside.
    /// </summary>
    public EntityUid? Manifest;
    public EntityUid? WithdrawAction;

    /// <summary>
    /// Admin/ghost tell that rides the host while latched.
    /// </summary>
    public EntityUid? LatchMarker;

    public EntityUid? LatchAction;
    public EntityUid? PunishAction;
    public EntityUid? SeizeAction;
    public EntityUid? ExitAction;
    public EntityUid? SurgeAction;
    public EntityUid? CoaxAction;
    public EntityUid? SustainAction;
    public EntityUid? ManifestAction;

    public TimeSpan NextTellAt;
    public TimeSpan NextCrawlResidueAt;
    public TimeSpan NextShedAt;
    public TimeSpan NextSoothePainAt;
    public TimeSpan NextGripPushAt;

    // Round-end summary bookkeeping
    public int HostsRidden;
    public TimeSpan TotalRideTime;

    public RiderFlavor Flavor;
    public EntProtoId? PuppeteerItem;

    // Hosts that count toward Leapfrog: only rides where the host was awake at
    // some point, or latched willingly. AFK sleepers steer nothing.
    public int CreditedHosts;
    public bool RideCredited;

    // Rider pairs, choir ping cadence
    public TimeSpan NextChoirAt;
}

/// <summary>
/// Marks the rider's manifested phantom. Its speech has no voice of its own;
/// it routes through the rider's whisper channel to the host.
/// </summary>
[RegisterComponent]
public sealed partial class RiderManifestComponent : Component
{
    public EntityUid Rider;
}

/// <summary>
/// Marks the seize spectator body. It stays mute for the ride's duration;
/// the seized player must not speak while bodyjacked.
/// </summary>
[RegisterComponent]
public sealed partial class RiderSeizeProxyComponent : Component;
