using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Round.Antags.Rider;

/// <summary>
/// On the host while ridden. Pairs with the hatchling's RiderComponent.
/// </summary>
[RegisterComponent]
public sealed partial class RiddenComponent : Component
{
    public EntityUid Rider;

    public bool ResistActive;

    /// <summary>
    /// Set when the latch went through the willing verb; willing hosts feed
    /// the rider's cooperative regen rate.
    /// </summary>
    public bool Willing;

    public EntityUid? ResistAction;

    /// <summary>
    /// 0 thread, 1 hold, 2 tight; the host reads grip qualitatively and only
    /// hears about it when the band changes.
    /// </summary>
    public int GripBand = 1;

    public TimeSpan RideStart;
}
