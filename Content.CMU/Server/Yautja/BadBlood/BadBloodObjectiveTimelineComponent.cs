namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// Tunables of the objective timeline (STATE-504). Ensured on the Bad Blood rule entity when the rule starts; the rule
/// YAML may configure it to retune. The timeline itself lives in <see cref="BadBloodObjectiveTimelineSystem"/>.
/// docs/GraphAudit-BadBlood-Objectives.md §5.6.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodObjectiveTimelineComponent : Component
{
    /// <summary>
    /// Deaths within this many tiles of an uplink site, a Totem or the Lodge count as an engagement at that site.
    /// </summary>
    [DataField]
    public float EngagementRadius = 12f;

    /// <summary>
    /// A Lodge reveal from the same source is recorded at most once per this long. Every hit on the Lodge reveals it.
    /// </summary>
    [DataField]
    public TimeSpan RevealDedupe = TimeSpan.FromSeconds(30);
}
