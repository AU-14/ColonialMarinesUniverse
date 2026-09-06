using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Round.Antags.CorporateAgent;

/// <summary>
/// A corporate infiltration antag (Laselle spy, Weyland-Yutani agent). Their goal is to hold
/// a number of active fetch-objective items and transmit them with a data link beacon.
/// </summary>
[RegisterComponent]
public sealed partial class CorporateAgentComponent : Component
{
    /// <summary>
    /// Corporation the agent works for, used in transmissions.
    /// </summary>
    [DataField]
    public string Corporation = "Weyland-Yutani";

    /// <summary>
    /// Letterhead paper the exfiltration confirmation prints on, matching the corporation.
    /// </summary>
    [DataField]
    public EntProtoId PaperPrototype = "CMUPaperWEYLAND";

    /// <summary>
    /// Fetch-objective items that must be held before the beacon can transmit.
    /// </summary>
    [DataField]
    public int RequiredItems = 2;

    /// <summary>
    /// Weyland-Yutani counterintelligence intercepts the transmission and alerts the CMB.
    /// </summary>
    [DataField]
    public bool WyCounterIntel;

    /// <summary>
    /// Set once the data link has transmitted successfully.
    /// </summary>
    public bool Completed;
}
