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
    public float GripStart = 45;

    [DataField]
    public float GripRegenPerMinute = 2;

    [DataField]
    public float GripCoopRegenPerMinute = 5;

    [DataField]
    public float GripDisableBelow = 15;

    [DataField]
    public float SootheThreshold = 70;

    [DataField]
    public float PunishCost = 15;

    [DataField]
    public float SpeakCost = 20;

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

    public EntityUid? Host;
    public float Grip;

    /// <summary>
    /// Partial-tick accumulator; regen is per-minute, updates are per-frame.
    /// </summary>
    public float GripAccumulator;

    public bool SeizeActive;
    public EntityUid? SeizeProxy;
    public TimeSpan SeizeEndsAt;
    public bool Soothing;

    public EntityUid? LatchTarget;

    public EntityUid? LatchAction;
    public EntityUid? VoiceAction;
    public EntityUid? PunishAction;
    public EntityUid? SeizeAction;
    public EntityUid? ExitAction;
    public EntityUid? SurgeAction;

    // Tells clock and residue trail, phase 2
    public TimeSpan NextTellAt;
    public TimeSpan NextCrawlResidueAt;
    public TimeSpan NextShedAt;
    public TimeSpan NextSoothePainAt;

    // Round-end summary bookkeeping
    public int HostsRidden;
    public TimeSpan TotalRideTime;

    // Objective flavor, rolled at selection (§6)
    public RiderFlavor Flavor;
    public EntProtoId? PuppeteerItem;

    // Hosts that count toward Leapfrog: only rides where the host was awake at
    // some point, or latched willingly. AFK sleepers steer nothing (§7)
    public int CreditedHosts;
    public bool RideCredited;

    // Choir ping cadence (§6.5)
    public TimeSpan NextChoirAt;
}
