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
    public float GripMax = 150;

    [DataField]
    public float GripTightThreshold = 105;

    [DataField]
    public float GripStart = 68;

    [DataField]
    public float GripRegenPerMinute = 20;

    [DataField]
    public float GripCoopRegenPerMinute = 30;

    [DataField]
    public float GripDisableBelow = 22;

    [DataField]
    public float SootheThreshold = 105;

    [DataField]
    public float PunishCost = 15;

    // Damage per punish scales up with the grip pool, so a deep grip can
    // press a host into crit over a few presses
    [DataField]
    public float PunishGripDamageScale = 0.5f;

    [DataField]
    public float SpeakCost = 5;

    // Starving riders lose the throat: below this much grip, spoken mimicry
    // starts clicking over into RiderCant. The last gasp before full disable
    [DataField]
    public float MaskSlipGripBelow = 30;

    [DataField]
    public float MaskSlipChance = 0.2f;

    [DataField]
    public float SurgeCost = 10;

    [DataField]
    public float CoaxCost = 12;

    [DataField]
    public float SustainCost = 20;

    [DataField]
    public float MuteCost = 10;

    [DataField]
    public float SeizeCost = 30;

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
    public TimeSpan SeizeDuration = TimeSpan.FromSeconds(90);

    /// <summary>
    /// How long a mute keeps the host's voice clamped shut.
    /// </summary>
    [DataField]
    public TimeSpan MuteDuration = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long pressing against a blocking door takes to squeeze through.
    /// Never applies to plain closed airlocks: See RiderSystem.OnSqueezeTouch.
    /// </summary>
    [DataField]
    public TimeSpan SqueezeDuration = TimeSpan.FromSeconds(2);

    /// <summary>
    /// A fresh offer may not be spammed at the same host for this long.
    /// </summary>
    [DataField]
    public TimeSpan OfferCooldown = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a revived corpse's original owner has to answer the return
    /// prompt before the body is raffled to ghosts.
    /// </summary>
    [DataField]
    public TimeSpan HostReturnWindow = TimeSpan.FromSeconds(90);

    [DataField]
    public float ResistDrainSeconds = 150;

    /// <summary>
    /// How hard resisting strangles regen at an empty tank, scaling down to
    /// nothing at a full one. 0.8 leaves a starving rider a fifth of their
    /// refill, and a full one loses nothing.
    /// </summary>
    [DataField]
    public float GripResistRegenSuppression = 0.8f;

    [DataField]
    public TimeSpan LatchDuration = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How far the phantom may drift from the host while manifested.
    /// </summary>
    [DataField]
    public float ManifestLeash = 8;

    public EntityUid? Host;
    public float Grip;

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
    /// While CurTime is below this the host's own chat input is swallowed.
    /// </summary>
    public TimeSpan MutedUntil;

    /// <summary>
    /// The blocking door currently being squeezed through, if any.
    /// </summary>
    public EntityUid? SqueezingDoor;
    public TimeSpan SqueezeDoneAt;

    /// <summary>
    /// Host with an open latch offer; cleared on answer or expiry.
    /// </summary>
    public EntityUid? OfferedTo;
    public TimeSpan OfferExpiresAt;

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
    public EntityUid? MuteAction;
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
