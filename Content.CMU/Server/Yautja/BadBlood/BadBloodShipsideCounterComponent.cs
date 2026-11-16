namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-514: the shipside counter to the Vessel Strike. A living Bad Blood at the console the strike was fired from
/// cancels the running strike after a do-after; gate, signature and uplink are kept. Lives on the rule entity.
/// docs/GraphAudit-BadBlood-Objectives.md §5.3 (COUNTER 2). Read by <see cref="BadBloodStrikeSabotageSystem"/>.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodShipsideCounterComponent : Component
{
    /// <summary>
    /// How long a Bad Blood works the strike console to cancel the strike.
    /// </summary>
    [DataField]
    public TimeSpan SabotageDoAfter = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Warn GOVFOR through ARES as soon as the sabotage starts, so the ship crew can stop it.
    /// </summary>
    [DataField]
    public bool AnnounceOnStart = true;
}
