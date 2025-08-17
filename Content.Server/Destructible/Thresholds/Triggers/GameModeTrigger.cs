using Content.Server.GameTicking;
using Content.Shared.Damage;

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

    public bool Reached(DamageableComponent damageable, DestructibleSystem system)
    {
        var ticker = system.EntityManager.System<GameTicker>();
        var preset = ticker.CurrentPreset ?? ticker.Preset;
        if (preset == null)
            return false;

        var match = Modes.Contains(preset.ID);
        return Invert ? !match : match;
    }
}
