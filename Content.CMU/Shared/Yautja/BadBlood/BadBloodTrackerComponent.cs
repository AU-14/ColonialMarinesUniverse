using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ARCH-606: the Tracker caste, which finds and calls targets rather than outranging them. Added by the Tracker
/// caste prototype. Lighter armour, a cloak that comes back sooner (YautjaCloakSystem), and a "Mark for the clan" verb
/// that puts the prey on every Bad Blood's bracer map (BadBloodTrackerSystem). The caster is the clan's own.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodTrackerComponent : Component
{
    /// <summary>
    /// The Tracker's armour counts for this share of its value.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ArmorMultiplier = 0.85f;

    /// <summary>
    /// The bracer's cloak cooldown is multiplied by this.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CloakCooldownMultiplier = 0.75f;

    /// <summary>
    /// How long marked prey shows on the clan's bracer maps.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan MarkDuration = TimeSpan.FromSeconds(60);
}
