using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Dropship;

/// <summary>
///     A landing pad aboard a faction's ship for the third parties that faction calls in. Only shuttles summoned by the
///     ship's own faction may land here, and while that faction is shipside its summoned shuttles may land nowhere else.
///     A shuttle that lands here heads back to deep space shortly afterwards.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUSummonedThirdPartyDestinationComponent : Component
{
    /// <summary>How long a summoned shuttle stays on the pad before it returns to deep space.</summary>
    [DataField]
    public TimeSpan Departure = TimeSpan.FromSeconds(60);
}
