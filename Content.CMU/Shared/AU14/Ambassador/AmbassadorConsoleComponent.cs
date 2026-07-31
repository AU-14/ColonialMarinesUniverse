using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.AU14.Ambassador;

[RegisterComponent, NetworkedComponent]
public sealed partial class AmbassadorConsoleComponent : Component
{
    /// <summary>
    /// The name of the faction operating this console (e.g., "UPP", "UA", "CLF").
    /// Used in announcements to identify who activated embargo/shipment/etc.
    /// </summary>
    [DataField("factionName")]
    public string FactionName = "Unknown Faction";

    /// <summary>
    /// Internal bank balance.
    /// </summary>
    [DataField("budget")]
    public float Budget = 0f;

    /// <summary>
    /// How much money is added each replenish tick.
    /// </summary>
    [DataField("replenishAmount")]
    public float ReplenishAmount = 10f;

    /// <summary>
    /// How often (in seconds) budget replenishes.
    /// </summary>
    [DataField("replenishInterval")]
    public float ReplenishInterval = 60f;

    public float ReplenishTimer = 0f;

    // --- Embargo ---
    [DataField("embargoActive")]
    public bool EmbargoActive = false;

    /// <summary>
    /// Multiplier applied to submission point rewards when embargo is active (0.8 = 20% reduction).
    /// </summary>
    [DataField("embargoMultiplier")]
    public float EmbargoMultiplier = 0.8f;

    /// <summary>
    /// Cost per minute while embargo is active.
    /// </summary>
    [DataField("embargoCostPerMinute")]
    public float EmbargoCostPerMinute = 30f;

    public float EmbargoTimer = 0f;

    // --- Trade Pact (inverse of embargo) ---
    [DataField("tradePactActive")]
    public bool TradePactActive = false;

    /// <summary>
    /// Multiplier applied to submission point rewards when trade pact is active (1.2 = 20% increase).
    /// </summary>
    [DataField("tradePactMultiplier")]
    public float TradePactMultiplier = 1.2f;

    /// <summary>
    /// Cost per minute while trade pact is active.
    /// </summary>
    [DataField("tradePactCostPerMinute")]
    public float TradePactCostPerMinute = 30f;

    public float TradePactTimer = 0f;

    // --- Comms Jam ---
    [DataField("commsJamActive")]
    public bool CommsJamActive = false;

    /// <summary>
    /// Cost per minute while comms jam is active.
    /// </summary>
    [DataField("commsJamCostPerMinute")]
    public float CommsJamCostPerMinute = 400f;

    public float CommsJamTimer = 0f;

    // --- Signal Boost / Signal Jam ---
    [DataField("signalBoostActive")]
    public bool SignalBoostActive = false;

    [DataField("signalJamActive")]
    public bool SignalJamActive = false;

    /// <summary>
    /// Multiplier applied to third party spawn interval when signal boost is active.
    /// </summary>
    [DataField("signalBoostMultiplier")]
    public float SignalBoostMultiplier = 0.5f;

    /// <summary>
    /// Multiplier applied to third party spawn interval when signal jam is active.
    /// </summary>
    [DataField("signalJamMultiplier")]
    public float SignalJamMultiplier = 2.0f;

    /// <summary>
    /// Cost per minute while signal boost is active.
    /// </summary>
    [DataField("signalBoostCostPerMinute")]
    public float SignalBoostCostPerMinute = 50f;

    public float SignalBoostTimer = 0f;

    /// <summary>
    /// Cost per minute while signal jam is active.
    /// </summary>
    [DataField("signalJamCostPerMinute")]
    public float SignalJamCostPerMinute = 50f;

    public float SignalJamTimer = 0f;

    /// <summary>
    /// Cost to scan the radar (one-time).
    /// </summary>
    [DataField("radarScanCost")]
    public float RadarScanCost = 25f;

    /// <summary>
    /// Cached radar scan results. Blank until the user pays to scan.
    /// </summary>
    public List<string> LastRadarScanResults = new();

    // --- Broadcast ---
    /// <summary>
    /// Cost to send a broadcast.
    /// </summary>
    [DataField("broadcastCost")]
    public float BroadcastCost = 50f;

    // --- Callable Third Parties ---
    /// <summary>
    /// Map of third party prototype ID to cost.
    /// </summary>
    [DataField("callableParties")]
    public Dictionary<string, float> CallableParties = new();

    /// <summary>
    /// Set of third party IDs that have already been called and cannot be called again.
    /// </summary>
    public HashSet<string> CalledParties = new();
}
