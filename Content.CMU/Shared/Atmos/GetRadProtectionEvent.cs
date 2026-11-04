using Content.Shared.Inventory;

namespace Content.Shared.CMU14.Atmos;

/// <summary>
/// Raised on an irradiated entity to collect wearable rad shielding.
/// The received dose is multiplied by the final amount before it becomes damage.
/// </summary>
[ByRefEvent]
public sealed class GetRadProtectionEvent : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = ~SlotFlags.POCKET;

    /// <summary>
    /// What to multiply the received dose by.
    /// </summary>
    public float Multiplier;

    public GetRadProtectionEvent()
    {
        Multiplier = 1f;
    }

    /// <summary>
    /// Reduce the received dose by a percentage.
    /// </summary>
    public void Reduce(float by)
    {
        Multiplier -= by;
        Multiplier = MathF.Max(Multiplier, 0f);
    }
}
