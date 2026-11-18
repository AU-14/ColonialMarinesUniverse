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

    /// <summary>
    /// CMU14 ECS-704: Blood Trail. Bleeding marines within this many tiles show on the clan's bracer maps...
    /// </summary>
    [DataField]
    public float BloodTrailRange = 15f;

    /// <summary>
    /// ...checked this often, each shown for <see cref="BloodTrailShowFor"/>.
    /// </summary>
    [DataField]
    public TimeSpan BloodTrailEvery = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan BloodTrailShowFor = TimeSpan.FromSeconds(6);

    [ViewVariables]
    public TimeSpan NextBloodTrail;

    /// <summary>
    /// CMU14 ECS-704: early warning. A marine this many tiles beyond a Totem's contest radius warns the Tracker
    /// before the contest starts...
    /// </summary>
    [DataField]
    public float WarningPadding = 6f;

    /// <summary>
    /// ...at most once per Totem in this long.
    /// </summary>
    [DataField]
    public TimeSpan WarningCooldown = TimeSpan.FromSeconds(60);

    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> WarnedAt = new();
}
