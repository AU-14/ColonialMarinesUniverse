namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Scent;

/// <summary>
/// Remembers the last entity that picked this item up into a hand. Added server-side to any item on pickup,
/// so a Wendigo can catch that entity's scent from the item.
/// </summary>
[RegisterComponent]
public sealed partial class CMULastHolderComponent : Component
{
    [ViewVariables]
    public EntityUid? Holder;

    [ViewVariables]
    public TimeSpan At;
}
