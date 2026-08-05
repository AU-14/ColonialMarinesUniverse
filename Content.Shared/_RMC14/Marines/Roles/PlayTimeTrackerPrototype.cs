// ReSharper disable CheckNamespace
namespace Content.Shared.Players.PlayTimeTracking;

public sealed partial class PlayTimeTrackerPrototype
{
    [DataField]
    public bool IsHumanoid;

    [DataField]
    public bool IsXeno;

    /// <summary>
    /// The localized name used when multiple jobs contribute to this tracker.
    /// If unset, consumers display the complete list of contributing job names.
    /// </summary>
    [DataField]
    public LocId? Name { get; private set; }

    /// <summary>
    /// Whether this tracker should appear in the playtime statistics menu.
    /// </summary>
    [DataField]
    public bool ShowInStatsMenu { get; private set; } = true;
}
