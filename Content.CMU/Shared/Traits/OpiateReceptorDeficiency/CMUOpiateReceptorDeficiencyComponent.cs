using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Traits.OpiateReceptorDeficiency;

/// <summary>
///     Basic painkillers (up to tramadol strength) work much less well on this person.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUOpiateReceptorDeficiencyComponent : Component
{
    /// <summary>
    ///     Painkillers that suppress this many pain tiers or fewer count as basic.
    /// </summary>
    [DataField]
    public int BasicTierLimit = 2;

    /// <summary>
    ///     How much of a basic painkiller's suppression and recovery bonus is left.
    /// </summary>
    [DataField]
    public float PotencyMultiplier = 0.4f;

    /// <summary>
    ///     How many fewer pain tiers a basic painkiller suppresses.
    /// </summary>
    [DataField]
    public int TierPenalty = 1;
}
