using Robust.Shared.Map;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// ARCH-02 bookkeeping the objective ladder keeps on the Joe, beside <c>EWJoeComponent</c> (which other work
/// owns): the player's directive reroll, the refill owed after a stale directive was withdrawn, and the
/// planet the current offer was made on. Ensured whenever the ladder offers. Server bookkeeping.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeDirectiveStateComponent : Component
{
    /// <summary>The level the last reroll was spent on; -1 when none was.</summary>
    [DataField]
    public int RerollLevel = -1;

    [DataField]
    public TimeSpan LastReroll;

    /// <summary>One reroll per level; a level that lasts this long after a reroll earns another.</summary>
    [DataField]
    public TimeSpan RerollCooldown = TimeSpan.FromMinutes(10);

    /// <summary>A stale directive was withdrawn: the offer is short and is topped up at the next retry.</summary>
    [DataField]
    public bool PendingRefill;

    /// <summary>
    /// Until the withdrawn slot was offered again, the shorter offer does not lower the credit needed to
    /// advance (otherwise withdrawing two of three directives would advance the level on one completion).
    /// </summary>
    [DataField]
    public TimeSpan HoldRequirementUntil;

    /// <summary>
    /// A map of the planet the current offer was made on. Directives are only judged stale while the Joe
    /// stands on that planet, so a trip to the ship never withdraws planet-side work.
    /// </summary>
    [ViewVariables]
    public MapId? OfferMap;
}
