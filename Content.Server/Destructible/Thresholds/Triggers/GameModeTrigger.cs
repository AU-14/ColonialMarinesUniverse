using System.Linq;
using Content.Server.GameTicking;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Destructible;
using Content.Shared.Destructible.Thresholds.Triggers;

namespace Content.Server.Destructible.Thresholds.Triggers;

/// <summary>
///     Triggers when the current game preset matches one of the specified modes.
/// </summary>
[Serializable]
[DataDefinition]
public sealed partial class GameModeTrigger : IThresholdTrigger
{
    /// <summary>
    ///     List of game preset IDs that will trigger this threshold.
    /// </summary>
    [DataField("modes")] public List<string> Modes { get; set; } = new();

    /// <summary>
    ///     If true, the trigger will activate when the current preset does NOT match any of <see cref="Modes"/>.
    /// </summary>
    [DataField("invert")]
    public bool Invert { get; set; }

    public bool Reached(Entity<DamageableComponent> damageable, SharedDestructibleSystem system)
    {
        if (system is not DestructibleSystem serverSystem)
            return false;

        var ticker = serverSystem.EntityManager.System<GameTicker>();
        var preset = ticker.CurrentPreset ?? ticker.Preset;
        if (preset == null)
            return false;

        var match = Modes.Contains(preset.ID);
        return Invert ? !match : match;
    }

    public int CompareTo(IThresholdTrigger? other) => 0;

    public bool Equals(IThresholdTrigger? other)
    {
        return other is GameModeTrigger trigger &&
               Invert == trigger.Invert &&
               Modes.SequenceEqual(trigger.Modes);
    }
}
